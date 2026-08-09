using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Automatic walking to the current target via the external vnavmesh plugin
/// (IPC): vnavmesh computes a path on the walkable-surface mesh and steers
/// the character around obstacles. This service starts/stops the walk,
/// watches progress every frame, feeds the audio beacon and announces
/// arrival. All IPC names and signatures verified against the vnavmesh
/// source (see docs/game-api.md -> "vnavmesh-IPC").
/// </summary>
public sealed class AutoWalkService : IDisposable
{
    /// <summary>Stop this close to the destination, in yalms/meters (interaction range).
    /// Public so a position-based walk to a browsed object stops as close as the
    /// walk to a game target would (Plugin.TryResolveMarkerDestination).</summary>
    public const float StopRange = 2.5f;

    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IClientState _clientState;
    private readonly TolkService _tolk;
    private readonly Configuration _config;
    private readonly PlacesService _places;
    private readonly RouteService _routes;
    private readonly ObjectNameService _objectNames;
    private readonly IPluginLog _log;

    private readonly ICallGateSubscriber<bool> _navIsReady;
    private readonly ICallGateSubscriber<float> _navBuildProgress;
    private readonly ICallGateSubscriber<Vector3, bool, float, bool> _moveCloseTo;
    private readonly ICallGateSubscriber<object> _pathStop;
    private readonly ICallGateSubscriber<bool> _pathIsRunning;
    private readonly ICallGateSubscriber<bool> _pathfindInProgress;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> _pointOnFloor;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> _nearestPoint;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> _nearestPointReachable;
    // DIAGNOSTIC (temporary): the waypoints of the path vnavmesh is actually
    // following. Lets us tell whether the destination is reachable (last
    // waypoint sits on the target) or the route jams short of it. Verified
    // against vnavmesh IPCProvider: Path.ListWaypoints -> List<Vector3>.
    private readonly ICallGateSubscriber<List<Vector3>> _pathListWaypoints;
    // Computes a route without walking it - used by the approach search.
    private readonly ICallGateSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>> _navPathfind;
    // Walks a fixed list of points, bypassing the pathfinder entirely.
    private readonly ICallGateSubscriber<List<Vector3>, bool, object> _pathMoveTo;

    private bool _active;
    private bool _sawRunning;          // the path actually started at least once

    /// <summary>Whether an auto-walk is currently running. Plugin.cs suppresses
    /// automatic target-change announcements while this is true - passing NPCs
    /// grab the soft target every few steps and each one would be announced
    /// with distance and direction (user feedback 2026-07-10).</summary>
    public bool IsActive => _active;

    /// <summary>Whether the follow mode is currently running (see <see cref="ToggleFollow"/>).
    /// Plugin.cs suppresses target-change announcements while this is true, for the
    /// same reason as <see cref="IsActive"/>.</summary>
    public bool IsFollowing => _following;
    private DateTime _startedAt;
    private ulong _targetId;           // 0 for position destinations (quest markers)
    private string _targetName = string.Empty;
    private Vector3 _destPosition;     // refreshed from the object each frame if _targetId != 0
    private float _stopRange = StopRange;

    // Progress tracking so the walk always ends with feedback and the user
    // hears it is working (a slow 190 m walk with no spoken updates felt broken
    // and got cancelled; log 2026-07-11 21:00). The auto-walk deliberately does
    // NOT sound the direction beacon - it was distracting while the game steers
    // for you (user 2026-07-12); the beacon stays with the manual walk guide.
    private ushort _startTerritory;    // announce success when the player crosses into a new zone
    private Vector3 _lastPosition;     // where the character last moved (for stall detection)
    private DateTime _lastMoveAt;
    // Remaining distance at the last spoken progress line - the next one waits
    // until another AutoWalkProgressStep metres are behind us (never the clock,
    // or a slow/blocked walk chatters while nothing happens).
    private float _lastProgressDistance;
    private bool _diagLoggedPath;      // DIAGNOSTIC: full waypoint route logged once per walk
    private DateTime _lastDiagAt;      // DIAGNOSTIC: throttles the per-second position log

    public AutoWalkService(
        IDalamudPluginInterface pluginInterface,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IClientState clientState,
        TolkService tolk,
        Configuration config,
        PlacesService places,
        RouteService routes,
        ObjectNameService objectNames,
        IPluginLog log)
    {
        _objectTable = objectTable;
        _targetManager = targetManager;
        _clientState = clientState;
        _tolk = tolk;
        _config = config;
        _places = places;
        _routes = routes;
        _objectNames = objectNames;
        _log = log;

        // Subscribing is always safe - the gates only fail on INVOKE while
        // vnavmesh is not loaded (IpcNotReadyError).
        _navIsReady         = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _navBuildProgress   = pluginInterface.GetIpcSubscriber<float>("vnavmesh.Nav.BuildProgress");
        _moveCloseTo        = pluginInterface.GetIpcSubscriber<Vector3, bool, float, bool>("vnavmesh.SimpleMove.PathfindAndMoveCloseTo");
        _pathStop           = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        _pathIsRunning      = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        _pathfindInProgress = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
        // Query.Mesh.PointOnFloor(p, allowUnlandable, halfExtentXZ) -> Vector3?
        // (vnavmesh IPCProvider decompiled 2026-07-11): finds the walkable
        // floor near p - built for exactly our case, 2D map coordinates
        // without a height (same mechanism as vnavmesh's own FlagToPoint).
        _pointOnFloor       = pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
        // NearestPoint(p, halfExtentXZ, halfExtentY) -> Vector3? finds the
        // closest mesh point INSIDE a box around p. Unlike PointOnFloor (which
        // casts straight DOWN and can snap to a floor far below a bridge/walkway
        // - log 2026-07-11: -12.9 -> -50.5), the bounded vertical extent keeps
        // the result near the player's level. See docs/game-api.md -> vnavmesh.
        _nearestPoint       = pluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
        // Same signature as NearestPoint, but vnavmesh passes allowUnreachable:
        // false - patches cut off from the zone's main surface are skipped. See
        // SnapToReachableMesh for what that flag really means (decompiled).
        _nearestPointReachable = pluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPointReachable");
        _pathListWaypoints  = pluginInterface.GetIpcSubscriber<List<Vector3>>("vnavmesh.Path.ListWaypoints");
        // Pathfind(from, to, fly) -> Task<List<Vector3>>: computes a route
        // WITHOUT walking it. The ascent probe below needs exactly that - try
        // many candidate destinations and look at the routes, without moving
        // the character an inch. Signature decompiled 2026-08-06 from
        // vnavmesh.dll (IPCProvider -> NavmeshManager.QueryPathBasic).
        _navPathfind        = pluginInterface.GetIpcSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>("vnavmesh.Nav.Pathfind");
        // Path.MoveTo(waypoints, fly): steers the character along the given
        // points WITHOUT any pathfinding - vnavmesh hands them straight to
        // FollowPath.Move (IPCProvider, decompiled 2026-08-06). This is the one
        // way to cross a gap the navigation mesh does not know about.
        _pathMoveTo         = pluginInterface.GetIpcSubscriber<List<Vector3>, bool, object>("vnavmesh.Path.MoveTo");
    }

