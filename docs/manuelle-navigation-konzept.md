# Concept: Manual navigation along the vnavmesh path

Status: research 2026-07-15, referring to FF14Accessibility V4.61. Pure
feasibility analysis, no code change.

## Summary / feasibility statement

Feasible, with limitations. vnavmesh already delivers exactly the waypoint
list that a manually guided navigation needs (`Nav.Pathfind`), WITHOUT
triggering any movement. With BeaconService, CueService and the existing walk
guide (NavigationService), the plugin already has all the audio building
blocks needed for waypoint guidance. At its core the implementation is an
extension of the existing walk guide from a single target vector to a
waypoint list - not a rebuild.

Limitations: the navmesh does not know about dynamic obstacles (other
players, monsters), only the fixed terrain shape. Real jumps across gaps
(jump puzzles) are generally not represented by vnavmesh. Both are assessed
in more detail below.

## 1. vnavmesh IPC: path calculation without auto-movement

### What the project already uses

`AutoWalkService.cs` currently only uses movement-triggering and status
endpoints:

```73:110:H:\ff14\FF14Accessibility\Services\AutoWalkService.cs
        _navIsReady         = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _navBuildProgress   = pluginInterface.GetIpcSubscriber<float>("vnavmesh.Nav.BuildProgress");
        _moveCloseTo        = pluginInterface.GetIpcSubscriber<Vector3, bool, float, bool>("vnavmesh.SimpleMove.PathfindAndMoveCloseTo");
        _pathStop           = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        _pathIsRunning      = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        _pathfindInProgress = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
```

`SimpleMove.PathfindAndMoveCloseTo` calculates the path AND lets vnavmesh
walk the character automatically (`OverrideMovement`/`OverrideCamera`
internally, see `FollowPath.cs`). For our purpose that is the wrong endpoint -
it takes the keys out of the player's hands.

`Path.ListWaypoints` is already subscribed to, but only for diagnostics
(logging), not for guidance of our own:

```41:45:H:\ff14\FF14Accessibility\Services\AutoWalkService.cs
    // DIAGNOSTIC (temporary): the waypoints of the path vnavmesh is actually
    // following. Lets us tell whether the destination is reachable ...
    private readonly ICallGateSubscriber<List<Vector3>> _pathListWaypoints;
```

### The decisive find: `Nav.Pathfind`

Comparing against the source of `awgil/ffxiv_navmesh` (IPCProvider.cs,
current `master`) reveals a pure query endpoint that triggers NO movement:

```
RegisterFunc("Nav.Pathfind", (Vector3 from, Vector3 to, bool fly)
    => navmeshManager.QueryPathBasic(from, to, fly));
RegisterFunc("Nav.PathfindWithTolerance", (Vector3 from, Vector3 to, bool fly, float range)
    => navmeshManager.QueryPathBasic(from, to, fly, range));
RegisterFunc("Nav.PathfindInProgress", () => navmeshManager.PathfindInProgress);
RegisterFunc("Nav.PathfindNumQueued", () => navmeshManager.NumQueuedPathfindRequests);
RegisterAction("Nav.PathfindCancelAll", () => navmeshManager.Reload(true));
```

`QueryPathBasic` (in `NavmeshManager.cs`) runs asynchronously through the
navmesh query layer (`Query.PathfindMesh`/`PathfindVolume`, string pulling
optionally active) and returns a plain `List<Vector3>` - exactly the waypoint
list needed for audio guidance. `FollowPath.Move()` is not called anywhere, so
movement stays entirely with the player.

Signature for the Dalamud subscriber (following the existing patterns in
`AutoWalkService.cs`):

```csharp
ICallGateSubscriber<Vector3, Vector3, bool, List<Vector3>> navPathfind =
    pluginInterface.GetIpcSubscriber<Vector3, Vector3, bool, List<Vector3>>("vnavmesh.Nav.Pathfind");
```

`docs/game-api.md` has already noted this endpoint as an open item
("`Nav.Pathfind(from, to, fly) -> List<Vector3>` (waypoint list!)"), but has
not wired it up productively yet - this piece of work confirms it against the
source and shows the concrete way to use it.

