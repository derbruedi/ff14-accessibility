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
    /// <summary>Stop this close to the destination, in yalms/meters (interaction range).</summary>
    private const float StopRange = 2.5f;

    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IClientState _clientState;
    private readonly TolkService _tolk;
    private readonly Configuration _config;
    private readonly PlacesService _places;
    private readonly RouteService _routes;
    private readonly IPluginLog _log;

    private readonly ICallGateSubscriber<bool> _navIsReady;
    private readonly ICallGateSubscriber<float> _navBuildProgress;
    private readonly ICallGateSubscriber<Vector3, bool, float, bool> _moveCloseTo;
    private readonly ICallGateSubscriber<object> _pathStop;
    private readonly ICallGateSubscriber<bool> _pathIsRunning;
    private readonly ICallGateSubscriber<bool> _pathfindInProgress;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> _pointOnFloor;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> _nearestPoint;
    // DIAGNOSTIC (temporary): the waypoints of the path vnavmesh is actually
    // following. Lets us tell whether the destination is reachable (last
    // waypoint sits on the target) or the route jams short of it. Verified
    // against vnavmesh IPCProvider: Path.ListWaypoints -> List<Vector3>.
    private readonly ICallGateSubscriber<List<Vector3>> _pathListWaypoints;

    private bool _active;
    private bool _sawRunning;          // the path actually started at least once

    /// <summary>Whether an auto-walk is currently running. Plugin.cs suppresses
    /// automatic target-change announcements while this is true - passing NPCs
    /// grab the soft target every few steps and each one would be announced
    /// with distance and direction (user feedback 2026-07-10).</summary>
    public bool IsActive => _active;
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
    private DateTime _lastProgressSpeak;
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
        IPluginLog log)
    {
        _objectTable = objectTable;
        _targetManager = targetManager;
        _clientState = clientState;
        _tolk = tolk;
        _config = config;
        _places = places;
        _routes = routes;
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
        _pathListWaypoints  = pluginInterface.GetIpcSubscriber<List<Vector3>>("vnavmesh.Path.ListWaypoints");
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
                _log.Info($"[Orte] NearestPoint (hohe Säule) ({approximate.X:F1}|{approximate.Y:F1}|{approximate.Z:F1}) -> " +
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

    /// <summary>Starts the auto-walk to the current game target, or stops a running one.</summary>
    public void Toggle()
    {
        if (_active)
        {
            Stop(announce: true);
            return;
        }

        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt("Kein Ziel. Erst mit N ein Objekt wählen.");
            return;
        }

        if (!TryStartPath(target.Position, StopRange)) return;

        _targetId = target.GameObjectId;
        _targetName = target.Name.TextValue;
        _destPosition = target.Position;
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
    public void ToggleToPosition(Vector3 position, string name, float stopRange)
    {
        if (_active)
        {
            Stop(announce: true);
            return;
        }

        if (!TryStartPath(position, stopRange)) return;

        _targetId = 0;
        _targetName = name;
        _destPosition = position;
        _stopRange = stopRange;
        BeginWalk();
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
                    ? $"Wegenetz lädt noch, {progress * 100:F0} Prozent. Gleich nochmal versuchen."
                    : "Wegenetz ist noch nicht bereit. Gleich nochmal versuchen.");
                return false;
            }

            if (!_moveCloseTo.InvokeFunc(destination, false, stopRange))
            {
                // MoveTo returns false only while a previous pathfind is queued
                _tolk.SpeakInterrupt("Wegfindung läuft schon. Gleich nochmal versuchen.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Auto-Lauf: vnavmesh-IPC fehlgeschlagen (Plugin installiert?)");
            _tolk.SpeakInterrupt("Auto-Lauf nicht verfügbar. Das Plugin vnavmesh fehlt oder ist nicht geladen.");
            return false;
        }
        return true;
    }

    private void BeginWalk()
    {
        _active = true;
        _sawRunning = false;
        _startedAt = DateTime.UtcNow;
        _startTerritory = (ushort)_clientState.TerritoryType;
        _lastPosition = _objectTable.LocalPlayer?.Position ?? default;
        _lastMoveAt = DateTime.UtcNow;
        _lastProgressSpeak = DateTime.UtcNow;
        _diagLoggedPath = false;
        _lastDiagAt = DateTime.UtcNow;
        _log.Info($"[Nav] Auto-Lauf: gestartet zu {_targetName} (id={_targetId:X}, stopRange={_stopRange:F1}, " +
                  $"dist={Vector3.Distance(_objectTable.LocalPlayer?.Position ?? default, _destPosition):F1})");
        _tolk.SpeakInterrupt($"Laufe zu {_targetName}.");
    }

    /// <summary>Stops a running auto-walk without any announcement (e.g. when the walk guide takes over).</summary>
    public void StopQuiet()
    {
        if (_active) Stop(announce: false);
    }

    private void Stop(bool announce)
    {
        _active = false;

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
        if (announce) _tolk.SpeakInterrupt("Auto-Lauf gestoppt.");
    }

    /// <summary>Watches the running walk. Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
    {
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
            _tolk.SpeakInterrupt("Angekommen, neues Gebiet erreicht.");
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
            _tolk.SpeakInterrupt("Auto-Lauf abgebrochen, vnavmesh antwortet nicht.");
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
                // Queued (not interrupting) so it follows "Laufe zu ..."; push
                // the 3 s progress timer back so "Noch X Meter" does not cut
                // the preview off mid-sentence.
                _tolk.Speak(_routes.DescribeRoute(_targetName, waypoints));
                _lastProgressSpeak = DateTime.UtcNow.AddSeconds(5);
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
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[NavDiag] Waypoint-IPC fehlgeschlagen");
        }

        // Spoken progress every 3 s so the walk is clearly working (the beacon
        // tone alone left the user unsure and cancelling; report 2026-07-11).
        if ((now - _lastProgressSpeak).TotalSeconds >= 3)
        {
            _lastProgressSpeak = now;
            _tolk.SpeakInterrupt($"Noch {FormatRemaining(distance)}.");
            _log.Info($"[Nav] Auto-Lauf: läuft, dist={distance:F1} running={running} computing={computing}");
        }

        if (_sawRunning && !running)
        {
            // Path finished. Arrived, or did vnavmesh stop early?
            _active = false;
            var arrived = distance <= _stopRange + 1.5f;
            _log.Info($"[Nav] Auto-Lauf: Pfad beendet, dist={distance:F1}, angekommen={arrived}");
            _tolk.SpeakInterrupt(arrived
                ? $"Ziel erreicht: {_targetName}."
                : $"Auto-Lauf beendet, noch {FormatRemaining(distance)}.");
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
            Stop(announce: false);
            _tolk.SpeakInterrupt(arrived
                ? $"Ziel erreicht: {_targetName}."
                : $"Ich stecke fest, noch {FormatRemaining(distance)}. Auto-Lauf beendet.");
            return;
        }

        // Pathfind finished but produced no path (unreachable destination).
        // Grace period covers the frames between queueing and task start.
        if (!_sawRunning && !computing && (now - _startedAt).TotalSeconds > 1.5)
        {
            _active = false;
            _log.Info($"[Nav] Auto-Lauf: kein Weg zu {_targetName} (id={_targetId:X}) gefunden.");
            _tolk.SpeakInterrupt($"Kein Weg zu {_targetName} gefunden.{_places.BuildNoPathHint(_destPosition)}");
        }
    }

    private static string FormatRemaining(float distance) =>
        float.IsNaN(distance) ? "Ziel unbekannt" : $"{distance:F0} Meter";

    public void Dispose() => StopQuiet();
}