    /// <summary>
    /// Resolves the walkable height for a 2D map position (map markers carry
    /// no Y). Uses the player's height as the search origin. Returns null if
    /// vnavmesh is missing/not ready or no floor exists near the point.
    /// </summary>
    public Vector3? ResolveFloorPoint(Vector3 approximate)
    {
        // try-catch: IPC into a foreign plugin (vnavmesh may be missing/loading)
        try
        {
            if (!_navIsReady.InvokeFunc()) return null;

            // Prefer NearestPoint with a bounded vertical extent: it stays near
            // the given height instead of dropping to a lower floor. 10 m XZ
            // covers markers a little off the path, 10 m Y catches small level
            // changes without falling through to a floor tens of metres below.
            var nearest = _nearestPoint.InvokeFunc(approximate, 10f, 10f);
            if (nearest.HasValue)
            {
                _log.Info($"[Orte] NearestPoint ({approximate.X:F1}|{approximate.Y:F1}|{approximate.Z:F1}) -> " +
                          $"({nearest.Value.X:F1}|{nearest.Value.Y:F1}|{nearest.Value.Z:F1})");
                return nearest;
            }

            // Second pass with a tall column: 2D markers use the PLAYER's height
            // as reference, but a target hundreds of metres away can sit on very
            // different ground (log 2026-07-13 10:11/10:18: aetheryte 0.5 km off
            // and a transition failed with the +-10 m box). NearestPoint picks
            // the mesh point CLOSEST to the input, so with several levels the
            // one nearest the reference height still wins - unlike PointOnFloor's
            // blind down-cast (bridge trap, V4.41).
            nearest = _nearestPoint.InvokeFunc(approximate, 10f, 100f);
            if (nearest.HasValue)
            {
                _log.Info($"[Orte] NearestPoint (hohe SÃ¤ule) ({approximate.X:F1}|{approximate.Y:F1}|{approximate.Z:F1}) -> " +
                          $"({nearest.Value.X:F1}|{nearest.Value.Y:F1}|{nearest.Value.Z:F1})");
                return nearest;
            }

            // Fallback: PointOnFloor casts straight down - a last resort when no
            // mesh sits near the height (e.g. the marker is above a deep drop).
            var floor = _pointOnFloor.InvokeFunc(approximate, false, 5f);
            _log.Info($"[Orte] NearestPoint leer, PointOnFloor ({approximate.X:F1}|{approximate.Y:F1}|{approximate.Z:F1}) -> " +
                      (floor.HasValue ? $"({floor.Value.X:F1}|{floor.Value.Y:F1}|{floor.Value.Z:F1})" : "null"));
            return floor;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Orte] Floor-Query-IPC fehlgeschlagen");
            return null;
        }
    }

    /// <summary>
    /// Resolves a fishing spot's WATER-CENTRE position to the nearest walkable
    /// bank you can cast from. Fishing spots sit in the middle of the water where
    /// no mesh exists, so the generic <see cref="ResolveFloorPoint"/> (a 10 m box
    /// plus a straight-down cast) either finds nothing or snaps to a lakebed far
    /// below - the player then does not land at the water. Here we search a WIDE
    /// horizontal area (banks can be tens of metres from the centre) but a THIN
    /// vertical slab around the player's height, so the nearest point returned is
    /// the bank at water-surface level, never a floor above or below it.
    /// Returns null (caller falls back to the generic resolver) when vnavmesh is
    /// missing/not ready or no bank is found within range.
    /// </summary>
    public Vector3? ResolveNearestBank(Vector3 waterCentre)
    {
        // try-catch: IPC into a foreign plugin (vnavmesh may be missing/loading).
        try
        {
            if (!_navIsReady.InvokeFunc()) return null;

            // 75 m horizontal covers a bank well out from a large water centre;
            // 8 m vertical keeps the result at the player's level (the bank),
            // not a lakebed or bridge. NearestPoint returns the CLOSEST mesh
            // point in the box, so the near bank always wins over a far one.
            var bank = _nearestPoint.InvokeFunc(waterCentre, 75f, 8f);
            _log.Info($"[Angeln] Ufer: NearestPoint ({waterCentre.X:F1}|{waterCentre.Y:F1}|{waterCentre.Z:F1}) -> " +
                      (bank.HasValue ? $"({bank.Value.X:F1}|{bank.Value.Y:F1}|{bank.Value.Z:F1})" : "null"));
            return bank;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Angeln] Ufer-Query-IPC fehlgeschlagen");
            return null;
        }
    }

    /// <summary>Starts the auto-walk to the current game target, or stops a running one.</summary>
    public void Toggle()
    {
        // A running final hop is the tail of the player's last walk - the key
        // ends it instead of starting something new on top of it.
        if (StopFinalHopIfRunning()) return;

        // Starting a one-shot walk cancels a running follow (they share vnavmesh).
        StopFollowQuiet();

        if (_active)
        {
            Stop(announce: true);
            return;
        }

        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoTargetSelectN);
            return;
        }

        if (!TryStartPath(target.Position, StopRange)) return;

        _targetId = target.GameObjectId;
        _targetName = _objectNames.Describe(target);   // never a blank in "Laufe zu ..."
        _destPosition = target.Position;
        // A game object carries its real height - nothing guessed here.
        _destHeightIsGuess = false;
        _stopRange = StopRange;
        BeginWalk();
    }

    /// <summary>
    /// Starts the auto-walk to a fixed world position (quest markers and
    /// waypoints have no game object to target), or stops a running one.
    /// The caller passes the final stop range: tight for locations (~1 m) so
    /// the player actually arrives on the spot, tighter still for zone
    /// transitions so they trigger, or the objective radius for quest areas.
    /// The position should already be snapped onto the walkable mesh so
    /// vnavmesh can finish within that range.
    /// </summary>
    /// <param name="heightIsGuess">True when the height of <paramref name="position"/>
    /// was guessed rather than known - everything that comes from the 2D map is.
    /// See <see cref="_destHeightIsGuess"/>.</param>
    public void ToggleToPosition(Vector3 position, string name, float stopRange,
                                 bool heightIsGuess = false)
    {
        if (StopFinalHopIfRunning()) return;

        StopFollowQuiet();

        if (_active)
        {
            Stop(announce: true);
            return;
        }

        if (!TryStartPath(position, stopRange)) return;

        _targetId = 0;
        _targetName = name;
        _destPosition = position;
        _destHeightIsGuess = heightIsGuess;
        _stopRange = stopRange;
        BeginWalk();
    }

    // â”€â”€ Ziel folgen (kontinuierlich) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    //
    // Unlike Toggle() - which computes ONE path and stops on arrival - follow
    // keeps re-issuing PathfindAndMoveCloseTo to the target's CURRENT position,
    // so the character trails a moving player and stops when they stop (user
    // request 2026-07-26). FFXIV has no plugin-callable native "follow" (verified
    // against FFXIVClientStructs: MoveController carries no follow, only companion/
    // mount/camera/map "follow" exist), so this rebuilds it on vnavmesh - the same
    // engine the auto-walk already uses.

    private bool _following;
    private ulong _followTargetId;
    private string _followName = string.Empty;
    private ushort _followStartTerritory;
    private Vector3 _lastFollowDest;      // target position the last path was issued for
    private DateTime _lastFollowPathAt;

    /// <summary>Trail distance in yalms: stop this far behind the target.</summary>
    private const float FollowDistance = 3f;
    /// <summary>Only re-path once the target has drifted this far from the last
    /// commanded destination - keeps a slow-moving target from re-pathing per frame.</summary>
    private const float FollowRepathMove = 1.5f;
    /// <summary>Minimum seconds between re-paths (throttle for vnavmesh).</summary>
    private const double FollowRepathIntervalS = 0.4;

    /// <summary>
    /// Starts following the current game target, or stops a running follow.
    /// The character trails the target at <see cref="FollowDistance"/> and halts
    /// when the target halts; a second key press ends it. Mutually exclusive with
    /// the one-shot auto-walk (the caller stops the walk guide first).
    /// </summary>
    public void ToggleFollow()
    {
        if (_following)
        {
            StopFollow(announce: true);
            return;
        }

        // A running one-shot walk and follow must not fight over vnavmesh.
        if (_active) Stop(announce: false);

        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.FollowNoTarget);
            return;
        }
        if (target.GameObjectId == (_objectTable.LocalPlayer?.GameObjectId ?? 0))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.FollowSelf);
            return;
        }

        _following = true;
        _followTargetId = target.GameObjectId;
        // Same resolution as the browser and the target announcement: a raw name
        // is empty for a whole class of objects and unspeakable ("?") for
        // another, so "folge <nichts>" was possible here (user 2026-08-08).
        _followName = _objectNames.Describe(target);
        _followStartTerritory = (ushort)_clientState.TerritoryType;
        _lastFollowDest = default;         // force the first path immediately
        _lastFollowPathAt = DateTime.MinValue;
        _log.Info($"[Nav] Folgen: gestartet -> {_followName} (id={_followTargetId:X})");
        _tolk.SpeakInterrupt(AccessibilityStrings.Following(_followName));
    }

    private void StopFollow(bool announce)
    {
        if (!_following) return;
        _following = false;

        // try-catch: IPC into a foreign plugin (see Toggle)
        try { _pathStop.InvokeAction(); }
        catch (Exception ex) { _log.Error(ex, "[Nav] Folgen: Path.Stop fehlgeschlagen"); }

        _log.Info("[Nav] Folgen: gestoppt.");
        if (announce) _tolk.SpeakInterrupt(AccessibilityStrings.FollowStopped);
    }

    /// <summary>Ends a running follow without announcement (e.g. when a walk takes over).</summary>
    public void StopFollowQuiet() => StopFollow(announce: false);

    /// <summary>Runs every frame while follow is active. Re-issues the vnavmesh path
    /// toward the target's current position and ends the follow when the target
    /// vanishes, the player leaves, or the zone changes.</summary>
    private void FollowUpdate()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            // Logout/zone change - vnavmesh drops the path itself.
            StopFollow(announce: false);
            return;
        }

        if ((ushort)_clientState.TerritoryType != _followStartTerritory)
        {
            _log.Info("[Nav] Folgen: Gebiet gewechselt, beende.");
            StopFollow(announce: false);
            _tolk.SpeakInterrupt(AccessibilityStrings.FollowStoppedZone);
            return;
        }

        var target = _objectTable.FirstOrDefault(o => o.GameObjectId == _followTargetId);
        if (target == null)
        {
            _log.Info($"[Nav] Folgen: Ziel {_followTargetId:X} nicht mehr da, beende.");
            StopFollow(announce: false);
            _tolk.SpeakInterrupt(AccessibilityStrings.FollowTargetGone(_followName));
            return;
        }

        var dest = target.Position;
        var distance = Vector3.Distance(player.Position, dest);
        var now = DateTime.UtcNow;

        // Nothing to do while already within trail distance - let the target
        // pull away first (the character stops when the target stops).
        if (distance <= FollowDistance + 0.5f) return;

        // Throttle re-paths and skip while one is still being computed.
        if ((now - _lastFollowPathAt).TotalSeconds < FollowRepathIntervalS) return;

        bool running, computing;
        // try-catch: IPC into a foreign plugin (see Toggle)
        try
        {
            computing = _pathfindInProgress.InvokeFunc();
            running   = _pathIsRunning.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Folgen: Status-IPC fehlgeschlagen, breche ab");
            StopFollow(announce: false);
            _tolk.SpeakInterrupt(AccessibilityStrings.FollowAbortedNoResponse);
            return;
        }
        if (computing) return;

        // Re-path when the target drifted enough OR the previous path already
        // finished (we are idle but still beyond the trail distance - the target
        // walked off while we stood still).
        if (Vector3.Distance(dest, _lastFollowDest) < FollowRepathMove && running) return;

        // try-catch: IPC into a foreign plugin (vnavmesh may vanish mid-follow)
        try
        {
            _moveCloseTo.InvokeFunc(dest, false, FollowDistance);
            _lastFollowDest = dest;
            _lastFollowPathAt = now;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Folgen: MoveCloseTo fehlgeschlagen, breche ab");
            StopFollow(announce: false);
            _tolk.SpeakInterrupt(AccessibilityStrings.FollowAbortedUnavailable);
        }
    }

    // â”€â”€ Zugang zum Ziel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    //
    // Ein Ziel kann auf einer Flaeche liegen, die im Wegenetz gar nicht an
    // unserer haengt (Schiffsdeck, Empore, Balkon). Dorthin fuehrt KEIN Weg -
    // kein Umweg und kein Zwischen-Wegpunkt aendert das. Offline am gecachten
    // Wegenetz nachgemessen (2026-08-06, Schiff Astalicia in Limsa Lominsa):
    // die Schiffsflaeche mit 129 Polygonen hat NULL Verbindungen zur Kai-
    // Flaeche des Spielers mit 1468 Polygonen.
    //
    // Erkennbar ist das an einer Eigenheit von vnavmesh: PathfindMesh haengt
    // das ZIEL immer als letzten Wegpunkt an die Liste an, auch wenn die
    // Wegsuche es nie erreicht hat (vnavmesh.dll -> NavmeshQuery.PathfindMesh,
    // dekompiliert 2026-08-06). Der VORLETZTE Wegpunkt ist deshalb das Ende
    // des ECHTEN Pfades. Das erklaert auch den vermeintlichen "9-Meter-Sprung"
    // aus den Logs: der echte Pfad endete nach 0,9 m, der Rest war nur das
    // angeklebte Ziel - es war nie eine zu steile Treppe, sondern gar kein Weg.