Important for re-routing (section 2): `Nav.Pathfind` runs asynchronously
(query `PathfindInProgress`/`PathfindNumQueued` to find out whether a new
request has finished) - calling it again while a request is still running has
to be caught, exactly as `TryStartPath` already does today for `SimpleMove`.

### Conclusion on point 1

Directly feasible. No detour through reflection or undocumented structures is
needed - `Nav.Pathfind` is an officially registered IPC gate in the third-party
plugin, using the same subscriber technique the project already applies to six
other vnavmesh endpoints.

## 2. Guidance concept: waypoints instead of a straight line

### What already exists today (walk guide)

The existing walk guide in `NavigationService.cs` already does almost
everything needed - just with a single target point instead of a waypoint
list:

```504:537:H:\ff14\FF14Accessibility\Services\NavigationService.cs
    private void WalkGuideFrame(IGameObject player)
    {
        var obj = _objectTable.FirstOrDefault(o => o.GameObjectId == _walkTargetId);
        ...
        var distance = Vector3.Distance(player.Position, obj.Position);
        if (distance <= ArrivalDistance)
        {
            StopWalkGuide();
            _tolk.SpeakInterrupt($"Ziel erreicht: {_walkTargetName}.");
            return;
        }

        var relAngle = RelativeAngle(player, obj.Position);
        _beacon.Update(relAngle, distance);
        ...
        _tolk.SpeakInterrupt($"{FormatDistance(distance)}, {DirectionText(relAngle)}.");
    }
```

Today this is pure straight-line guidance: the tone always points directly at
the final destination, even when a wall, a cliff or a body of water lies in
between. That is exactly what waypoint guidance is meant to fix.

### Concept: waypoint state machine

Core idea: instead of a single target point, `WalkGuideFrame` gets a list
`List<Vector3> _pathWaypoints` plus an index `_pathIndex`. Sequence:

1. When the walk guide starts (`ToggleWalkGuide`): call
   `Nav.Pathfind(playerPos, targetPos, fly:false)` (asynchronously, waiting on
   `PathfindInProgress` as is already customary for `SimpleMove`).
2. Every frame: the beacon points at `_pathWaypoints[_pathIndex]` (no longer at
   the final destination), through the same
   `BeaconService.Update(relAngle, distance)` method as today.
3. If the player is closer to the current waypoint than an arrival radius
   (e.g. 2-3 m, analogous to the existing `ArrivalDistance = 3f`): increase the
   index by one. At the last waypoint the existing "destination reached" path
   applies.
4. Speech announcement still every 2 seconds, but now relative to the overall
   progress ("3 sections left, 45 metres total", or more simply just
   direction/distance to the current waypoint, see section 5).

Structurally this is very close to what vnavmesh's own `FollowPath.cs` does
internally (work through a waypoint, `Tolerance` for "reached", next waypoint) -
except that instead of `OverrideMovement`, our existing Beacon/Tolk mechanism
passes the waypoints on to the human.

### Deviation from the path / re-routing

If the player drifts sideways off the given path (e.g. because they do not
follow the announcement exactly, or dodge), the plain "next waypoint reached"
test eventually goes wrong: the player can scrape past a waypoint alongside the
path without an arrival being detected, or they move structurally away from the
calculated route.

Two supplementary checks (both implementable with existing means, without new
IPC endpoints):

- Perpendicular distance to the current waypoint segment: analogous to
  vnavmesh's own `DistanceToLineSegment` check in `FollowPath.cs`, one can
  check how far the player deviates sideways from the line (previous waypoint →
  current waypoint). If that exceeds a threshold (e.g. 5-8 m), the path counts
  as "left".
- Movement standstill as in the existing auto-walk watchdog
  (`AutoWalkService.Update`, position delta < 0.5 m for several seconds) can
  detect a snag/blockage.

On "path left", `Nav.Pathfind` is simply called again from the current player
position to the unchanged final destination and the waypoint list is replaced -
no special case, the same function as at the start. That matches exactly what
the user means by "re-routing", and is feasible with the async best practice
already present (check `PathfindInProgress`, no duplicate request).

### Conclusion on point 2

Feasible, as an extension of the existing walk guide (not a new feature from
scratch). Biggest rework: converting `WalkGuideFrame` from "one target point"
to "list + index". That is manageable state machine code, no new audio or IPC
technology.

