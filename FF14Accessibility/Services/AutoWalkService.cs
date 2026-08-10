using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Automatic walking via the external vnavmesh plugin: vnavmesh computes a path
/// on the walkable-surface mesh and steers the character, this service decides
/// when a walk starts, when it is over, and what the player is told.
///
/// The whole design follows three facts decompiled from the installed
/// vnavmesh.dll (2026-08-10), each of which broke the previous implementation:
///
/// 1. A path request is ASYNCHRONOUS and does not cancel the running path.
///    <c>AsyncMoveRequest.MoveTo</c> only queues a task; the old path keeps
///    steering until <c>Update</c> hands the result to <c>FollowPath.Move</c>.
///    So right after a start, "is a path running" still describes the PREVIOUS
///    walk. Reading it then produced instant bogus "arrived/ended" verdicts
///    (log 2026-08-10 08:05:05: ended 52 ms after start, 499 m short).
///
/// 2. vnavmesh restarts itself. With <c>StopOnStuck</c> + <c>RetryOnStuck</c>
///    (both on in the user's config) <c>FollowPath</c> calls <c>Stop()</c> after
///    <c>StuckTimeoutMs</c> without movement and immediately re-queues the same
///    destination. "Path is running" therefore blinks false once a second while
///    the character is wedged - it is NOT an end-of-walk signal. Every such gap
///    must be debounced (<see cref="PathEndDebounceS"/>).
///
/// 3. The last waypoint is fiction. <c>NavmeshQuery.PathfindMesh</c> appends the
///    requested destination to the result unconditionally, reachable or not.
///    When a zone's mesh falls apart into unconnected islands, vnavmesh happily
///    returns a path whose final hop is hundreds of metres through solid rock,
///    and then shoves the character against the mesh edge forever (log
///    2026-08-10 08:04:24-08:05:55: 91 retries, one per second, in silence).
///
/// Consequences for this service: it stops the running path BEFORE starting its
/// own, it never trusts a single frame's status, it always calls Path.Stop when
/// it ends a walk, and it keeps watching for a while afterwards because a
/// pathfind already in flight can revive a stopped walk (see <see cref="_guardUntil"/>).
/// </summary>
public sealed class AutoWalkService : IDisposable
{
    /// <summary>Stop this close to the destination, in yalms/meters (interaction range).
    /// Public so a position-based walk to a browsed object stops as close as the
    /// walk to a game target would (Plugin.TryResolveMarkerDestination).</summary>
    public const float StopRange = 2.5f;

    /// <summary>Counts as arrived this far beyond the requested stop range. vnavmesh
    /// aims for the range, the character coasts a little past it.</summary>
    private const float ArrivalSlack = 1.5f;

    /// <summary>A walk is only over once "no path running and none computing" has
    /// held this long. Shorter than vnavmesh's own 1 s stuck-retry cycle would
    /// mistake its self-restart for the end of the walk (see fact 2 above).</summary>
    private const double PathEndDebounceS = 1.6;

    /// <summary>No path has appeared this long after the request - treat it as
    /// "no route". Covers pathfind time on large zones with room to spare.</summary>
    private const double StartTimeoutS = 6.0;

    /// <summary>The character has not moved this long while a path claims to run:
    /// wedged on geometry, or shoved against the edge of the mesh towards a
    /// destination the mesh cannot reach.</summary>
    private const double StallS = 4.0;

    /// <summary>Counts as real movement (below this is jitter against geometry).</summary>
    private const float MovementEpsilon = 0.5f;

    /// <summary>
    /// Getting no closer to the destination for this long, with only the appended
    /// destination left as a waypoint, means the mesh ends here. Needed next to
    /// the stall check because being shoved at an unreachable point makes the
    /// character skid a little every retry, which keeps resetting the stall timer
    /// (log 2026-08-10 18:31:26: 12 s of shoving before the stall check fired).
    /// </summary>
    private const double NoApproachS = 2.5;

    /// <summary>Getting at least this much closer counts as progress.</summary>
    private const float ApproachEpsilon = 1f;

    /// <summary>
    /// Only judge "no approach" while still this far outside the stop range. On
    /// the final straight a walk legitimately spends a moment without closing in
    /// (rounding an obstacle), and there the normal stall check is the right tool.
    /// </summary>
    private const float NoApproachMinDistance = 20f;

    /// <summary>Counts as "reached the far end of the trail". Wider than the
    /// normal arrival slack: the recording ends wherever the player happened to
    /// stand, and the point of a crossing is being on the other side, not on a
    /// particular metre of it.</summary>
    private const float TrailArrivalRange = 4f;

    /// <summary>Grace period before an empty waypoint list counts as the end of
    /// the trail - FollowPath prunes points it deems already reached on its first
    /// update, so the list can look empty for a frame right after the handover.</summary>
    private const double TrailSettleS = 0.5;