#if DEBUG
    // ── Luecke ueberqueren (Versuchsaufbau Astalicia) ────────────────
    //
    // Das Wegenetz kennt den Weg an Bord nicht - deshalb kommt weder vnavmesh
    // noch unsere Zugangssuche dort hin, beide fragen dieselbe Karte. Der
    // einzige Ausweg ist `Path.MoveTo`: eine feste Punktliste abfahren, ganz
    // OHNE Wegsuche. Die Figur laeuft dann auch ueber Boden, den das Netz nicht
    // kennt.
    //
    // Die Koordinaten stammen aus der Offline-Vermessung des gecachten
    // Wegenetzes (2026-08-06): engste Stelle zwischen Kai-Flaeche und
    // Schiffsflaeche, 1,2 m waagerecht und 0,5 m hoch. HARTKODIERT und nur im
    // Debug-Build - das ist ein Versuch an EINER Stelle, kein fertiges Feature.
    // Traegt der Versuch, wird daraus eine allgemeine Ueberquerung.
    private static readonly Vector3 PlankeKai    = new(-274.0f, 11.5f, 190.0f);
    private static readonly Vector3 PlankeSchiff = new(-272.8f, 12.0f, 190.0f);
    private static readonly Vector3 PlankeDeck   = new(-271.0f, 12.0f, 189.5f);

    /// <summary>Points to walk without pathfinding once the approach walk has
    /// finished, and where that crossing is supposed to start.</summary>
    private List<Vector3>? _pendingGapCross;
    private Vector3 _gapFrom;

    /// <summary>How far from the crossing's start we may be and still set off.
    /// Guards against firing after the player cancelled the walk somewhere
    /// else entirely.</summary>
    private const float GapCrossMaxOffset = 3f;

    /// <summary>Zones the surveyed coordinates apply to. Both rows carry the
    /// same background (ffxiv/sea_s1/twn/s1t2/level/s1t2, read from the
    /// TerritoryType sheet 2026-08-07) and therefore the same geometry.
    /// Without this check the command would steer the character to those
    /// coordinates in whatever zone it is typed in.</summary>
    private static readonly ushort[] PlankeTerritories = { 129, 404 };

    /// <summary>Crossing queued for the framework thread by the side check.
    /// That check runs on a worker - the path queries are async - and must not
    /// start a walk itself.</summary>
    private (Vector3 To, List<Vector3> Cross)? _pendingPlankRun;

    /// <summary>
    /// Test run for the Astalicia: walks to the near side of the gangway the
    /// normal way, then crosses the gap the mesh does not know using
    /// <c>Path.MoveTo</c>. Works in BOTH directions - a one-way version strands
    /// the player on the ship, which is exactly what happened 2026-08-07.
    /// Debug only.
    /// </summary>
    public void CrossPlank()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var territory = (ushort)_clientState.TerritoryType;
        if (Array.IndexOf(PlankeTerritories, territory) < 0)
        {
            _log.Info($"[Planke] Gebiet {territory} ist nicht Untere Decks " +
                      $"({string.Join("/", PlankeTerritories)}) - die vermessenen Koordinaten " +
                      $"gelten dort nicht.");
            _tolk.SpeakInterrupt(AccessibilityStrings.GapCrossWrongZone);
            return;
        }

        // Which side of the gap are we on? NOT decided by height: the two sides
        // differ by 0,5 m and the character habitually stands between them
        // (measured 2026-08-07 00:46, Y 11,9 while still on the ship's side
        // while the quay lies at 11,5). The mesh itself answers it - the side
        // we stand on is the one a route actually reaches.
        var me = player.Position;
        var kaiTask = SafePathfind(me, PlankeKai);
        var deckTask = SafePathfind(me, PlankeDeck);

        Task.Run(async () =>
        {
            var kaiRoute = await AwaitRoute(kaiTask).ConfigureAwait(false);
            var deckRoute = await AwaitRoute(deckTask).ConfigureAwait(false);
            var kaiOk = RouteReachesSpot(kaiRoute, PlankeKai, me);
            var deckOk = RouteReachesSpot(deckRoute, PlankeDeck, me);
            _log.Info($"[Planke] Seitenpruefung von <{me.X:F1}, {me.Y:F1}, {me.Z:F1}>: " +
                      $"Kai erreichbar={kaiOk}, Deck erreichbar={deckOk}.");

            // Quay first: that is where the player stands in the normal case,
            // and if both sides answer yes the mesh is connected anyway and the
            // ordinary walk does the job.
            if (kaiOk)
            {
                _log.Info($"[Planke] Richtung an Bord. Etappe 1 zum Kai " +
                          $"<{PlankeKai.X:F1}, {PlankeKai.Y:F1}, {PlankeKai.Z:F1}>, danach ohne " +
                          $"Wegsuche ueber <{PlankeSchiff.X:F1}, {PlankeSchiff.Y:F1}, {PlankeSchiff.Z:F1}> " +
                          $"nach <{PlankeDeck.X:F1}, {PlankeDeck.Y:F1}, {PlankeDeck.Z:F1}>.");
                _pendingPlankRun = (PlankeKai, new List<Vector3> { PlankeSchiff, PlankeDeck });
            }
            else if (deckOk)
            {
                _log.Info($"[Planke] Richtung zurueck an Land. Etappe 1 zum Deck " +
                          $"<{PlankeDeck.X:F1}, {PlankeDeck.Y:F1}, {PlankeDeck.Z:F1}>, danach ohne " +
                          $"Wegsuche ueber <{PlankeSchiff.X:F1}, {PlankeSchiff.Y:F1}, {PlankeSchiff.Z:F1}> " +
                          $"nach <{PlankeKai.X:F1}, {PlankeKai.Y:F1}, {PlankeKai.Z:F1}>.");
                _pendingPlankRun = (PlankeDeck, new List<Vector3> { PlankeSchiff, PlankeKai });
            }
            else
            {
                _log.Info("[Planke] Weder Kai noch Deck sind von hier aus erreichbar - " +
                          "die Figur haengt an einer dritten Flaeche.");
                _tolk.SpeakInterrupt(AccessibilityStrings.GapCrossNoSide);
            }
        });
    }

    /// <summary>Half-width of the square the ground probe samples around the
    /// player, and its grid spacing. 0,25 m matches the navmesh's own CellSize,
    /// so the probe cannot miss surface the build would have seen.</summary>
    private const float ProbeExtent = 3f;
    private const float ProbeStep = 0.25f;

    /// <summary>How far a mesh point may sit from the collision hit and still
    /// count as "the mesh knows this floor".</summary>
    private const float ProbeMeshTolerance = 0.5f;

    /// <summary>
    /// Compares the GAME's own collision floor against the navigation mesh
    /// around the player. This is the one measurement that separates the two
    /// possible causes of a stranded character: either there is no floor at
    /// all (only <c>Path.MoveTo</c> can cross, as today), or there IS floor and
    /// the mesh build discards it - in which case a per-zone customization of
    /// the build settings would fix the crossing properly, and every similar
    /// spot with it. Collision comes straight from the game
    /// (BGCollisionModule.RaycastMaterialFilter), so it is independent of
    /// vnavmesh. Debug only.
    /// </summary>
    public unsafe void ProbeGround()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;
        var me = player.Position;

        var module = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->BGCollisionModule;
        if (module == null)
        {
            _log.Warning("[Boden] BGCollisionModule ist null - Kollision nicht abfragbar.");
            _tolk.SpeakInterrupt(AccessibilityStrings.GroundProbeUnavailable);
            return;
        }

        _log.Info($"[Boden] Raster um <{me.X:F2}, {me.Y:F2}, {me.Z:F2}>, +/-{ProbeExtent:F1} m " +
                  $"in {ProbeStep:F2} m Schritten. Netz-Toleranz {ProbeMeshTolerance:F2} m.");

        var down = new Vector3(0f, -1f, 0f);
        int hits = 0, onMesh = 0, offMesh = 0, logged = 0;
        for (var dx = -ProbeExtent; dx <= ProbeExtent; dx += ProbeStep)
            for (var dz = -ProbeExtent; dz <= ProbeExtent; dz += ProbeStep)
            {
                // Start above head height: the floor we are hunting (a deck,
                // a gangway) can sit HIGHER than where we stand, and a ray
                // started at our feet would never see it.
                var origin = new Vector3(me.X + dx, me.Y + 3f, me.Z + dz);
                // Static overload - it resolves the module itself; the null
                // check above only tells us the collision scene is loaded.
                if (!FFXIVClientStructs.FFXIV.Common.Component.BGCollision.BGCollisionModule
                        .RaycastMaterialFilter(origin, down, out var hit, 12f)) continue;
                hits++;

                // Tight box on purpose - the question is whether the mesh
                // covers THIS spot, not whether there is mesh somewhere near.
                var near = NearestMeshPoint(hit.Point, ProbeMeshTolerance, ProbeMeshTolerance);
                if (near != null) { onMesh++; continue; }

                offMesh++;
                // Steepness decides whether the build was even allowed to keep
                // this surface: anything above AgentMaxSlopeDeg (55) is dropped
                // by design, and no customization changes that.
                var slope = MathF.Acos(Math.Clamp(hit.Normal.Y, -1f, 1f)) * 180f / MathF.PI;
                if (logged++ < 60)
                    _log.Info($"[Boden] BODEN OHNE NETZ bei <{hit.Point.X:F2}, {hit.Point.Y:F2}, " +
                              $"{hit.Point.Z:F2}>, Neigung {slope:F0} Grad.");
            }

        _log.Info($"[Boden] Ergebnis: {hits} Treffer, davon {onMesh} mit Netz und {offMesh} OHNE. " +
                  $"Viele Treffer ohne Netz = das Wegenetz verwirft vorhandenen Boden (eine " +
                  $"Zonen-Anpassung koennte helfen). Null Treffer ueber der Luecke = dort ist " +
                  $"wirklich kein Boden.");
        _tolk.SpeakInterrupt(AccessibilityStrings.GroundProbeResult(hits, offMesh));
    }