## 3. Obstacles: is waypoint guidance enough?

### Static obstacles: yes, the navmesh already handles that

The navmesh is built once per zone from the level geometry (`NavmeshBuilder`,
cached in `NavmeshManager.BuildNavmesh`) and only represents walkable surfaces.
Walls, cliffs, buildings, water (depending on zone customisation) are already
cut out of it - a path from `Nav.Pathfind` never leads into a solid wall. For
standing environment geometry, waypoint guidance is therefore genuinely a
full-fledged obstacle avoidance, without the plugin itself having to know
anything about geometry.

### Dynamic obstacles: no, the navmesh does not know about those

The mesh is built once on entering the zone and does not change live
afterwards. Other players, monsters and NPCs currently standing in the way are
invisible to `Nav.Pathfind` - the calculated path can lead straight through a
group of monsters or another player. This is not a limitation that better use
of vnavmesh could fix; it follows from how the tool works.

Realistic assessment: this is not a new problem compared to today's auto-walk -
that stumbles in exactly the same places (which is why the standstill watchdog
exists in `AutoWalkService`). When walking manually the situation is if anything
EASIER for a blind player than with auto-walk: they notice the bump/stop from
the collision immediately through the normal game controls (the character stops,
the movement keys stop taking effect) and can react - they need no detour
detection via diagnostic log as with auto-walk.

### Sensible additional option (not a navmesh topic, but ObjectTable)

A genuine proximity warning ("monster 2 metres ahead") would technically NOT be
solvable via vnavmesh, but only via an additional scan of the `ObjectTable` in
the viewing direction (distance + angle cone in front of the player, similar to
the existing `CalculateDirection` logic). That is feasible, but a standalone
feature detached from waypoint guidance - see the effort estimate, comfort
tier. It coincides with the "object radar" idea already sketched in
`docs/verbesserungsvorschlaege.md` and should not be mixed up with the actual
path waypoint guidance.

### Conclusion on point 3

Waypoint guidance alone is fully sufficient for static obstacles. For dynamic
obstacles it is blind by design - a real limitation, but not a new one compared
to today's auto-walk, and bearable for the MVP.

## 4. Verticality: jumps, elevations, narrow passages

### Granularity of the waypoints

`Nav.Pathfind` runs with string pulling enabled
(`NavmeshManager.UseStringPulling = true`), which means: waypoints are NOT
placed at fixed intervals, but only where the direction changes (corners,
bends, stair landings). A long straight stretch yields a single waypoint across
dozens of metres, while a staircase or a winding corridor produces
correspondingly more points. That fits the existing beacon logic well: between
two waypoints the continuous tone/direction hint takes over the fine guidance,
exactly as it already does today for a single distant target.

### Height differences

Every waypoint is a complete `Vector3` (X, Y, Z) - height changes between two
consecutive waypoints can be read directly from the difference in Y values.
Concept: if the Y difference between the current and next waypoint exceeds a
threshold (e.g. ±1.5 m), add a short "upwards" / "downwards" or "steps" on top
of the direction announcement. That is pure vector arithmetic on data already
present, no new IPC needed.

### Real jumps (gaps, jump puzzles)

Here lies the actual limit: vnavmesh only represents WALKABLE surfaces. A gap
that can only be crossed by jumping (e.g. some treasure map spots, jump
puzzles) is as a rule NOT connected in the mesh - `Nav.Pathfind` simply finds
no route for it (exactly the "no path found" familiar from auto-walk, see
`AutoWalkService.BuildNoPathHint`). Special handling for that already exists
internally in vnavmesh only for automatically generated jump paths in dungeons
(`Navmesh.AreaId.ClientPath`/`Warp` in `FollowPath.CheckCondition`, there with
an automatic jump key for AUTO movement) - that does not apply to freely
walkable open-world gaps and is irrelevant for manual guidance anyway, because
the human jumps themselves.

### Conclusion on point 4

Feasible with a limitation: normal terrain steps, stairs and ramps are readily
announceable via the Y difference between waypoints. Real jump gaps remain
unreachable just as with today's auto-walk - that is a limit of vnavmesh
itself, not a gap in our implementation.

## 5. UX sketch

### Key bindings: no new key needed