    /// <summary>After ending a walk, keep vetoing revivals this long: a pathfind
    /// that was already computing when we stopped still gets handed to FollowPath
    /// and would walk off unsupervised (fact 1).</summary>
    private const double StopGuardS = 3.0;

    private enum Phase
    {
        /// <summary>Nothing running.</summary>
        Idle,
        /// <summary>Path requested, waiting for vnavmesh to deliver it.</summary>
        Starting,
        /// <summary>Our path is steering the character.</summary>
        Walking,
        /// <summary>Driving a recorded trail over a gap in the mesh (see
        /// <see cref="TryTakeTrail"/>), with vnavmesh's pathfinding out of the loop.</summary>
        TrailWalking,
        /// <summary>Walk over; suppressing late revivals (see <see cref="StopGuardS"/>).</summary>
        Guarding,
    }

    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IClientState _clientState;
    private readonly TolkService _tolk;
    private readonly Configuration _config;
    private readonly PlacesService _places;
    private readonly RouteService _routes;
    private readonly TrailService _trails;
    private readonly IPluginLog _log;
    private readonly NavmeshIpc _nav;

    private Phase _phase = Phase.Idle;
    private DateTime _startedAt;
    private DateTime _guardUntil;
    private bool _guardWarned;          // late revival reported once per walk

    private ulong _targetId;            // 0 for position destinations (quest markers)
    private string _targetName = string.Empty;
    private Vector3 _destPosition;      // refreshed from the object each frame if _targetId != 0
    private float _stopRange = StopRange;
    private ushort _startTerritory;

    private Vector3 _lastPosition;      // where the character last actually moved
    private DateTime _lastMoveAt;
    private DateTime _pathQuietSince;   // when "no path, no pathfind" started holding
    private bool _pathQuiet;
    private float _lastProgressDistance;
    private float _bestDistance;        // closest we have been to the destination
    private DateTime _lastApproachAt;   // when that last improved
    private bool _routeSpoken;
    private int _lastWaypointCount;     // remaining hops at the last check
    private DateTime _lastDiagAt;

    // Spur-Etappe (siehe TryTakeTrail / TrailWalkingUpdate)
    private Vector3 _trailEnd;
    private int _trailWaypointsSeen;    // highest waypoint count seen on our own list
    private DateTime _trailStartedAt;
    private readonly HashSet<string> _usedTrails = new();

    /// <summary>Whether an auto-walk is currently running. Plugin.cs suppresses
    /// automatic target-change announcements while this is true - passing NPCs
    /// grab the soft target every few steps and each one would be announced
    /// with distance and direction (user feedback 2026-07-10).</summary>
    public bool IsActive => _phase is Phase.Starting or Phase.Walking or Phase.TrailWalking;

    /// <summary>Whether the follow mode is currently running (see <see cref="ToggleFollow"/>).</summary>
    public bool IsFollowing => _following;

    public AutoWalkService(
        IDalamudPluginInterface pluginInterface,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IClientState clientState,
        TolkService tolk,
        Configuration config,
        PlacesService places,
        RouteService routes,
        TrailService trails,
        IPluginLog log)
    {
        _objectTable = objectTable;
        _targetManager = targetManager;
        _clientState = clientState;
        _tolk = tolk;
        _config = config;
        _places = places;
        _routes = routes;
        _trails = trails;
        _log = log;
        _nav = new NavmeshIpc(pluginInterface, log);
    }

    // ── Höhen auf dem Netz suchen ────────────────────────────────────

    /// <summary>
    /// Resolves the walkable height for a 2D map position (map markers carry
    /// no Y). Uses the given height as the search origin. Returns null if
    /// vnavmesh is missing/not ready or no floor exists near the point.
    /// </summary>
    public Vector3? ResolveFloorPoint(Vector3 approximate)
    {
        if (!_nav.IsReady) return null;

        // Prefer NearestPoint with a bounded vertical extent: it stays near the
        // given height instead of dropping to a lower floor. 10 m XZ covers
        // markers a little off the path, 10 m Y catches small level changes
        // without falling through to a floor tens of metres below.
        var nearest = _nav.NearestPoint(approximate, 10f, 10f);
        if (nearest.HasValue)
        {
            _log.Info($"[Orte] NearestPoint ({Fmt(approximate)}) -> ({Fmt(nearest.Value)})");
            return nearest;
        }

        // Second pass with a tall column: 2D markers use the PLAYER's height as
        // reference, but a target hundreds of metres away can sit on very
        // different ground (log 2026-07-13: aetheryte 0.5 km off failed with the
        // +-10 m box). NearestPoint returns the point CLOSEST to the input, so
        // with several levels the one nearest the reference height still wins -
        // unlike PointOnFloor's blind down-cast (bridge trap, V4.41).
        nearest = _nav.NearestPoint(approximate, 10f, 100f);
        if (nearest.HasValue)
        {
            _log.Info($"[Orte] NearestPoint (hohe Säule) ({Fmt(approximate)}) -> ({Fmt(nearest.Value)})");
            return nearest;
        }

        // Last resort when no mesh sits near the height at all.
        var floor = _nav.PointOnFloor(approximate, 5f);
        _log.Info($"[Orte] NearestPoint leer, PointOnFloor ({Fmt(approximate)}) -> " +
                  (floor.HasValue ? $"({Fmt(floor.Value)})" : "null"));
        return floor;
    }