#endif

    // Both helpers below started out inside the Astalicia experiment above and
    // moved out of it when the near-miss walk (2026-08-07) turned them into
    // production code: it uses the same two tools - check whether the mesh
    // covers a spot, and steer without pathfinding.

    /// <summary>Nearest navmesh point within a tight box, or null if the mesh
    /// does not cover the spot. Separate from <see cref="SnapToMesh"/>, whose
    /// wide box (3 m / 15 m) would answer "yes" almost anywhere.</summary>
    private Vector3? NearestMeshPoint(Vector3 probe, float halfExtentXZ, float halfExtentY)
    {
        // try-catch: IPC into a foreign plugin (see Toggle).
        try { return _nearestPoint.InvokeFunc(probe, halfExtentXZ, halfExtentY); }
        catch (Exception ex) { _log.Error(ex, "[Boden] NearestPoint-IPC fehlgeschlagen"); return null; }
    }

    /// <summary>Steers along fixed points with no pathfinding involved.</summary>
    private void MoveWithoutPathfinding(List<Vector3> points)
    {
        // try-catch: IPC into a foreign plugin (see Toggle).
        try { _pathMoveTo.InvokeAction(points, false); }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Path.MoveTo-IPC fehlgeschlagen");
            _tolk.SpeakInterrupt(AccessibilityStrings.NoNavmeshPlugin);
        }
    }

    /// <summary>Approach spot the search found, waiting to be walked to on the
    /// next framework tick. Written from the search's worker thread, read and
    /// cleared in <see cref="Update"/>.</summary>
    private (Vector3 Pos, string Name)? _pendingApproachWalk;

    /// <summary>Whether the destination's HEIGHT was guessed rather than known.
    /// True for everything that comes off the 2D map (places, aetherytes, zone
    /// transitions, quest markers, typed coordinates): the height is filled in
    /// from the navmesh using the PLAYER's height as the reference, which picks
    /// the nearest storey to us - not necessarily the right one. With a known
    /// height a big vertical gap means "different floor, no way there"; with a
    /// guessed one it usually means the guess was wrong.</summary>
    private bool _destHeightIsGuess;

    /// <summary>Near-miss redirect parked by the route check, started on the next
    /// tick. Not started straight away for the same reason as the plank run: the
    /// check sits inside the waypoint evaluation of the CURRENT walk, and
    /// restarting from there would re-enter it mid-flight.
    /// <paramref name="SpotIsGoal"/> marks the wrong-storey case: there the
    /// path's end IS the destination, so nothing is left to walk afterwards.</summary>
    private (Vector3 Spot, Vector3 Goal, string Name, bool SpotIsGoal)? _pendingNearMissWalk;

    /// <summary>Destination of a running near-miss walk: set once the redirect
    /// has started, read on arrival to drive the last few metres,
    /// cleared by <see cref="Stop"/> and by every fresh walk the player starts.</summary>
    private (Vector3 Goal, string Name)? _nearMissGoal;

    /// <summary>The place the PLAYER asked for, kept across every redirect.
    /// A walk can be restarted internally several times (wrong storey, path
    /// ending short, approach spot), and each restart overwrites
    /// <see cref="_destPosition"/> with an intermediate point. When a later
    /// stage has to fall back on the mesh search, it must search around the
    /// real destination - searching around an intermediate point looks for a
    /// way to somewhere the player never named.</summary>
    private (Vector3 Goal, string Name)? _walkOrigin;

    /// <summary>Whether the mesh search already ran for this walk. The search
    /// ends in a walk of its own, which could fail again and start it anew -
    /// one attempt per destination is the hard stop against that loop.</summary>
    private bool _approachTried;

    /// <summary>A final hop in progress: the character is being steered the last
    /// few metres without pathfinding. Watched in <see cref="Update"/> because
    /// <c>Path.MoveTo</c> is fire-and-forget - it reports nothing back.</summary>
    private (Vector3 Goal, string Name, DateTime StartedAt)? _finalHop;

    /// <summary>Spacing of the ground samples along the final hop. One metre is
    /// well under the width of anything the character could fall off.</summary>
    private const float FinalHopProbeStep = 1f;

    /// <summary>Box around each sample the mesh has to reach into for the spot
    /// to count as solid ground. Tight on purpose - the question is whether the
    /// mesh covers THIS spot. Measured on the case this was built for: the
    /// samples sat 0,0-0,5 m from real mesh throughout.</summary>
    private const float FinalHopProbeXZ = 1f;
    private const float FinalHopProbeY = 1.5f;

    /// <summary>How close to the destination the hop counts as arrived.</summary>
    private const float FinalHopArrival = 2f;

    /// <summary>After this long the hop is given up on. Generous: 15 m at
    /// walking pace is a few seconds, and a zone transition adds a loading
    /// screen during which the character is gone from the object table.</summary>
    private const double FinalHopTimeoutS = 20;

    /// <summary>Stop range for the walk to an approach spot: tight, because the
    /// point of it is to stand exactly there and continue on foot.</summary>
    private const float ApproachStopRange = 1f;

    /// <summary>How far a route may end beside a CANDIDATE spot and still count
    /// as reaching it. Much tighter than <see cref="UnreachableGap"/>: the spot
    /// was snapped onto the mesh, so a real route lands on it exactly.</summary>
    private const float ApproachSnapTolerance = 1f;

    /// <summary>Distance from the last REAL waypoint to the destination above
    /// which the destination counts as unreachable. Wide enough to absorb stop
    /// range and mesh jitter, tight enough to catch a missing connection.</summary>
    private const float UnreachableGap = 3f;

    /// <summary>How far the real path may end short of the destination and still
    /// be worth walking: the character is set down at the path's end and told how
    /// far and in which direction the rest goes on foot, instead of the walk
    /// being refused outright. Chosen by the user (2026-08-07) after the measured
    /// case below; beyond it the hard abort stays, because a spot that far off is
    /// usually not "just short" but somewhere else entirely.
    ///
    /// The case: the "Übergang nach Nordwald" marker in the Central Shroud sits
    /// on a 57-polygon patch of mesh with no link to the 22.937 polygons the
    /// player stands on, so a 652 m route was refused over its last 9,1 m. The
    /// ground there is continuous - sampled offline from the vnavmesh cache every
    /// 0,5 m, mesh at every point, height rising 74,2 -> 74,8 - only the polygon
    /// link is missing, so the player can simply walk the rest.
    /// </summary>
    private const float NearMissGap = 15f;

    /// <summary>Ring radii (metres) sampled around an unreachable destination
    /// when looking for the closest spot one CAN walk to. Sixteen bearings per
    /// ring: with only eight, a narrow gangway between two levels falls between
    /// the spokes and the search settles for a spot on the wrong storey.</summary>
    private static readonly float[] ApproachRadii = { 5f, 9f, 13f, 17f, 22f, 28f };

    /// <summary>Bearings sampled per ring.</summary>
    private const int ApproachBearings = 16;

    /// <summary>How many height levels each bearing is probed at, spread evenly
    /// between our own height and the destination's. Probing only from our own
    /// feet always snaps onto our own storey - measured 2026-08-06 on the
    /// Astalicia, where every single candidate landed on the quay 9 m below the
    /// deck and the landing at +4 m (the actual way in) was never even seen.</summary>
    private const int ApproachLevels = 4;

    /// <summary>How much a metre of height counts against a metre of ground when
    /// ranking approach spots. Standing 3 m away but 9 m below the destination
    /// is worthless - you are simply underneath it; standing 14 m away and level
    /// with it is the way in. Plain 3D distance rates those the wrong way round,
    /// so height weighs several times heavier than ground.</summary>
    private const float ApproachHeightWeight = 5f;

    /// <summary>
    /// Whether a vnavmesh route actually arrives at <paramref name="goal"/>.
    /// The last waypoint is always the goal itself - vnavmesh appends it
    /// unconditionally - so the SECOND TO LAST one is where the real path ends.
    /// A route of fewer than two points carries no real path at all.
    /// </summary>
    /// <param name="extraTolerance">Added to the allowed gap. The auto-walk
    /// passes its stop range here: with a range &gt; 0 vnavmesh deliberately ends
    /// the path short of the destination (GoalRadiusHeuristic), which would
    /// otherwise read as "unreachable" on every quest area.</param>
    public static bool RouteReachesGoal(IReadOnlyList<Vector3>? route, Vector3 goal,
                                        float extraTolerance = 0f)
        => route is { Count: >= 2 }
           && Vector3.Distance(route[^2], goal) <= UnreachableGap + extraTolerance;

    /// <summary>A climb of at least this much within one segment is examined.
    /// Below it we are looking at kerbs and doorsteps.</summary>
    private const float ImpossibleRise = 2f;

    /// <summary>How far sideways the wrong-storey correction may look for mesh
    /// under its computed point. Deliberately small: the point already carries
    /// the marker's own X/Z, so anything further out is no longer the place the
    /// player asked for but merely somewhere nearby - and finding that spot is
    /// the mesh search's job, which ranks candidates properly.</summary>
    private const float WrongStoreySnapXZ = 3f;

    /// <summary>Height gained per metre of ground above which a segment cannot
    /// be a staircase or ramp but only a hole in the mesh. Checked against real
    /// geometry: the Astalicia's own deck ramp climbs 3 m over 5,1 m of ground
    /// (0,59) and stays well clear, while the phantom hop that fooled the first
    /// version climbed 9,1 m over 5,2 m (1,75).</summary>
    private const float ImpossibleSlope = 1.5f;

    /// <summary>
    /// Whether the route contains a hop the character cannot physically climb.
    /// Needed ALONGSIDE <see cref="RouteReachesGoal"/>, not instead of it: when
    /// the search finds no path at all it returns just [start, goal], so the
    /// second-to-last waypoint IS the goal and the distance check reads zero -
    /// measured 2026-08-07 00:25, where a spot 9,1 m straight up was reported
    /// as reachable and the walk then stalled on the quay. The distance check
    /// catches partial paths, this one catches the empty ones.
    /// </summary>
    private static bool RouteHasImpossibleJump(IReadOnlyList<Vector3> route, Vector3 from,
                                               out Vector3 at, out float rise)
    {
        var prev = from;
        foreach (var wp in route)
        {
            var dxz = MathF.Sqrt((wp.X - prev.X) * (wp.X - prev.X) + (wp.Z - prev.Z) * (wp.Z - prev.Z));
            var dy = wp.Y - prev.Y;
            if (dy >= ImpossibleRise && dy > dxz * ImpossibleSlope)
            {
                at = prev;
                rise = dy;
                return true;
            }
            prev = wp;
        }
        at = default;
        rise = 0f;
        return false;
    }

    /// <summary>Whether a route both ends at the destination AND consists only
    /// of segments the character can actually walk. Both halves are needed -
    /// see <see cref="RouteHasImpossibleJump"/>.</summary>
    private bool RouteIsWalkable(IReadOnlyList<Vector3>? route, Vector3 from, Vector3 goal,
                                 float extraTolerance, string was)
    {
        if (!RouteReachesGoal(route, goal, extraTolerance)) return false;
        if (!RouteHasImpossibleJump(route!, from, out var at, out var rise)) return true;
        _log.Info($"[Zugang] {was}: Route endet zwar am Ziel, enthaelt aber einen Sprung von " +
                  $"{rise:F1} m bei <{at.X:F1}, {at.Y:F1}, {at.Z:F1}> - den kann die Figur nicht " +
                  $"steigen, das ist ein Loch im Wegenetz.");
        return false;
    }

    /// <summary>
    /// Whether a route genuinely arrives at a spot that sits ON the mesh (a
    /// snapped candidate, a surveyed crossing point). Tighter than
    /// <see cref="RouteIsWalkable"/> on purpose: such a spot is a mesh point,
    /// so a real route lands exactly on it, and the 3 m
    /// <see cref="UnreachableGap"/> would wave through routes that only get
    /// near - measured 2026-08-07 00:46, where a route ending 1,3 m short on
    /// the WRONG side of the gangway passed as "arrived" and the character
    /// stayed stuck on the ship. The jump check is needed alongside it: a spot
    /// no path reaches gets the route [start, spot], whose distance reads zero.
    /// </summary>
    private static bool RouteReachesSpot(IReadOnlyList<Vector3>? route, Vector3 spot, Vector3 from)
        => route is { Count: >= 2 }
           && Vector3.Distance(route[^2], spot) <= ApproachSnapTolerance
           && !RouteHasImpossibleJump(route, from, out _, out _);

    /// <summary>
    /// Reports whether the destination can be walked to at all, and if not,
    /// which reachable spot comes closest to it. Nothing moves - this only
    /// asks the navigation mesh.
    /// </summary>
    /// <param name="quiet">Suppresses the running commentary. Set when the
    /// search runs automatically as the auto-walk's last resort rather than on
    /// the player's own "/acc zugang": there they asked a question and want the
    /// answer, here they asked to be walked somewhere and want to be walked.
    /// The one thing it still says is that nothing was found - that ends the
    /// walk, and silence would be indistinguishable from success.</param>
    /// <param name="noPathHint">Appended when the search comes up empty, so the
    /// auto-walk's practical advice (a nearby aetheryte to teleport to) survives
    /// the detour through here instead of being replaced by a bare refusal.</param>
    public void AnnounceApproach(Vector3 goal, string name, bool quiet = false,
                                 string noPathHint = "")
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;
        var me = player.Position;

        // try-catch: IPC into a foreign plugin (see Toggle).
        try
        {
            if (!_navIsReady.InvokeFunc())
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.MeshNotReady);
                return;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Zugang] Nav.IsReady-IPC fehlgeschlagen");
            _tolk.SpeakInterrupt(AccessibilityStrings.NoNavmeshPlugin);
            return;
        }

        if (!quiet) _tolk.SpeakInterrupt(AccessibilityStrings.ApproachChecking(name));
        _log.Info($"[Zugang] Suche Zugang zu '{name}' <{goal.X:F1}, {goal.Y:F1}, {goal.Z:F1}>, " +
                  $"ich <{me.X:F1}, {me.Y:F1}, {me.Z:F1}>");

        // Candidate spots on rings around the DESTINATION - a way in has to be
        // somewhere near it. Each bearing is probed at several heights between
        // our own and the destination's, because the snap always returns the
        // nearest floor: probing from our feet alone would only ever find our
        // own storey and miss the landing halfway up.
        var candidates = new List<Vector3>();
        foreach (var radius in ApproachRadii)
            for (var i = 0; i < ApproachBearings; i++)
            {
                var angle = i * 2f * MathF.PI / ApproachBearings;
                var x = goal.X + MathF.Sin(angle) * radius;
                var z = goal.Z + MathF.Cos(angle) * radius;
                for (var level = 0; level < ApproachLevels; level++)
                {
                    var y = me.Y + (goal.Y - me.Y) * level / (ApproachLevels - 1f);
                    // Reachable-only: a candidate on a patch that is cut off from
                    // the zone's main surface can never be a way in, and letting
                    // it into the list costs one full pathfind to reject it.
                    var floor = SnapToReachableMesh(new Vector3(x, y, z));
                    if (floor == null) continue;
                    // Drop near-duplicates: neighbouring rings and levels often
                    // snap onto the same patch, and every extra spot costs one
                    // path query.
                    if (candidates.Any(c => Vector3.Distance(c, floor.Value) < 2.5f)) continue;
                    candidates.Add(floor.Value);
                }
            }

        _log.Info($"[Zugang] {candidates.Count} Kandidatenpunkte auf dem Wegenetz.");

        // Fire the goal query plus one per candidate, then evaluate - each
        // pathfind is async inside vnavmesh and must not block the game thread.
        var goalTask = SafePathfind(me, goal);
        var probes = candidates.Select(c => (Pos: c, Task: SafePathfind(me, c))).ToList();

        Task.Run(async () =>
        {
            var goalRoute = await AwaitRoute(goalTask).ConfigureAwait(false);
            if (RouteIsWalkable(goalRoute, me, goal, 0f, $"Ziel '{name}'"))
            {
                var laenge = RouteLength(me, goalRoute!);
                _log.Info($"[Zugang] '{name}' IST erreichbar: {goalRoute!.Count} Wegpunkte, {laenge:F0} m Weg.");
                if (quiet)
                {
                    // The walk that sent us here judged the destination
                    // unreachable; if the fresh query disagrees, just walk it.
                    _pendingApproachWalk = (goal, name);
                    return;
                }
                _tolk.SpeakInterrupt(AccessibilityStrings.ApproachReachable(name, laenge));
                return;
            }

            var ende = goalRoute is { Count: >= 2 } ? goalRoute[^2] : me;
            _log.Info($"[Zugang] '{name}' ist NICHT erreichbar - der echte Pfad endet bei " +
                      $"<{ende.X:F1}, {ende.Y:F1}, {ende.Z:F1}>, {Vector3.Distance(ende, goal):F1} m vor dem Ziel. " +
                      $"Das Ziel haengt an einer anderen Flaeche des Wegenetzes.");

            // Among the spots we can actually walk to, the one closest to the
            // destination marks where the way in must be.
            // No element may be called "Rest" here - that name is reserved on
            // tuples (ValueTuple.Rest) and does not compile.
            (Vector3 Pos, float ToGoal, float Walk, float Score, float Slack)? best = null;
            var erreichbar = 0;
            foreach (var (pos, task) in probes)
            {
                var route = await AwaitRoute(task).ConfigureAwait(false);
                // Kept for the diagnostic line below - the check itself lives in
                // RouteReachesSpot, so candidates and crossing points are judged
                // by exactly the same rule.
                var rest = route is { Count: >= 2 }
                    ? Vector3.Distance(route[^2], pos)
                    : float.MaxValue;
                if (!RouteReachesSpot(route, pos, me)) continue;
                erreichbar++;
                var flach = MathF.Sqrt((pos.X - goal.X) * (pos.X - goal.X)
                                     + (pos.Z - goal.Z) * (pos.Z - goal.Z));
                var score = flach + ApproachHeightWeight * MathF.Abs(goal.Y - pos.Y);
                if (best == null || score < best.Value.Score)
                    best = (pos, Vector3.Distance(pos, goal), RouteLength(me, route!), score, rest);
            }
            _log.Info($"[Zugang] {erreichbar} von {probes.Count} Kandidaten sind erreichbar.");

            if (best == null)
            {
                _log.Info("[Zugang] Kein einziger Kandidat war erreichbar - der Zugang liegt weiter weg als 30 m.");
                // The hint already reads as its own sentence with a leading
                // space (see NoPathAetheryteHint), so both languages carry over
                // unchanged - no new string needed for the combination.
                _tolk.SpeakInterrupt(AccessibilityStrings.ApproachNone(name) + noPathHint);
                return;
            }

            var b = best.Value;
            var compass = RouteService.CompassWord(me, b.Pos);
            _log.Info($"[Zugang] Naechster erreichbarer Punkt: <{b.Pos.X:F1}, {b.Pos.Y:F1}, {b.Pos.Z:F1}>, " +
                      $"{b.Walk:F0} m Weg nach {compass}, von dort noch {b.ToGoal:F1} m zum Ziel " +
                      $"(Hoehenunterschied {goal.Y - b.Pos.Y:F1} m). " +
                      $"Route endet {b.Slack:F2} m neben dem Punkt - je naeher an 0, desto sicherer " +
                      $"ist er wirklich erreichbar. Laufe hin.");
            if (!quiet)
                _tolk.SpeakInterrupt(AccessibilityStrings.ApproachFound(
                    name, b.Walk, compass, b.ToGoal, goal.Y - b.Pos.Y));

            // Hand the walk to the game thread: we are on a worker thread here
            // (the path queries are async), and both the object table and the
            // vnavmesh IPC must only be touched from the framework tick.
            _pendingApproachWalk = (b.Pos, AccessibilityStrings.ApproachSpotName(name));
        });
    }

    /// <summary>
    /// Ends a redirected walk at the spot where the mesh runs out: drives the
    /// last few metres without pathfinding when the ground carries, and says
    /// what is left when it does not.
    ///
    /// Driving blind is only safe because <see cref="GroundIsContinuous"/> has
    /// just confirmed there is ground the whole way - the mesh is missing a
    /// polygon LINK there, not the floor itself. Without that check this would
    /// steer the character over the edge of whatever the path ended at.
    /// </summary>
    private void FinishNearMiss(Vector3 here, (Vector3 Goal, string Name) near)
    {
        var rest = Vector3.Distance(here, near.Goal);
        var compass = RouteService.CompassWord(here, near.Goal);

        if (GroundIsContinuous(here, near.Goal))
        {
            // Silent on purpose (user 2026-08-07: "die meldung brauche ich
            // nicht"). The walk simply carries on to its end - what the player
            // hears is the arrival, not a running commentary on the mechanics.
            _log.Info($"[Nav] Letztes Stueck: fahre die restlichen {rest:F1} m nach {compass} " +
                      $"ohne Wegsuche zu '{near.Name}' <{near.Goal.X:F1}, {near.Goal.Y:F1}, {near.Goal.Z:F1}>.");
            _finalHop = (near.Goal, near.Name, DateTime.UtcNow);
            MoveWithoutPathfinding(new List<Vector3> { near.Goal });
            return;
        }

        // Not silent: the walk ends here without having arrived, and silence is
        // the one thing a blind player cannot tell apart from success. The plain
        // "ended, N metres left" line every cut-short walk already uses.
        _log.Info($"[Nav] Letztes Stueck NICHT gefahren - auf der Strecke fehlt Boden. " +
                  $"Noch {rest:F1} m nach {compass} bis '{near.Name}'.");
        _tolk.SpeakInterrupt(AccessibilityStrings.AutoWalkEndedRemaining(rest));
    }

    /// <summary>
    /// Whether the straight line between two points runs over ground the mesh
    /// covers, sampled every <see cref="FinalHopProbeStep"/> metres. Answers the
    /// one question that makes driving without pathfinding safe: is the floor
    /// there? A missing sample means a drop, water or thin air; a sample the
    /// character cannot climb means a wall. Either way we do not drive.
    /// </summary>
    private bool GroundIsContinuous(Vector3 from, Vector3 to)
    {
        var total = Vector3.Distance(from, to);
        if (total < FinalHopProbeStep) return true;

        var steps = (int)MathF.Ceiling(total / FinalHopProbeStep);
        var prev = from;
        for (var i = 1; i <= steps; i++)
        {
            var probe = Vector3.Lerp(from, to, (float)i / steps);
            var ground = NearestMeshPoint(probe, FinalHopProbeXZ, FinalHopProbeY);
            if (ground == null)
            {
                _log.Info($"[Nav] Letztes Stueck: bei <{probe.X:F1}, {probe.Y:F1}, {probe.Z:F1}> " +
                          $"({i * FinalHopProbeStep:F0} m) liegt kein Netz im Umkreis von " +
                          $"{FinalHopProbeXZ:F0} m - dort ist kein Boden.");
                return false;
            }

            // Same rule the route check uses (RouteHasImpossibleJump): a rise
            // this steep is a wall, not a step.
            var dxz = MathF.Sqrt((ground.Value.X - prev.X) * (ground.Value.X - prev.X)
                               + (ground.Value.Z - prev.Z) * (ground.Value.Z - prev.Z));
            var dy = ground.Value.Y - prev.Y;
            if (dy >= ImpossibleRise && dy > dxz * ImpossibleSlope)
            {
                _log.Info($"[Nav] Letztes Stueck: Stufe von {dy:F1} m auf {dxz:F1} m Boden bei " +
                          $"<{ground.Value.X:F1}, {ground.Value.Y:F1}, {ground.Value.Z:F1}> - das ist eine Wand.");
                return false;
            }
            prev = ground.Value;
        }
        return true;
    }

    /// <summary>Watches a running final hop: Path.MoveTo reports nothing back,
    /// so arrival, the zone change a transition triggers, and the character
    /// getting stuck all have to be spotted from the outside.</summary>
    private void FinalHopUpdate()
    {
        if (_finalHop is not { } hop) return;

        // Zone changed: this is what walking into a transition is FOR, and it is
        // the only "arrived" a cross-zone walk ever gets.
        if ((ushort)_clientState.TerritoryType != _startTerritory)
        {
            _finalHop = null;
            _log.Info($"[Nav] Letztes Stueck: Gebiet gewechselt ({_startTerritory} -> " +
                      $"{_clientState.TerritoryType}), '{hop.Name}' erreicht.");
            _tolk.SpeakInterrupt(AccessibilityStrings.ArrivedNewZone);
            return;
        }

        var player = _objectTable.LocalPlayer;
        // Gone from the object table - a loading screen, or we logged out. The
        // zone check above catches the transition on the next tick.
        if (player == null) return;

        var rest = Vector3.Distance(player.Position, hop.Goal);
        if (rest <= FinalHopArrival)
        {
            _finalHop = null;
            StopPathQuiet();
            _log.Info($"[Nav] Letztes Stueck: angekommen, {rest:F1} m von '{hop.Name}'.");
            _tolk.SpeakInterrupt(AccessibilityStrings.TargetReached(hop.Name));
            return;
        }

        if ((DateTime.UtcNow - hop.StartedAt).TotalSeconds > FinalHopTimeoutS)
        {
            _finalHop = null;
            StopPathQuiet();
            var compass = RouteService.CompassWord(player.Position, hop.Goal);
            _log.Info($"[Nav] Letztes Stueck: nach {FinalHopTimeoutS:F0} s aufgegeben, " +
                      $"noch {rest:F1} m nach {compass} bis '{hop.Name}'.");
            _tolk.SpeakInterrupt(AccessibilityStrings.AutoWalkEndedRemaining(rest));
        }
    }

    /// <summary>Ends a running final hop on the player's command. From the
    /// player's side that hop is just the tail end of their walk, so the key
    /// that stops a walk has to stop this too. True when there was one.</summary>
    private bool StopFinalHopIfRunning()
    {
        if (_finalHop == null) return false;
        _finalHop = null;
        StopPathQuiet();
        _log.Info("[Nav] Letztes Stueck: vom Spieler abgebrochen.");
        _tolk.SpeakInterrupt(AccessibilityStrings.AutoWalkStopped);
        return true;
    }

    /// <summary>Stops whatever vnavmesh is steering, without touching our own
    /// walk state - the final hop runs outside it.</summary>
    private void StopPathQuiet()
    {
        // try-catch: IPC into a foreign plugin (see Toggle).
        try { _pathStop.InvokeAction(); }
        catch (Exception ex) { _log.Error(ex, "[Nav] Letztes Stueck: Path.Stop fehlgeschlagen"); }
    }

    /// <summary>Approach check for the current game target.</summary>
    public void AnnounceApproachToTarget()
    {
        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.ApproachNoTarget);
            return;
        }
        AnnounceApproach(target.Position, _objectNames.Describe(target));
    }

    /// <summary>Snaps a probe position onto the walkable mesh, or null if there
    /// is no mesh near it. The vertical extent is generous (one storey up and
    /// down) so a spot on a raised walkway is still found.</summary>
    private Vector3? SnapToMesh(Vector3 probe)
    {
        // try-catch: IPC into a foreign plugin (see Toggle).
        try { return _nearestPoint.InvokeFunc(probe, 3f, 15f); }
        catch (Exception ex) { _log.Error(ex, "[Zugang] NearestPoint-IPC fehlgeschlagen"); return null; }
    }

    /// <summary>
    /// Like <see cref="SnapToMesh"/>, but skips mesh patches vnavmesh has marked
    /// as cut off from the zone's main area.
    ///
    /// WHAT "REACHABLE" MEANS HERE, decompiled 2026-08-08 rather than assumed -
    /// it is NOT "reachable from where I stand". The gate passes
    /// <c>allowUnreachable: false</c>, which swaps the polygon filter for
    /// <c>FloodFillAwareFilter</c>, and that one rejects polygons carrying flag
    /// 0x10. The flag is set once per zone by <c>NavmeshManager.Prune</c>: it
    /// flood-fills from seed points and marks everything it cannot reach. So the
    /// property is "hangs together with the zone's main surface" - a
    /// pre-computed fact about the map, not about the player.
    ///
    /// That is exactly the right filter for approach candidates. The ring search
    /// snaps probes onto whatever mesh is nearest, and a detached island (the
    /// Astalicia case) answers just as readily as real ground - each such
    /// candidate then costs a full pathfind to be ruled out. Here they never
    /// enter the list.
    ///
    /// HONEST LIMIT: <c>Prune</c> only runs where <c>FloodFill.TryLookup</c> has
    /// seed points for the territory. Without them nothing is flagged and this
    /// behaves exactly like <see cref="SnapToMesh"/> - no worse, no better.
    /// </summary>
    private Vector3? SnapToReachableMesh(Vector3 probe)
    {
        // try-catch: IPC into a foreign plugin (see Toggle). A vnavmesh without
        // this gate throws on INVOKE, so the fallback keeps the approach search
        // working on older builds instead of finding no candidates at all.
        try { return _nearestPointReachable.InvokeFunc(probe, 3f, 15f); }
        catch (Exception ex)
        {
            _log.Warning($"[Zugang] NearestPointReachable nicht verfuegbar ({ex.Message}) - " +
                         "nutze NearestPoint ohne Erreichbarkeits-Filter.");
            return SnapToMesh(probe);
        }
    }

    /// <summary>Awaits a pathfind task, swallowing the faults vnavmesh throws
    /// when the mesh unloads mid-query (zone change).</summary>
    private async Task<List<Vector3>?> AwaitRoute(Task<List<Vector3>>? task)
    {
        if (task == null) return null;
        try { return await task.ConfigureAwait(false); }
        catch (Exception ex) { _log.Warning($"[Zugang] Pfadsuche fehlgeschlagen: {ex.Message}"); return null; }
    }

    /// <summary>Horizontal length of a route, starting from our own position.</summary>
    private static float RouteLength(Vector3 from, IReadOnlyList<Vector3> route)
    {
        var total = 0f;
        var prev = from;
        foreach (var wp in route)
        {
            total += MathF.Sqrt((wp.X - prev.X) * (wp.X - prev.X) + (wp.Z - prev.Z) * (wp.Z - prev.Z));
            prev = wp;
        }
        return total;
    }

    /// <summary>Pathfind via IPC, null when vnavmesh refuses the call.</summary>
    private Task<List<Vector3>>? SafePathfind(Vector3 from, Vector3 to)
    {
        // try-catch: IPC into a foreign plugin (see Toggle).
        try { return _navPathfind.InvokeFunc(from, to, false); }
        catch (Exception ex) { _log.Error(ex, "[Zugang] Nav.Pathfind-IPC fehlgeschlagen"); return null; }
    }

    // â”€â”€ Wegenetz-Aufbau mitverfolgen â”€â”€

    private float _lastMeshProgress = -1f;   // -1 = kein Aufbau lÃ¤uft
    private int _lastSpokenMeshStep = -1;    // last announced 20 % step (0..4)
    private bool _meshMonitorOff;            // IPC unavailable - reported once, then quiet

    /// <summary>
    /// Announces the navmesh build in 20 % steps and reports when it is done
    /// (user request 2026-07-18). Without it the player has no way to tell
    /// "still loading" from "broken" - the auto-walk simply refuses to start.
    ///
    /// vnavmesh semantics verified by decompiling NavmeshManager (2026-07-18):
    /// LoadTaskProgress is -1 while no build runs, set to 0 when one starts,
    /// grows to 1 via BuildTiles, and is reset to -1 in an OnDispose when the
    /// task ends - so completion shows up as the drop back to -1, and only
    /// Nav.IsReady tells success (mesh present) from cancellation.
    /// Loads served from the tile cache can finish so fast that no intermediate
    /// step is ever seen; then only start and finish speak. That is correct.
    /// </summary>
    private void MonitorMeshBuild()
    {
        if (_meshMonitorOff || !_config.AnnounceMeshProgress) return;

        float progress;
        bool ready;
        // try-catch: IPC into a foreign plugin (vnavmesh may be missing).
        try
        {
            progress = _navBuildProgress.InvokeFunc();
            ready    = _navIsReady.InvokeFunc();
        }
        catch (Exception ex)
        {
            // Never per frame: report once, then stay off. A missing vnavmesh is
            // already announced properly when a walk is actually attempted.
            _meshMonitorOff = true;
            _log.Warning(ex, "[Nav] Wegenetz-Fortschritt nicht lesbar - Ãœberwachung aus.");
            return;
        }

        var wasBuilding = _lastMeshProgress >= 0f;
        var isBuilding  = progress >= 0f;
        _lastMeshProgress = progress;

        if (isBuilding)
        {
            if (!wasBuilding)
            {
                // Step 0 counts as spoken, so 0 % never announces itself.
                _lastSpokenMeshStep = 0;
                _log.Info("[Nav] Wegenetz-Aufbau gestartet.");
                _tolk.SpeakInterrupt(AccessibilityStrings.MeshLoading);
                return;
            }

            var step = (int)(progress * 5); // 0..4 = 0/20/40/60/80 %
            if (step > _lastSpokenMeshStep && step < 5)
            {
                _lastSpokenMeshStep = step;
                _log.Info($"[Nav] Wegenetz-Aufbau: {step * 20} % (progress={progress:F2})");
                _tolk.SpeakInterrupt(AccessibilityStrings.MeshPercent(step * 20));
            }
            return;
        }

        if (!wasBuilding) return;

        _lastSpokenMeshStep = -1;
        _log.Info($"[Nav] Wegenetz-Aufbau beendet, bereit={ready}");
        _tolk.SpeakInterrupt(ready
            ? AccessibilityStrings.MeshReady
            : AccessibilityStrings.MeshAborted);
    }

    /// <summary>Queues the vnavmesh path. False (with announcement) when vnavmesh is not ready.</summary>
    private bool TryStartPath(Vector3 destination, float stopRange)
    {
        // try-catch: IPC into a foreign plugin - vnavmesh may be missing,
        // disabled or still loading (IpcNotReadyError).
        try
        {
            if (!_navIsReady.InvokeFunc())
            {
                var progress = _navBuildProgress.InvokeFunc();
                _tolk.SpeakInterrupt(progress >= 0
                    ? AccessibilityStrings.MeshStillLoading(progress * 100)
                    : AccessibilityStrings.MeshNotReady);
                return false;
            }

            if (!_moveCloseTo.InvokeFunc(destination, false, stopRange))
            {
                // MoveTo returns false only while a previous pathfind is queued
                _tolk.SpeakInterrupt(AccessibilityStrings.PathfindBusy);
                return false;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Auto-Lauf: vnavmesh-IPC fehlgeschlagen (Plugin installiert?)");
            _tolk.SpeakInterrupt(AccessibilityStrings.AutoWalkUnavailable);
            return false;
        }
        return true;
    }

    private void BeginWalk()
    {
        _active = true;
        // Every walk starts out as its own origin; the redirects in Update put
        // the player's original destination back afterwards, so a chain of
        // restarts keeps pointing at the place that was actually asked for.
        _walkOrigin = (_destPosition, _targetName);
        _approachTried = false;
        // Belongs to the walk that is ending, not to this one. The redirect sets
        // it again right after starting its walk - a walk that ends short of its
        // spot would otherwise leave it behind for the next, unrelated one.
        _nearMissGoal = null;
        _finalHop = null;
        _sawRunning = false;
        _startedAt = DateTime.UtcNow;
        _startTerritory = (ushort)_clientState.TerritoryType;
        _lastPosition = _objectTable.LocalPlayer?.Position ?? default;
        _lastMoveAt = DateTime.UtcNow;
        // Baseline for the progress lines: the first one is due once we are a
        // full step closer than the start, so short walks never speak at all.
        _lastProgressDistance = Vector3.Distance(_lastPosition, _destPosition);
        _diagLoggedPath = false;
        _lastDiagAt = DateTime.UtcNow;
        _log.Info($"[Nav] Auto-Lauf: gestartet zu {_targetName} (id={_targetId:X}, stopRange={_stopRange:F1}, " +
                  $"dist={Vector3.Distance(_objectTable.LocalPlayer?.Position ?? default, _destPosition):F1})");
        _tolk.SpeakInterrupt(AccessibilityStrings.WalkingTo(_targetName));
    }

    /// <summary>Stops a running auto-walk without any announcement (e.g. when the walk guide takes over).</summary>
    public void StopQuiet()
    {
        if (_active) Stop(announce: false);
    }

    private void Stop(bool announce)
    {
        _active = false;
        // Both end with the walk they belong to. The two places that DO announce
        // the remaining distance read _nearMissGoal before they call this, and
        // the final hop is started afterwards, never before.
        _nearMissGoal = null;
        _finalHop = null;

        // try-catch: IPC into a foreign plugin (see Toggle)
        try
        {
            _pathStop.InvokeAction();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Auto-Lauf: Path.Stop fehlgeschlagen");
        }

        _log.Info("[Nav] Auto-Lauf: gestoppt.");
        if (announce) _tolk.SpeakInterrupt(AccessibilityStrings.AutoWalkStopped);
    }

    /// <summary>Watches the running walk. Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
    {
        // Runs even when no walk is active: the player wants to know when the
        // navmesh finishes loading, precisely BECAUSE they cannot walk yet.
        MonitorMeshBuild();

        // The approach search runs off-thread and cannot start a walk itself;
        // it parks the spot here and we set off on the next tick. Must happen
        // before the _active check - at this moment no walk is running.
        if (_pendingApproachWalk is { } approach)
        {
            _pendingApproachWalk = null;
            var approachOrigin = _walkOrigin;
            if (_active) Stop(announce: false);
            // The player asked for this walk, so it wins over a final hop that
            // may still be running - dropped here rather than letting
            // ToggleToPosition read it as "stop what you are doing".
            _finalHop = null;
            ToggleToPosition(approach.Pos, approach.Name, ApproachStopRange);
            if (_active)
            {
                if (approachOrigin != null) _walkOrigin = approachOrigin;
                // This walk IS the search's answer. Should it fail too, there is
                // nothing left to search for - starting the search again would
                // only walk to the next-best spot around the same destination,
                // over and over.
                _approachTried = true;
            }
        }

        // The route check found a path that ends just short of the destination
        // and parked the redirect here (same reason as above: it runs inside the
        // waypoint evaluation of the walk we are replacing).
        if (_pendingNearMissWalk is { } nearMiss)
        {
            _pendingNearMissWalk = null;
            var nearMissOrigin = _walkOrigin;
            if (_active) Stop(announce: false);
            // Keeps the destination's own name: we ARE heading there, just not
            // the whole way. ToggleToPosition clears _nearMissGoal on the way in,
            // so it is set afterwards - and only if the path actually started.
            ToggleToPosition(nearMiss.Spot, nearMiss.Name, ApproachStopRange);
            // No announcement for the redirect itself: from the player's side
            // this is still the walk they started, and the mechanics behind it
            // are not news they asked for (user 2026-08-07).
            // Wrong-storey redirects need no follow-up at all - the spot IS the
            // destination, so the normal "arrived" is the truth.
            if (_active && !nearMiss.SpotIsGoal) _nearMissGoal = (nearMiss.Goal, nearMiss.Name);
            // The corrected point is a means, not the destination: if the walk
            // to it finds no route, the mesh search must still look around the
            // place the player named. Measured 2026-08-08 (Haukke-Herrenhaus):
            // without this the chain ended at the intermediate point and said
            // "no path", 14 m from a route the mesh had.
            if (_active && nearMissOrigin != null) _walkOrigin = nearMissOrigin;
        }

#if DEBUG
        // The side check ran on a worker and parked its result here. Must be
        // handled BEFORE the crossing block below: starting the walk sets
        // _active, which is what keeps that block from firing in the same frame.
        if (_pendingPlankRun is { } plank)
        {
            _pendingPlankRun = null;
            if (_active) Stop(announce: false);
            _pendingGapCross = plank.Cross;
            _gapFrom = plank.To;
            ToggleToPosition(plank.To, AccessibilityStrings.GapCrossSpotName, ApproachStopRange);
        }

        // The walk to the near side of the gap has ended - now cross it without
        // the pathfinder. Only if we actually got there: after a cancelled walk
        // we would otherwise steer off from wherever the player stopped.
        if (_pendingGapCross is { } gap && !_active)
        {
            _pendingGapCross = null;
            var here = _objectTable.LocalPlayer?.Position;
            var offset = here == null ? float.MaxValue : Vector3.Distance(here.Value, _gapFrom);
            if (offset <= GapCrossMaxOffset)
            {
                _log.Info($"[Planke] Etappe 2: an der Uebergangsstelle ({offset:F1} m daneben). " +
                          $"Fahre ohne Wegsuche: {string.Join(" -> ", gap.Select(p => $"({p.X:F1}|{p.Y:F1}|{p.Z:F1})"))}");
                _tolk.SpeakInterrupt(AccessibilityStrings.GapCrossing);
                MoveWithoutPathfinding(gap);
            }
            else
            {
                _log.Info($"[Planke] Etappe 2 verworfen: {offset:F1} m von der Uebergangsstelle entfernt " +
                          $"(erlaubt {GapCrossMaxOffset:F1} m) - der Lauf dorthin kam nicht an.");
                _tolk.SpeakInterrupt(AccessibilityStrings.GapCrossTooFar);
            }
        }
#endif

        // The final hop runs AFTER the walk has ended (_active is already false),
        // so it is watched before the active-walk checks below.
        if (_finalHop != null) { FinalHopUpdate(); return; }

        // Follow is a separate, continuously re-pathing mode; it never overlaps
        // the one-shot walk (each cancels the other on start).
        if (_following) { FollowUpdate(); return; }

        if (!_active) return;

        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            // Logout/zone change - vnavmesh drops the path itself, we just clean up.
            Stop(announce: false);
            return;
        }

        // Zone transition succeeded: walking to a transition put the player into
        // a new area. This is the real "arrived" signal for cross-zone walks -
        // vnavmesh's own path never reports it (the destination is on the far
        // side of the zone line).
        if ((ushort)_clientState.TerritoryType != _startTerritory)
        {
            _active = false;
            _log.Info($"[Nav] Auto-Lauf: Gebiet gewechselt ({_startTerritory} -> {_clientState.TerritoryType}), Ziel erreicht.");
            _tolk.SpeakInterrupt(AccessibilityStrings.ArrivedNewZone);
            return;
        }

        bool running;
        bool computing;
        // try-catch: IPC into a foreign plugin (see Toggle)
        try
        {
            running = _pathIsRunning.InvokeFunc();
            computing = _pathfindInProgress.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Auto-Lauf: Status-IPC fehlgeschlagen, breche ab");
            Stop(announce: false);
            _tolk.SpeakInterrupt(AccessibilityStrings.AutoWalkAbortedNoResponse);
            return;
        }

        if (running) _sawRunning = true;

        // Moving objects (NPCs) update their destination; quest markers are fixed.
        if (_targetId != 0)
        {
            var obj = _objectTable.FirstOrDefault(o => o.GameObjectId == _targetId);
            if (obj != null) _destPosition = obj.Position;
        }

        var distance = Vector3.Distance(player.Position, _destPosition);
        var now = DateTime.UtcNow;

        // Stall detection watches the character's OWN movement, not the distance
        // to the destination: detours legitimately move away from the target for
        // a while (false abort right after start, log 2026-07-13 01:08). Jitter
        // while pushed against geometry stays under the 0.5 m threshold.
        if (Vector3.Distance(player.Position, _lastPosition) >= 0.5f)
        {
            _lastPosition = player.Position;
            _lastMoveAt = now;
        }

        // Reads the path vnavmesh is actually following (Path.ListWaypoints):
        //  - once, when the route first appears: speak the route preview (user
        //    request 2026-07-15: announce via which waypoints the destination
        //    is reached) and log the full waypoint list plus how far its LAST
        //    point sits from our target. Last-point-near-target => destination
        //    reachable, the char jams on collision; last point far short =>
        //    the mesh has no route there (a gap / wrong target).
        //  - every second (diagnostic): live position, remaining waypoint
        //    count and the distance to the next waypoint.
        // try-catch: IPC into a foreign plugin (see Toggle).
        try
        {
            var waypoints = _pathListWaypoints.InvokeFunc();
            if (!_diagLoggedPath && waypoints is { Count: > 0 })
            {
                _diagLoggedPath = true;
                var last = waypoints[^1];
                var route = string.Join(" -> ", waypoints.Select(w => $"({w.X:F1}|{w.Y:F1}|{w.Z:F1})"));
                _log.Info($"[NavDiag] Pfad: {waypoints.Count} Wegpunkte, letzter->Ziel={Vector3.Distance(last, _destPosition):F1} m. Route: {route}");

                // The last waypoint is always the destination itself - vnavmesh
                // appends it even when the search never got there (see
                // RouteReachesGoal). So judge by the SECOND TO LAST one: if the
                // real path ends far short, the destination hangs on a separate
                // patch of mesh and walking is pointless. Say so instead of
                // setting off and going quiet - a blind player cannot see the
                // character standing still against a wall.
                if (!RouteIsWalkable(waypoints, player.Position, _destPosition, _stopRange,
                                     $"Auto-Lauf zu '{_targetName}'"))
                {
                    var realEnd = waypoints.Count >= 2 ? waypoints[^2] : player.Position;
                    var gapFlat = MathF.Sqrt(
                        (_destPosition.X - realEnd.X) * (_destPosition.X - realEnd.X) +
                        (_destPosition.Z - realEnd.Z) * (_destPosition.Z - realEnd.Z));
                    var gapRise = _destPosition.Y - realEnd.Y;
                    _log.Info($"[Nav] Auto-Lauf: '{_targetName}' haengt an einer anderen Flaeche. " +
                              $"Echter Pfad endet bei <{realEnd.X:F1}, {realEnd.Y:F1}, {realEnd.Z:F1}>, " +
                              $"waagerecht {gapFlat:F1} m, Hoehe {gapRise:F1} m vor dem Ziel " +
                              $"(durchgehender Weg waere bis {UnreachableGap + _stopRange:F1} m, " +
                              $"Zielhoehe {(_destHeightIsGuess ? "geraten" : "bekannt")}).");

                    // Case 1 - wrong storey. The path ends right above or below
                    // the destination, and the destination's height was only a
                    // guess: then the mesh knows better than the guess and the
                    // path's end IS the place. Measured 2026-08-07 (aetheryte
                    // Herbstkürbis-See): mesh at Y -49 and Y -39 over the same
                    // spot, the guess took -49 from the player's own height,
                    // and only -39 was reachable - the path ended there, 2,7 m
                    // away. Requires the guessed height: with a KNOWN one a
                    // vertical gap is real (Astalicia, ship's deck 9,1 m up).
                    if (_destHeightIsGuess && gapFlat <= NearMissGap && MathF.Abs(gapRise) >= ImpossibleRise)
                    {
                        // Aim at the marker's own X/Z with the height the mesh
                        // just proved walkable - closer to the place the player
                        // asked for than the path's end beside it.
                        //
                        // That point is ARITHMETIC, not a mesh point: marker X/Z
                        // combined with a height measured somewhere else. Nothing
                        // says there is floor there, and a destination off the
                        // mesh yields no route at all. Measured 2026-08-08
                        // (Haukke-Herrenhaus): the corrected point returned zero
                        // waypoints and the walk gave up 14,6 m from a path the
                        // mesh had. So snap it onto the mesh first, and keep the
                        // storey while doing it - the whole point of the redirect
                        // is the height, so the vertical box stays at the step we
                        // already treat as one storey.
                        var computed = _destPosition with { Y = realEnd.Y };
                        var corrected = NearestMeshPoint(computed, WrongStoreySnapXZ, ImpossibleRise);
                        if (corrected == null)
                        {
                            _log.Info($"[Nav] Auto-Lauf: der korrigierte Punkt <{computed.X:F1}, " +
                                      $"{computed.Y:F1}, {computed.Z:F1}> liegt nicht auf dem Wegenetz " +
                                      $"({WrongStoreySnapXZ:F0} m waagerecht, {ImpossibleRise:F0} m hoch " +
                                      $"abgesucht) - dorthin umzuleiten ergaebe keine Route. Suche " +
                                      $"stattdessen den naechsten erreichbaren Punkt.");
                            var storeyGoal = _walkOrigin?.Goal ?? _destPosition;
                            var storeyName = _walkOrigin?.Name ?? _targetName;
                            var storeyHint = _places.BuildNoPathHint(storeyGoal);
                            _approachTried = true;
                            Stop(announce: false);
                            AnnounceApproach(storeyGoal, storeyName, quiet: true, noPathHint: storeyHint);
                            return;
                        }
                        _log.Info($"[Nav] Auto-Lauf umgeleitet (falsche Etage): die geratene Zielhoehe lag " +
                                  $"{-gapRise:F1} m neben der begehbaren. Neues Ziel <{corrected.Value.X:F1}, " +
                                  $"{corrected.Value.Y:F1}, {corrected.Value.Z:F1}> (auf das Wegenetz gelegt, " +
                                  $"{Vector3.Distance(computed, corrected.Value):F1} m vom errechneten Punkt).");
                        _pendingNearMissWalk = (corrected.Value, corrected.Value, _targetName, SpotIsGoal: true);
                        Stop(announce: false);
                        return;
                    }

                    // Case 2 - the path stops just short on the same level: walk
                    // to its end and drive the rest (see NearMissGap).
                    if (gapFlat <= NearMissGap && MathF.Abs(gapRise) < ImpossibleRise)
                    {
                        _log.Info($"[Nav] Auto-Lauf umgeleitet: laufe bis zum Pfadende, " +
                                  $"die restlichen {gapFlat:F1} m werden ohne Wegsuche gefahren " +
                                  $"(Grenze {NearMissGap:F0} m waagerecht, {ImpossibleRise:F0} m Hoehe).");
                        _pendingNearMissWalk = (realEnd, _destPosition, _targetName, SpotIsGoal: false);
                        Stop(announce: false);
                        return;
                    }

                    // Case 3 - too far off to fix from the path alone. Rather
                    // than refuse (user 2026-08-07: "es sollen alle angelaufen
                    // werden können die das navmesh hat"), search the rings
                    // around the destination for the closest spot that IS
                    // reachable and walk there. Silent: it announces only if it
                    // finds nothing at all.
                    _log.Info($"[Nav] Auto-Lauf: kein Pfadende in Reichweite - suche automatisch den " +
                              $"naechsten erreichbaren Punkt um '{_targetName}'.");
                    // The player's own destination, not this leg's: after a
                    // redirect _destPosition is an intermediate point, and the
                    // way in belongs around the place that was asked for.
                    var unreachableGoal = _walkOrigin?.Goal ?? _destPosition;
                    var unreachableName = _walkOrigin?.Name ?? _targetName;
                    var unreachableHint = _places.BuildNoPathHint(unreachableGoal);
                    _approachTried = true;
                    Stop(announce: false);
                    AnnounceApproach(unreachableGoal, unreachableName, quiet: true,
                                     noPathHint: unreachableHint);
                    return;
                }
                // Queued (not interrupting) so it follows "Laufe zu ...". The
                // progress lines are distance-gated now, so nothing has to be
                // pushed back to protect the preview from being cut off.
                _tolk.Speak(_routes.DescribeRoute(_targetName, waypoints));
            }
            if ((now - _lastDiagAt).TotalSeconds >= 1)
            {
                _lastDiagAt = now;
                var p = player.Position;
                var remaining = waypoints?.Count ?? 0;
                var next = remaining > 0 ? waypoints![0] : default;
                var distNext = remaining > 0 ? Vector3.Distance(p, next) : -1f;
                _log.Info($"[NavDiag] pos=({p.X:F1}|{p.Y:F1}|{p.Z:F1}) distZiel={distance:F1} " +
                          $"restWp={remaining} nextWp=({next.X:F1}|{next.Y:F1}|{next.Z:F1}) distNextWp={distNext:F1}");
#if DEBUG
                // Direction probe (2026-08-01, user report "left is right"):
                // vnavmesh is actively steering the character towards nextWp, so
                // its own heading must read "straight ahead" there. That makes
                // this line ground truth for the direction formula - no game
                // target, no turning, and no judgement about one's own facing
                // needed. angleZiel is the straight line to the destination and
                // may legitimately differ around corners.
                if (remaining > 0)
                {
                    var angleWp = NavigationService.RelativeAngle(player, next);
                    var angleDest = NavigationService.RelativeAngle(player, _destPosition);
                    _log.Info($"[NavDirProbe] auto-walk: rot={player.Rotation:F3} " +
                              $"dxWp={next.X - p.X:F2} dzWp={next.Z - p.Z:F2} " +
                              $"angleWp={angleWp:F1} wortWp='{AccessibilityStrings.RelativeDirection(angleWp)}' " +
                              $"angleZiel={angleDest:F1} wortZiel='{AccessibilityStrings.RelativeDirection(angleDest)}'");
                }
#endif
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[NavDiag] Waypoint-IPC fehlgeschlagen");
        }

        // Spoken progress. Originally every 3 s, because the beacon tone alone
        // left the user unsure and cancelling walks (report 2026-07-11) - but on
        // a long walk that is a wall of "Noch X Meter" (report 2026-07-18).
        // Now tied to PROGRESS instead of the clock: one line per
        // ProgressStepMetres covered, so a short hop stays silent and a long run
        // reports a handful of times. The reassurance survives, the chatter does
        // not. ProgressStepMetres = 0 turns it off entirely.
        var step = _config.AutoWalkProgressStep;
        if (step > 0 && distance <= _lastProgressDistance - step)
        {
            _lastProgressDistance = distance;
            _tolk.SpeakInterrupt(AccessibilityStrings.StillToGo(distance));
            _log.Info($"[Nav] Auto-Lauf: lÃ¤uft, dist={distance:F1} running={running} computing={computing}");
        }

        if (_sawRunning && !running)
        {
            // Path finished. Arrived, or did vnavmesh stop early?
            _active = false;
            var arrived = distance <= _stopRange + 1.5f;
            // Split the remaining gap into horizontal and vertical: a path that
            // ends short is almost always one of two cases, and only these
            // numbers tell them apart - the target being walled off (horizontal
            // distance left) versus the target standing above or below us
            // (height left, which vnavmesh cannot climb). Cheap, and it turns a
            // "why does it stop?" report into a diagnosis.
            var me = player.Position;
            var dxz = MathF.Sqrt(
                (_destPosition.X - me.X) * (_destPosition.X - me.X) +
                (_destPosition.Z - me.Z) * (_destPosition.Z - me.Z));
            var dy = _destPosition.Y - me.Y;
            _log.Info($"[Nav] Auto-Lauf: Pfad beendet, dist={distance:F1}, angekommen={arrived}. " +
                      $"Rest waagerecht={dxz:F1} m, Hoehenunterschied={dy:F1} m " +
                      $"(Ziel {(dy > 0 ? "ueber" : "unter")} mir). " +
                      $"Ich <{me.X:F1}, {me.Y:F1}, {me.Z:F1}> " +
                      $"Ziel <{_destPosition.X:F1}, {_destPosition.Y:F1}, {_destPosition.Z:F1}>");
            // Redirected walk: we are standing where the mesh ends, not at the
            // destination. Saying "reached" here would be a lie the player cannot
            // check - name the remaining distance and direction instead.
            if (arrived && _nearMissGoal is { } near)
            {
                _nearMissGoal = null;
                _log.Info($"[Nav] Auto-Lauf: am Pfadende angekommen, " +
                          $"{Vector3.Distance(me, near.Goal):F1} m bis '{near.Name}'.");
                FinishNearMiss(me, near);
                return;
            }
            _tolk.SpeakInterrupt(arrived
                ? AccessibilityStrings.TargetReached(_targetName)
                : AccessibilityStrings.AutoWalkEndedRemaining(distance));
            return;
        }

        // Stall: vnavmesh keeps running but the character has not moved for 5 s
        // (wedged on geometry the mesh does not know, e.g. the zone-line spots
        // from 2026-07-12/13). Stop the path too - previously only our tracking
        // ended and vnavmesh kept pushing against the obstacle.
        if (_sawRunning && (now - _lastMoveAt).TotalSeconds > 5)
        {
            var arrived = distance <= _stopRange + 2f;
            _log.Info($"[Nav] Auto-Lauf: keine Bewegung seit 5 s, dist={distance:F1}, angekommen={arrived}");
            // Same case as on a clean finish, only the character came to rest a
            // moment earlier: read the redirect BEFORE Stop clears it.
            var stalledNear = arrived ? _nearMissGoal : null;
            var here = player.Position;
            Stop(announce: false);
            if (stalledNear is { } near2)
            {
                _log.Info($"[Nav] Auto-Lauf: am Pfadende zum Stehen gekommen, " +
                          $"{Vector3.Distance(here, near2.Goal):F1} m bis '{near2.Name}'.");
                FinishNearMiss(here, near2);
                return;
            }
            _tolk.SpeakInterrupt(arrived
                ? AccessibilityStrings.TargetReached(_targetName)
                : AccessibilityStrings.StuckRemaining(distance));
            return;
        }

        // Pathfind finished but produced no path (unreachable destination).
        // Grace period covers the frames between queueing and task start.
        if (!_sawRunning && !computing && (now - _startedAt).TotalSeconds > 1.5)
        {
            _active = false;
            _log.Info($"[Nav] Auto-Lauf: kein Weg zu {_targetName} (id={_targetId:X}) gefunden.");

            // No route does NOT mean no way there. It usually means the exact
            // point is off the mesh - a marker inside a wall, a spot on a ledge,
            // a corrected height that landed beside the floor - while the mesh
            // does know ground a few metres away. The same search the "path ends
            // short" case already uses answers that, so run it here too instead
            // of refusing (user 2026-08-07: "es sollen alle angelaufen werden
            // koennen die das navmesh hat"). Measured 2026-08-08
            // (Haukke-Herrenhaus): four attempts, each ending here, while the
            // mesh carried a 603 m route to within 14,6 m of the destination.
            //
            // Once per destination: the search ends in a walk of its own, and
            // letting that walk restart the search would loop.
            if (!_approachTried && _walkOrigin is { } origin)
            {
                _approachTried = true;
                _log.Info($"[Nav] Auto-Lauf: keine Route zu <{_destPosition.X:F1}, {_destPosition.Y:F1}, " +
                          $"{_destPosition.Z:F1}> - suche den naechsten erreichbaren Punkt um " +
                          $"'{origin.Name}' <{origin.Goal.X:F1}, {origin.Goal.Y:F1}, {origin.Goal.Z:F1}>.");
                AnnounceApproach(origin.Goal, origin.Name, quiet: true,
                                 noPathHint: _places.BuildNoPathHint(origin.Goal));
                return;
            }

            _tolk.SpeakInterrupt(AccessibilityStrings.NoPathTo(_targetName, _places.BuildNoPathHint(_destPosition)));
        }
    }

    public void Dispose()
    {
        StopFollowQuiet();
        StopQuiet();
    }
}