The toggle between auto-walk and manually guided walking already exists:
`Numpad3` (auto-walk, `AutoWalkService.Toggle`) and `Shift+Numpad3` (walk
guide, `NavigationService.ToggleWalkGuide`). Waypoint guidance is conceptually
an extension of the existing walk guide (not a third mode), so the key bindings
from `Configuration.cs` stay unchanged:

- `N` / `Shift+N`: select an object or waypoint/quest target (as before).
- `Numpad3`: auto-walk (the game moves by itself).
- `Shift+Numpad3`: walk guide on/off - from now on path-based instead of
  straight-line.
- `F`: turn towards the current (intermediate) target (existing game key, still
  usable unchanged).
- `W`/`R`: walk/run manually (existing game keys, unchanged).

Optionally one could later offer an additional key for "force a hard straight
line instead of the path" (debug/fallback case), but that is not part of the
MVP.

### What the user hears, step by step

1. `N` repeatedly: choose a target (NPC, waypoint, quest target - as today).
2. `Shift+Numpad3`: walk guide on. Announcement e.g. "Walk guide on: Aetheryte
   Limsa Lominsa, calculating route." Short pause while `Nav.Pathfind`
   calculates asynchronously (typically well under a second for normal
   in-zone distances).
3. The beacon tone starts, directed at the FIRST waypoint (not the final
   destination) - pitch/pan as usual from `BeaconService`.
4. The user turns towards the tone with `F` or by ear, and walks with `W`.
5. Every 2 seconds a speech announcement about the current waypoint: "12 metres,
   straight ahead." (identical to today's format, only relative to the
   intermediate point instead of the final destination.)
6. On a larger height difference to the next waypoint, additionally:
   "The path now goes upwards."
7. When the user reaches the current waypoint (arrival radius), the beacon
   switches automatically, quietly and without interrupting speech, to the next
   waypoint - ideally with a short, unobtrusive transition tone from
   `CueService` (analogous to the existing destination tone), so the user
   notices the switch without a whole announcement cutting in.
8. If the user deviates noticeably from the path: a short announcement
   "recalculating route" and a silent switch to the new waypoint list (the
   beacon points at the new first point immediately).
9. At the last waypoint: the existing arrival announcement "destination
   reached: <name>." (unchanged from `WalkGuideFrame`).

## 6. Effort estimate

### MVP (core function, playable)

- Add IPC subscribers for `Nav.Pathfind` (and `Nav.PathfindInProgress`)
  analogous to the six existing vnavmesh subscribers.
- Rework `NavigationService.WalkGuideFrame` from a single target to a waypoint
  list + index (arrival radius per waypoint, advancing, final-destination
  detection as before).
- Re-pathfind trigger: perpendicular distance to the current segment (simple
  vector formula, analogous to vnavmesh's own `DistanceToLineSegment`) plus
  reuse of the existing standstill detection.
- Keep reusing the existing speech/tone building blocks (`BeaconService`,
  `TolkService`) unchanged, just fed with a new target point per frame.
- Test on several known routes (straight, around a corner, stairs, the known
  bridge-trap spot from the auto-walk log).

Assessment: small to medium effort - the async IPC handling, the beacon logic
and the standstill detection already exist in the project and "only" need
rewiring, not reinventing.

### Comfort (later expansion tiers)

- Height announcement at waypoint transitions ("upwards"/"downwards"/"steps")
  from the Y difference between waypoints.
- A short transition tone on waypoint change via `CueService` instead of a plain
  speech announcement (fewer speech interruptions along the way).
- Progress announcement on request ("3 sections / 80 metres total left").
- Configurable arrival radii by target type (analogous to the existing
  `AutoWalkPlaceStopRange`/`AutoWalkTransitionStopRange` in `Configuration.cs`).
- A standalone, separate feature (not part of the path guidance itself):
  proximity warning about people/monsters in the viewing direction via an
  `ObjectTable` scan - medium effort, see also the related suggestion in
  `docs/verbesserungsvorschlaege.md` ("Several simultaneous signature tones").
- A warmer/colder supplementary signal for path fidelity (continuous feedback
  on how close the player stays to the calculated line, not just to the
  waypoint) - more of a polish item than a necessity, since the perpendicular
  distance check from the MVP already provides the basic safeguard.