    /// <summary>
    /// Resolves a fishing spot's WATER-CENTRE position to the nearest walkable
    /// bank you can cast from. Fishing spots sit in the middle of the water where
    /// no mesh exists, so the generic <see cref="ResolveFloorPoint"/> either finds
    /// nothing or snaps to a lakebed far below. Here we search a WIDE horizontal
    /// area (banks can be tens of metres from the centre) but a THIN vertical slab
    /// around the player's height, so the result is the bank at water level.
    /// </summary>
    public Vector3? ResolveNearestBank(Vector3 waterCentre)
    {
        if (!_nav.IsReady) return null;

        var bank = _nav.NearestPoint(waterCentre, 75f, 8f);
        _log.Info($"[Angeln] Ufer: NearestPoint ({Fmt(waterCentre)}) -> " +
                  (bank.HasValue ? $"({Fmt(bank.Value)})" : "null"));
        return bank;
    }

    // ── Auto-Lauf starten und beenden ────────────────────────────────

    /// <summary>Starts the auto-walk to the current game target, or stops a running one.</summary>
    public void Toggle()
    {
        StopFollowQuiet();   // a one-shot walk cancels a running follow (shared vnavmesh)

        if (IsActive)
        {
            Finish(AccessibilityStrings.AutoWalkStopped, "vom Spieler gestoppt");
            return;
        }

        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoTargetSelectN);
            return;
        }

        Begin(target.Position, target.Name.TextValue, StopRange, target.GameObjectId);
    }

    /// <summary>
    /// Starts the auto-walk to a fixed world position (quest markers and waypoints
    /// have no game object to target), or stops a running one. The caller passes
    /// the final stop range: tight for locations so the player arrives on the spot,
    /// tighter still for zone transitions so they trigger. The position should
    /// already be snapped onto the walkable mesh.
    /// </summary>
    public void ToggleToPosition(Vector3 position, string name, float stopRange)
    {
        StopFollowQuiet();

        if (IsActive)
        {
            Finish(AccessibilityStrings.AutoWalkStopped, "vom Spieler gestoppt");
            return;
        }

        Begin(position, name, stopRange, 0);
    }

    /// <summary>
    /// Requests the path and enters <see cref="Phase.Starting"/>. Stops whatever
    /// vnavmesh was doing FIRST: a leftover path (quite possibly a stuck-retry
    /// cycle re-arming itself once a second) would otherwise both steer the
    /// character and make our own status reads describe the wrong walk.
    /// </summary>
    /// <param name="fresh">False when this is the continuation of a walk that was
    /// interrupted by a trail crossing: the "walking to X" line would repeat, and
    /// the trails already used must stay used so a crossing cannot be taken in a
    /// loop.</param>
    private void Begin(Vector3 destination, string name, float stopRange, ulong targetId, bool fresh = true)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        if (fresh) _usedTrails.Clear();

        if (!_nav.IsReady)
        {
            // Absent plugin and "mesh still building" are different problems and
            // get different advice; LastCallFailed tells them apart.
            var progress = _nav.BuildProgress;
            if (_nav.LastCallFailed)
            {
                _log.Warning("[Nav] Auto-Lauf: vnavmesh antwortet nicht (Plugin installiert und aktiv?)");
                _tolk.SpeakInterrupt(AccessibilityStrings.AutoWalkUnavailable);
            }
            else
            {
                _tolk.SpeakInterrupt(progress >= 0
                    ? AccessibilityStrings.MeshStillLoading(progress * 100)
                    : AccessibilityStrings.MeshNotReady);
            }
            return;
        }

        var distance = Vector3.Distance(player.Position, destination);
        if (distance <= stopRange)
        {
            // Already there - starting a walk would be a no-op the player has to
            // wait out, and vnavmesh would report an immediate end that reads
            // like a failure.
            _log.Info($"[Nav] Auto-Lauf: schon am Ziel {name} (dist={distance:F1} <= {stopRange:F1}).");
            _tolk.SpeakInterrupt(AccessibilityStrings.AlreadyAtTarget(name));
            return;
        }

        _nav.Stop();

        if (!_nav.MoveCloseTo(destination, stopRange))
        {
            _tolk.SpeakInterrupt(_nav.LastCallFailed
                ? AccessibilityStrings.AutoWalkUnavailable
                : AccessibilityStrings.PathfindBusy);
            return;
        }

        _targetId = targetId;
        _targetName = name;
        _destPosition = destination;
        _stopRange = stopRange;
        _startTerritory = (ushort)_clientState.TerritoryType;

        _phase = Phase.Starting;
        _startedAt = DateTime.UtcNow;
        _lastPosition = player.Position;
        _lastMoveAt = _startedAt;
        _lastProgressDistance = distance;
        _bestDistance = distance;
        _lastApproachAt = _startedAt;
        _lastDiagAt = _startedAt;
        _pathQuiet = false;
        _routeSpoken = false;
        _guardWarned = false;
        _lastWaypointCount = 0;

        _log.Info($"[Nav] Auto-Lauf: gestartet zu {name} (id={targetId:X}, stopRange={stopRange:F1}, " +
                  $"dist={distance:F1}, neu={fresh})");
        if (fresh) _tolk.SpeakInterrupt(AccessibilityStrings.WalkingTo(name));
    }

    /// <summary>
    /// Ends the walk: clears vnavmesh's path, announces <paramref name="spoken"/>
    /// (null for silent) and enters the guard phase. ALWAYS the way a walk ends -
    /// the previous implementation had exits that only dropped their own tracking
    /// and left vnavmesh steering (log 2026-08-10: a minute of unsupervised
    /// shoving after "Auto-Lauf beendet").
    /// </summary>
    private void Finish(string? spoken, string reason)
    {
        _nav.Stop();
        _phase = Phase.Guarding;
        _guardUntil = DateTime.UtcNow.AddSeconds(StopGuardS);
        _log.Info($"[Nav] Auto-Lauf: beendet ({reason}).");
        if (spoken != null) _tolk.SpeakInterrupt(spoken);
    }

    /// <summary>Stops a running auto-walk without any announcement (e.g. when the
    /// manual walk guide takes over).</summary>
    public void StopQuiet()
    {
        if (IsActive) Finish(null, "still gestoppt");
    }

    // ── Jede Frame: Aufsicht über den laufenden Weg ──────────────────

    /// <summary>Watches the running walk. Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
    {
        // Runs even when no walk is active: the player wants to know when the
        // navmesh finishes loading, precisely BECAUSE they cannot walk yet.
        MonitorMeshBuild();

        if (_following) { FollowUpdate(); return; }

        switch (_phase)
        {
            case Phase.Guarding: GuardUpdate(); return;
            case Phase.Starting: StartingUpdate(); return;
            case Phase.Walking:  WalkingUpdate(); return;
            case Phase.TrailWalking: TrailWalkingUpdate(); return;
            default: return;
        }
    }

    /// <summary>
    /// Waits for vnavmesh to deliver OUR path. Since <see cref="Begin"/> cleared
    /// the old one, a running path here is ours. Nothing is judged before it
    /// exists - that mistake produced instant "ended, 499 m remaining" verdicts.
    /// </summary>
    private void StartingUpdate()
    {
        if (_objectTable.LocalPlayer == null) { Finish(null, "Spieler weg"); return; }

        if (_nav.IsRunning)
        {
            _phase = Phase.Walking;
            _lastMoveAt = DateTime.UtcNow;
            _pathQuiet = false;
            _log.Info($"[Nav] Auto-Lauf: Pfad steht ({_nav.NumWaypoints} Wegpunkte), laufe.");
            return;
        }

        // Still computing is fine; only give up once nothing is coming.
        if ((DateTime.UtcNow - _startedAt).TotalSeconds <= StartTimeoutS) return;

        _log.Info($"[Nav] Auto-Lauf: kein Weg zu {_targetName} (id={_targetId:X}) gefunden.");
        Finish(AccessibilityStrings.NoPathTo(_targetName, _places.BuildNoPathHint(_destPosition)), "kein Weg");
    }

    private void WalkingUpdate()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) { Finish(null, "Spieler weg"); return; }

        // Zone transition succeeded: walking to a transition put the player into a
        // new area. This is the real "arrived" signal for cross-zone walks -
        // vnavmesh's own path never reports it (the destination is on the far side).
        if ((ushort)_clientState.TerritoryType != _startTerritory)
        {
            Finish(AccessibilityStrings.ArrivedNewZone, $"Gebiet {_startTerritory} -> {_clientState.TerritoryType}");
            return;
        }

        // Moving objects (NPCs) update their destination; markers are fixed.
        if (_targetId != 0)
        {
            var obj = _objectTable.FirstOrDefault(o => o.GameObjectId == _targetId);
            if (obj != null) _destPosition = obj.Position;
        }

        var now = DateTime.UtcNow;
        var distance = Vector3.Distance(player.Position, _destPosition);
        var waypoints = _nav.Waypoints;
        var remaining = waypoints.Count;

        // Arrival is decided on distance, not on vnavmesh going quiet: its path
        // goes quiet for a second on every stuck-retry too.
        if (distance <= _stopRange + ArrivalSlack)
        {
            Finish(AccessibilityStrings.TargetReached(_targetName), $"angekommen, dist={distance:F1}");
            return;
        }

        SpeakRoutePreviewOnce(player.Position, waypoints);
        LogDiagnostics(player.Position, distance, waypoints, now);

        // Own movement, not distance to the destination: a detour legitimately
        // moves away from the target for a while (false abort right after start,
        // log 2026-07-13 01:08).
        if (Vector3.Distance(player.Position, _lastPosition) >= MovementEpsilon)
        {
            _lastPosition = player.Position;
            _lastMoveAt = now;
        }

        if (distance <= _bestDistance - ApproachEpsilon)
        {
            _bestDistance = distance;
            _lastApproachAt = now;
        }

        // Being shoved at the appended destination (fact 3): the only waypoint
        // left IS that destination, and we are getting no closer to it. Checked
        // separately from the stall timer because every stuck-retry skids the
        // character a little, which resets that timer and dragged the verdict out
        // to 12 s. Restricted to targets still far away - near the destination a
        // pause without progress is just a walk rounding an obstacle.
        if (remaining <= 1
            && distance > _stopRange + NoApproachMinDistance
            && (now - _lastApproachAt).TotalSeconds > NoApproachS)
        {
            _log.Info($"[Nav] Auto-Lauf: keine Annäherung seit {NoApproachS:F1} s bei restWp={remaining}, " +
                      $"dist={distance:F1} - Netz endet hier.");
            if (TryTakeTrail(player.Position)) return;
            var far = RouteService.CompassWord(player.Position, _destPosition);
            Finish(AccessibilityStrings.WalkMeshEndsHere(distance, far), "Netz endet hier (keine Annäherung)");
            return;
        }

        // Not moving while a path claims to run. Two causes, one remedy, but the
        // player deserves to know which: with a single waypoint left we are being
        // shoved at the appended destination the mesh cannot reach (fact 3), so
        // the mesh ends here; otherwise we are wedged on geometry.
        if ((now - _lastMoveAt).TotalSeconds > StallS)
        {
            var meshEnds = remaining <= 1;
            _log.Info($"[Nav] Auto-Lauf: keine Bewegung seit {StallS:F0} s, dist={distance:F1}, " +
                      $"restWp={remaining}, Netzende={meshEnds}");
            if (meshEnds && TryTakeTrail(player.Position)) return;
            var direction = RouteService.CompassWord(player.Position, _destPosition);
            Finish(meshEnds
                    ? AccessibilityStrings.WalkMeshEndsHere(distance, direction)
                    : AccessibilityStrings.StuckRemaining(distance),
                meshEnds ? "Netz endet hier" : "festgesteckt");
            return;
        }

        // End of path - but only when it HOLDS. vnavmesh clears its waypoints for
        // a moment on every stuck-retry (once a second in the user's config), and
        // a single-frame read of that gap used to end the walk on the spot.
        var idle = !_nav.IsRunning && !_nav.PathfindInProgress;
        if (idle && !_pathQuiet)
        {
            _pathQuiet = true;
            _pathQuietSince = now;
        }
        else if (!idle)
        {
            _pathQuiet = false;
        }

        if (_pathQuiet && (now - _pathQuietSince).TotalSeconds >= PathEndDebounceS)
        {
            _log.Info($"[Nav] Auto-Lauf: Pfad zu Ende, dist={distance:F1}, restWp={remaining}");
            if (TryTakeTrail(player.Position)) return;
            var direction = RouteService.CompassWord(player.Position, _destPosition);
            Finish(AccessibilityStrings.WalkMeshEndsHere(distance, direction), "Pfad zu Ende ohne Ankunft");
            return;
        }

        _lastWaypointCount = remaining;
        SpeakProgress(distance);
    }

    // ── Spur-Etappe: über eine Lücke, die das Netz nicht kennt ───────

    /// <summary>
    /// Called wherever the walk has established that the mesh ends here. Looks for
    /// a trail the player recorded themselves and, if one fits, drives it with
    /// <c>Path.MoveTo</c> - a fixed point list, no pathfinding involved, which is
    /// the only way past a gap Recast does not know about.
    ///
    /// Each trail is used at most once per walk: after the crossing the normal
    /// walk resumes, and if THAT one ends at the mesh edge again, offering the
    /// same crossing would just loop.
    /// </summary>
    private bool TryTakeTrail(Vector3 position)
    {
        var points = _trails.FindUsableTrail(position, _destPosition, out var name);
        if (points == null || _usedTrails.Contains(name)) return false;

        _nav.Stop();
        if (!_nav.MoveAlong(points))
        {
            _log.Warning("[Nav] Auto-Lauf: Spur konnte nicht gestartet werden.");
            return false;
        }

        _usedTrails.Add(name);
        _trailEnd = points[^1];
        _trailWaypointsSeen = points.Count;
        _trailStartedAt = DateTime.UtcNow;
        _phase = Phase.TrailWalking;
        _lastPosition = position;
        _lastMoveAt = _trailStartedAt;

        _log.Info($"[Nav] Auto-Lauf: nehme Spur '{name}' mit {points.Count} Punkten, Ende ({Fmt(_trailEnd)}).");
        _tolk.SpeakInterrupt(AccessibilityStrings.TrailTaking(name));
        return true;
    }

    /// <summary>
    /// Watches the trail crossing. Two things end it, and they are told apart
    /// because they mean different things to the player: arriving at the far end
    /// (walk continues normally), or vnavmesh taking the wheel back. The latter
    /// happens when the figure stalls for half a second - FollowPath then drops
    /// our point list and re-routes to its last point over the mesh that has no
    /// connection (OnStuck + RetryOnStuck, decompiled). The tell is the waypoint
    /// count GROWING: our own list only ever shrinks.
    /// </summary>
    private void TrailWalkingUpdate()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) { Finish(null, "Spieler weg"); return; }

        var now = DateTime.UtcNow;
        var remaining = _nav.NumWaypoints;
        var toEnd = Vector3.Distance(player.Position, _trailEnd);

        // Two independent tells that vnavmesh took the wheel back, because either
        // alone can miss: a re-route over the zone yields MORE waypoints than our
        // list, a re-route straight at the point yields fewer - but it always
        // starts with a pathfind, and our own crossing never runs one.
        if (remaining > _trailWaypointsSeen || _nav.PathfindInProgress)
        {
            _log.Warning($"[Nav] Auto-Lauf: Spur verloren, vnavmesh routet selbst " +
                         $"(restWp={remaining}, vorher {_trailWaypointsSeen}, wegsuche={_nav.PathfindInProgress}).");
            Finish(AccessibilityStrings.TrailLost, "Spur von vnavmesh überschrieben");
            return;
        }
        _trailWaypointsSeen = remaining;

        // Arrival at the far end, or the list simply ran out: either way the
        // crossing is behind us and the normal walk takes over from here. The
        // empty-list case is only trusted after a moment: FollowPath drops
        // waypoints it considers already reached in its first update.
        if (toEnd <= TrailArrivalRange ||
            (remaining == 0 && (now - _trailStartedAt).TotalSeconds > TrailSettleS))
        {
            _log.Info($"[Nav] Auto-Lauf: Spur zu Ende (dist zum Spur-Ende={toEnd:F1}, restWp={remaining}), " +
                      "normaler Lauf geht weiter.");
            _tolk.SpeakInterrupt(AccessibilityStrings.TrailFinished);
            _nav.Stop();
            Begin(_destPosition, _targetName, _stopRange, _targetId, fresh: false);
            return;
        }

        if (Vector3.Distance(player.Position, _lastPosition) >= MovementEpsilon)
        {
            _lastPosition = player.Position;
            _lastMoveAt = now;
        }

        // Wedged on the crossing itself. Not the same as the check above: there
        // vnavmesh took over, here nothing moves at all (movement disabled,
        // combat, a ledge the recording glossed over).
        if ((now - _lastMoveAt).TotalSeconds > StallS)
        {
            _log.Info($"[Nav] Auto-Lauf: auf der Spur festgesteckt, noch {toEnd:F1} m bis zum Spur-Ende.");
            Finish(AccessibilityStrings.TrailLost, "auf der Spur festgesteckt");
        }
    }

    /// <summary>
    /// After a walk ends: a pathfind that was already in flight when we stopped
    /// still gets handed to FollowPath, and vnavmesh's own stuck-retry re-arms
    /// the same way - either would walk the character off with nobody watching.
    /// Stop it again, and say so once, because unexplained movement is worse than
    /// no movement for someone who cannot see it.
    /// </summary>
    private void GuardUpdate()
    {
        if (DateTime.UtcNow >= _guardUntil)
        {
            _phase = Phase.Idle;
            return;
        }

        if (!_nav.IsRunning) return;

        _nav.Stop();
        if (_guardWarned) return;
        _guardWarned = true;
        _log.Info("[Nav] Auto-Lauf: nachlaufender Pfad nach dem Ende abgeräumt.");
    }

    /// <summary>
    /// Speaks the route preview once the path exists (user request 2026-07-15).
    /// The player's own position is passed along: vnavmesh's waypoint list starts
    /// at the first hop, so measuring only between waypoints dropped the whole
    /// leg from the character to the path - a 454 m walk was announced as
    /// "practically there" (log 2026-08-10 08:04:45).
    /// </summary>
    private void SpeakRoutePreviewOnce(Vector3 playerPosition, IReadOnlyList<Vector3> waypoints)
    {
        if (_routeSpoken || waypoints.Count == 0) return;
        _routeSpoken = true;

        var last = waypoints[^1];
        _log.Info($"[Nav] Pfad: {waypoints.Count} Wegpunkte, letzter->Ziel={Vector3.Distance(last, _destPosition):F1} m. " +
                  $"Route: {string.Join(" -> ", waypoints.Select(w => $"({Fmt(w)})"))}");

        // A "route" consisting only of the appended destination is not a route at
        // all - vnavmesh found no corridor and just handed the wish back (fact 3).
        // Announcing it produced "Weg zu X, 411 Meter: 411 Meter nach Osten" for a
        // walk that could not take a single step (log 2026-08-10 18:31:21). Stay
        // quiet; the no-approach check speaks the truth a moment later.
        if (waypoints.Count <= 1 &&
            Vector3.Distance(playerPosition, _destPosition) > _stopRange + NoApproachMinDistance)
        {
            _log.Info("[Nav] Pfad besteht nur aus dem angehängten Ziel - keine Routen-Vorschau.");
            return;
        }

        // Queued, not interrupting, so it follows "Laufe zu ...".
        _tolk.Speak(_routes.DescribeRoute(_targetName, waypoints, playerPosition));
    }

    /// <summary>
    /// Spoken progress, tied to distance covered rather than the clock: one line
    /// per configured step, so a short hop stays silent and a long run reports a
    /// handful of times. Originally every 3 s, which turned a long walk into a
    /// wall of "noch X Meter" (report 2026-07-18). 0 turns it off.
    /// </summary>
    private void SpeakProgress(float distance)
    {
        var step = _config.AutoWalkProgressStep;
        if (step <= 0 || distance > _lastProgressDistance - step) return;

        _lastProgressDistance = distance;
        _tolk.SpeakInterrupt(AccessibilityStrings.StillToGo(distance));
    }

    private void LogDiagnostics(Vector3 position, float distance, IReadOnlyList<Vector3> waypoints, DateTime now)
    {
        if ((now - _lastDiagAt).TotalSeconds < 1) return;
        _lastDiagAt = now;

        var next = waypoints.Count > 0 ? waypoints[0] : default;
        var distNext = waypoints.Count > 0 ? Vector3.Distance(position, next) : -1f;
        _log.Info($"[NavDiag] pos=({Fmt(position)}) distZiel={distance:F1} restWp={waypoints.Count} " +
                  $"nextWp=({Fmt(next)}) distNextWp={distNext:F1}");
    }

    private static string Fmt(Vector3 v) => $"{v.X:F1}|{v.Y:F1}|{v.Z:F1}";

    // ── Wegenetz-Aufbau mitverfolgen ─────────────────────────────────

    private float _lastMeshProgress = -1f;   // -1 = kein Aufbau läuft
    private int _lastSpokenMeshStep = -1;    // last announced 20 % step (0..4)
    private DateTime _lastMeshRetryLog;

    /// <summary>
    /// Announces the navmesh build in 20 % steps and reports when it is done
    /// (user request 2026-07-18). Without it the player has no way to tell "still
    /// loading" from "broken" - the auto-walk simply refuses to start.
    ///
    /// vnavmesh semantics (NavmeshManager, decompiled 2026-07-18): LoadTaskProgress
    /// is -1 while no build runs, 0 when one starts, grows to 1, and returns to -1
    /// when the task ends - so completion shows up as the drop back to -1, and only
    /// Nav.IsReady tells success from cancellation. Cache-served loads can finish
    /// so fast that no intermediate step is seen; then only start and finish speak.
    ///
    /// Never latches off: the plugin can load a second before vnavmesh does, and
    /// an early failed read used to disable the announcement for the whole session
    /// (log 2026-08-09 21:31).
    /// </summary>
    private void MonitorMeshBuild()
    {
        if (!_config.AnnounceMeshProgress) return;

        var progress = _nav.BuildProgress;
        if (_nav.LastCallFailed)
        {
            // vnavmesh not up yet (or gone). Keep trying, but log at most once a minute.
            if ((DateTime.UtcNow - _lastMeshRetryLog).TotalSeconds >= 60)
            {
                _lastMeshRetryLog = DateTime.UtcNow;
                _log.Info("[Nav] Wegenetz-Fortschritt noch nicht lesbar, versuche es weiter.");
            }
            return;
        }

        var wasBuilding = _lastMeshProgress >= 0f;
        var isBuilding = progress >= 0f;
        _lastMeshProgress = progress;

        if (isBuilding)
        {
            if (!wasBuilding)
            {
                _lastSpokenMeshStep = 0;   // step 0 counts as spoken, so 0 % stays quiet
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
        var ready = _nav.IsReady;
        _log.Info($"[Nav] Wegenetz-Aufbau beendet, bereit={ready}");
        _tolk.SpeakInterrupt(ready ? AccessibilityStrings.MeshReady : AccessibilityStrings.MeshAborted);
    }

    // ── Ziel folgen (kontinuierlich) ─────────────────────────────────
    //
    // Unlike the one-shot walk - which computes ONE path and stops on arrival -
    // follow keeps re-issuing the path to the target's CURRENT position, so the
    // character trails a moving player and stops when they stop (user request
    // 2026-07-26). FFXIV has no plugin-callable native "follow" (verified against
    // FFXIVClientStructs: MoveController carries no follow field), so this is
    // rebuilt on vnavmesh - the same engine the auto-walk uses.

    private bool _following;
    private ulong _followTargetId;
    private string _followName = string.Empty;
    private ushort _followStartTerritory;
    private Vector3 _lastFollowDest;
    private DateTime _lastFollowPathAt;

    /// <summary>Trail distance in yalms: stop this far behind the target.</summary>
    private const float FollowDistance = 3f;
    /// <summary>Only re-path once the target has drifted this far from the last
    /// commanded destination - keeps a slow target from re-pathing every frame.</summary>
    private const float FollowRepathMove = 1.5f;
    /// <summary>Minimum seconds between re-paths (throttle for vnavmesh).</summary>
    private const double FollowRepathIntervalS = 0.4;

    /// <summary>
    /// Starts following the current game target, or stops a running follow. The
    /// character trails the target at <see cref="FollowDistance"/> and halts when
    /// the target halts; a second key press ends it. Mutually exclusive with the
    /// one-shot auto-walk (each cancels the other).
    /// </summary>
    public void ToggleFollow()
    {
        if (_following)
        {
            StopFollow(announce: true);
            return;
        }

        if (IsActive) Finish(null, "Folgen übernimmt");

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
        _followName = string.IsNullOrWhiteSpace(target.Name.TextValue)
            ? AccessibilityStrings.UnnamedOfKind(target.ObjectKind)
            : target.Name.TextValue;
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
        _nav.Stop();
        // Same guard as the one-shot walk: a pathfind in flight would revive it.
        _phase = Phase.Guarding;
        _guardUntil = DateTime.UtcNow.AddSeconds(StopGuardS);
        _guardWarned = false;
        _log.Info("[Nav] Folgen: gestoppt.");
        if (announce) _tolk.SpeakInterrupt(AccessibilityStrings.FollowStopped);
    }

    /// <summary>Ends a running follow without announcement (e.g. when a walk takes over).</summary>
    public void StopFollowQuiet() => StopFollow(announce: false);

    /// <summary>Runs every frame while follow is active: re-issues the path toward
    /// the target's current position and ends when the target vanishes, the player
    /// leaves, or the zone changes.</summary>
    private void FollowUpdate()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) { StopFollow(announce: false); return; }

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

        // Nothing to do while already within trail distance - let the target pull
        // away first (the character stops when the target stops).
        if (distance <= FollowDistance + 0.5f) return;

        if ((now - _lastFollowPathAt).TotalSeconds < FollowRepathIntervalS) return;
        if (_nav.PathfindInProgress) return;

        // Re-path when the target drifted enough OR the previous path already
        // finished (idle but still beyond trail distance - the target walked off).
        if (Vector3.Distance(dest, _lastFollowDest) < FollowRepathMove && _nav.IsRunning) return;

        if (!_nav.MoveCloseTo(dest, FollowDistance))
        {
            if (!_nav.LastCallFailed) return;   // pathfind busy: just try again next frame
            _log.Warning("[Nav] Folgen: vnavmesh antwortet nicht, breche ab");
            StopFollow(announce: false);
            _tolk.SpeakInterrupt(AccessibilityStrings.FollowAbortedUnavailable);
            return;
        }

        _lastFollowDest = dest;
        _lastFollowPathAt = now;
    }

    public void Dispose()
    {
        StopFollowQuiet();
        StopQuiet();
    }
}
