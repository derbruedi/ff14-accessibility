# external-advice — NOT KOTOR-specific

Documents in this folder are **advice written for other accessibility
projects**, not documentation of this mod. They live here only because a
separate repository for occasional short write-ups is not worth the
overhead.

Rules for this folder:

- Nothing in here describes the current state of the KOTOR mod; do not
  cite these files as design references for our own code.
- Contents are hand-delivered to the target projects by the maintainer;
  do not link them from the main docs index or the changelog.
- Each document names its target project and the date/state of the
  external code it was written against.

Current contents:

- `ff14-room-shape-guide.md` — for derbruedi/ff14-accessibility: room-shape
  announcements on a navmesh (skeleton/medial-axis approach, ray-probe
  fallback, wall-proximity cues, speech-layer rules).
- `ff14-route-guidance-guide.md` — for derbruedi/ff14-accessibility:
  waypoint routing, route preview speech, and a route-aware walk guide
  built on vnavmesh's pathfind IPC.
- `ff14-discovery-filter-guide.md` — for derbruedi/ff14-accessibility:
  discovery-driven default filtering for the object cycle (stable keys,
  per-character persistence, organic-only recording, extended toggle).
- `ff14-small-hints.md` — for derbruedi/ff14-accessibility: four small
  independent suggestions (passive callouts, map flag as target,
  frozen-position targets, installer log collection).
