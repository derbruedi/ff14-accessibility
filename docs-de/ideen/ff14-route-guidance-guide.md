# Waypoint Routing and Route-Aware Guidance
## An implementation guide for FF14Accessibility, from the KOTOR accessibility mod

**From:** Voice of the Old Republic (KOTOR 1 accessibility mod) — https://github.com/JeanStiletto/voice-of-the-old-republic
**To:** the FF14Accessibility project (derbruedi/ff14-accessibility)
**Date:** 2026-07-15
**Status:** design guide — companion to `ff14-room-shape-guide.md`. Written
against your `AutoWalkService.cs` / `NavigationService.cs` and vnavmesh's
IPC surface (master, July 2026). We read your STATUS.md; we have not run
your plugin. Where we describe our own system, that part is shipped and
user-tested in our mod; where we adapt it to your stack, it is a design
suggestion — marked as such.

---

## 1. What this document is, and why we think it helps

Your auto-walk (vnavmesh drives the character) works. Your manual **walk
guide** — beacon plus "Distanz, Richtung" every 2 seconds — has a
structural limit we recognize, because our first guidance prototype had
exactly the same one: **it points in a straight line at the final target.**
Around a corner, the beacon pans toward a wall; the spoken direction sends
the player into geometry; the distance readout stalls or grows while the
player is actually making progress along the real path. The player either
gives up and switches to auto-walk, or bumps along walls until the
straight line happens to be clear.

Our mod solved this with a **route-aware guidance system**: compute the
waypoint path first, then run all guidance — beacon, spoken directions,
progress, arrival — against the *next waypoint of the route*, not the
final target. This is the single change that turned our walk guide from a
demo into the feature our users actually navigate with.

The crucial realization for your stack: **you already have everything
this needs.** `Nav.Pathfind(from, to, fly)` returns a waypoint list
*without moving the character* — pathfinding and movement are separable
in vnavmesh's IPC. Your `AutoWalkService` even reads `Path.ListWaypoints`
already, for diagnostics. This guide is about pointing your existing
guidance UX at that data. No navgraph, no new geometry code.

We describe our system honestly, including which parts exist because of
engine constraints you don't have, so you can skip those.

---

## 2. Our system in one page (context for the hints)

Our guidance pipeline ("Pillar 3") has four parts:

1. **Route computation.** We run our own A* over the engine's per-area
   nav graph, then a string-pulling smoothing pass against walkmesh walls
   to remove unnecessary corner waypoints. — *You skip this entire part:*
   `Nav.Pathfind` does both jobs (Detour's funnel algorithm already
   string-pulls). This is engine-constraint code, not design.
2. **Route preview speech.** Before or as movement starts, we speak the
   whole route as merged turn-by-turn segments: "Weg zur Einfachen
   Sicherheitstür, 38 Meter: 20 Meter Nord, dann 15 Meter Ost." Section 4.
3. **The waypoint-chasing beacon.** A repeating positional audio cue at
   the *next waypoint*, arrival cues per waypoint, automatic advance, and
   a fresh spoken segment after each waypoint. Section 5.
4. **The approach tracker.** One state machine that watches every
   dispatched walk (auto or assisted) and resolves it to *arrived*,
   *blocked*, or *cancelled by the player* — with the movement-liveness
   and key-cancel subtleties in section 6.

Parts 2, 3 and 4 transfer to you nearly unchanged and are the substance
of this guide.

---

## 3. The core architectural move

Restructure guidance around one shared object:

- **Route** = the waypoint list from `Nav.Pathfind` (or
  `PathfindCancelable`), plus a **cursor** (index of the next unreached
  waypoint).

Then your two existing modes and one new capability all consume it:

- **Auto-walk** (unchanged mechanics): vnavmesh drives, but you can now
  *narrate the route* — preview at start, optional "turning east" style
  progress — instead of only counting down meters. Cheap win, section 4.
- **Walk guide** (the big upgrade): beacon and spoken direction chase
  `route[cursor]` instead of the target. Section 5.
- **Route preview on demand** (new, nearly free): a hotkey that speaks
  the turn-by-turn summary for the currently selected navigation object
  *without walking at all*. Our users use this constantly to build a
  mental map before deciding whether to walk manually or auto-walk.

Practical IPC notes (from reading vnavmesh source, verify at runtime):

- Pathfind calls are **async/queued** (`Nav.PathfindInProgress`,
  `Nav.PathfindNumQueued`, `PathfindCancelable` with a token). Treat
  "route requested" and "route ready" as separate states; announce
  "berechne Weg" only if it takes noticeably long.
- A pathfind is per-mesh, i.e. per zone. Cross-zone journeys remain what
  they are in your auto-walk today: leg-by-leg via your transition
  handling. Guidance below is per-leg.
- For quest markers without height you already have the three-tier
  `ResolveFloorPoint` fallback — reuse it unchanged to produce the
  pathfind goal.

---

## 4. Route preview speech (shipped design, transfers verbatim)

The raw waypoint list is unusable as speech — even a funnel-smoothed path
has more corners than a human wants to hear. Our segment builder
compresses it; the algorithm is small and we recommend copying it as-is:

1. Walk the waypoint list pairwise; each hop is a 2D displacement.
2. Classify each hop into a **compass sector** (we use 8 sectors: N, NE,
   E, ...). 16 is too chatty for route speech.
3. **Fold consecutive hops in the same sector into one segment**,
   summing distances.
4. **Hops shorter than 1 m never become segments.** Accumulate them in a
   pending bucket and merge the bucket into the next real segment. This
   rule is what keeps mesh jitter and door-threshold micro-corners out
   of the speech.
5. Format: "{Meter} Meter {Richtung}", joined with "dann". Prefix a
   header with target name and total distance. Round meters — decimals
   are noise.
6. Cap the spoken segment count (we effectively speak 3–5). For longer
   routes say "…, dann weiter" — the walk guide will speak the rest as
   the player progresses; the preview only needs to orient.

Result: "Weg zu Ätheryt, 62 Meter: 25 Meter Nord, dann 30 Meter Nordost,
dann weiter."

Where to hook it in your plugin, in order of value:

- On walk-guide start (section 5) — mandatory, it is the route contract
  the beacon then fulfills.
- On auto-walk start — replaces nothing, adds orientation. Keep your
  3-second "Noch X Meter" during the drive; the preview runs once.
- On a "preview route" hotkey against the current navigation selection —
  pathfind, speak, discard. No movement. (Suggested adaptation; in our
  mod this is fused with the target-announce flow.)

**Compass vs. relative ("links/rechts") — a decision we tested:** route
*previews* use compass. Relative directions are meaningless three
segments ahead (relative to which heading?), and your users already hear
compass bearings from your `DirectionText`. *Live* steering speech during
the walk guide uses relative-to-heading, because there the player's
current facing is the reference that matters. Mixing the two — compass
for plans, relative for the next action — sounds inconsistent on paper
and is exactly what our blind users preferred in practice.

---

## 5. The waypoint-chasing walk guide (the main upgrade)

This replaces the straight-line logic inside your existing walk-guide
mode. The player still walks manually — that is the point of the mode —
but every guidance signal now refers to `route[cursor]`.

### 5.1 State and lifecycle

- On activation: pathfind to the target, speak the preview (section 4),
  set cursor to the first waypoint that is not already behind the player,
  start the beacon.
- Own the route; re-pathfind only per the drift rule (5.4).
- Disarm on: final arrival, zone change, player death/duty events, mode
  toggle, or auto-walk taking over (you already have the quiet-handoff
  pattern — keep it).

### 5.2 Beacon and per-waypoint cues (shipped design)

- The repeating beacon tone is **3D-positional at the next waypoint** —
  this is what makes corners audible: approaching a corner, the beacon
  sits at the corner; the moment the waypoint is reached it jumps to pan
  toward the new leg. Our users steer by that jump alone.
- We boost the beacon's gain well above our normal cue level (ours runs
  at double our standard cue gain) so it stays audible 15–25 m out
  against distance falloff. Whatever your audio backend, expect to need
  a louder-than-normal category for this one cue.
- On reaching a waypoint: a **non-positional, centered arrival cue** —
  a different, short sound. Positional makes no sense (the player is
  standing on it) and centered is always audible. Then advance the
  cursor and immediately speak the next segment.
- On reaching the final waypoint: a distinct arrival cue, spoken
  arrival, disarm. You already have arrival phrasing from auto-walk —
  reuse it so both modes end identically.

### 5.3 Spoken guidance is event-driven, not timer-driven

Your current 2-second "Distanz, Richtung" timer talks constantly and
says the same thing eleven times along a straight corridor. Ours speaks
on **events**:

- After each waypoint reach: one line, next segment, relative to the
  player's heading: "15 Meter, leicht links." (Your `RelativeAngle` /
  `DirectionText` already produce exactly this — feed them the waypoint
  instead of the target.)
- A slow fallback repeat (5–8 s) only while the player is between
  events, for reassurance on long legs — and consider making even that
  optional; the beacon already carries the direction continuously.

This single change cuts speech volume by roughly two thirds while
increasing the information per utterance. It was one of the
highest-praised adjustments in our mod's guidance history.

### 5.4 The three robustness rules (suggested adaptations)

Our engine walks the player natively between waypoints, so our shipped
code does not need all three of these; a manually steered player does.
We flag them because every one addresses a failure you will otherwise
meet in the first hour of testing:

- **Reach radius:** a waypoint counts as reached within ~2–3 m. Exact
  arrival is impossible on foot and the funnel corners sit tight against
  walls — a too-small radius strands the cursor forever at a corner the
  player already turned.
- **Skip-ahead:** each tick, check not just `route[cursor]` but a couple
  of waypoints beyond; if the player is within reach radius of a *later*
  waypoint (or measurably closer to `cursor+1` than `route[cursor]` is),
  advance past the intermediate ones silently. Without this, a player
  who cuts a corner is told to walk backwards.
- **Drift re-route:** if the player's distance to the current route
  segment exceeds ~10 m, quietly re-pathfind from the current position
  and splice in the new route (speak one line only if the new first
  direction differs from what was last announced). Players explore,
  dodge mobs, get knocked around; the guide must follow them, not scold
  them.

### 5.5 Why keep a manual mode at all

Worth stating in your docs, because "auto-walk works, why bother?" is a
fair question — we asked it ourselves. Our users' answer: auto-walk
teaches nothing. A player who is driven somewhere cannot find the way
back, cannot estimate distances, and does not build the survey knowledge
sighted players absorb passively. The walk guide is how blind players
*learn a dungeon*; auto-walk is how they commute through it. Both modes
consuming the same route (and speaking the same preview) means switching
between them costs no re-orientation — the route heard during auto-walk
is the same route the walk guide would have called out.

---

## 6. The approach tracker (one state machine for every walk)

We consolidated three independent "is this walk still alive?" observers
into one, after they disagreed with each other in the field. Yours are
`AutoWalkService`'s stuck/arrival logic today, plus whatever the walk
guide grows. Recommendations from that consolidation:

- **Liveness is player movement, never pathfinder state.** Your V4.61
  fix ("Komme nicht näher" false abort — measure actual movement, not
  goal-line distance) discovered this independently; we confirm it is
  the right axis and worth promoting to *the* rule: our engine's action
  queue transiently reads empty between waypoints, and vnavmesh's
  `Path.IsRunning` has its own timing — position deltas are the only
  signal that never lies.
- **Every walk resolves to exactly one of: arrived, blocked, cancelled.**
  One shared announcement per outcome, used by both modes. Our blocked
  line speaks the target name plus its **live** distance and direction —
  so the stuck player immediately has what they need to try manual
  steering or pick another route. Given your known mesh-defect stall
  (the "18 m before the target" case), this phrasing turns an opaque
  failure into an actionable one: "Weg blockiert. Ausgang Tiefer Wald,
  18 Meter, Nordost." The player can walk the last stretch by beacon.
- **Movement-key cancel needs a rising-edge gate and an arm-time grace.**
  The player pressing W/A/S/D during a guided or auto walk must reclaim
  control (cancel + one line: "Bewegung abgebrochen"). Two traps we hit,
  in both cases shipped fixes: a movement key *already held* when the
  walk starts must not instantly kill it — only a fresh press counts
  (rising edge); and even a fresh press within the first few hundred
  milliseconds should be ignored (grace window), or key-repeat from the
  triggering hotkey chord cancels the walk it just started.
- **Success detection has more than one shape.** Ours accepts "settled
  within reach of the target" but also "the interaction the walk was
  for actually happened" (a dialog opened, a container opened). For you:
  reaching an aetheryte's interaction range, a quest marker's trigger
  firing, or the duty objective updating are better arrival signals than
  pure distance — accept whichever comes first.

---

## 7. FFXIV-specific risks we could see from outside

- **Flying and swimming.** `Nav.Pathfind`'s `fly` flag produces 3D
  routes. The segment builder in section 4 is 2D; for flight legs add a
  vertical qualifier when a segment's climb angle is significant
  ("30 Meter Nordost, aufwärts"). The walk-guide beacon works unchanged
  in 3D if your audio cue is truly positional; the relative-direction
  speech needs an up/down word.
- **Waypoint density.** We don't know how dense vnavmesh's returned
  waypoint lists are in practice (Detour funnel output is usually
  sparse, but mesh detail varies). The 1 m pending-bucket rule and the
  8-sector fold in section 4 are your defense; if lists turn out huge,
  decimate before segment-building rather than after.
- **Moving targets.** Your bestiary/monster tracking guides toward
  entities that walk around. Re-run the drift rule (5.4) against the
  *target's* position too: if the target has moved more than ~10 m since
  the route was computed, re-pathfind. For fast movers, fall back to
  your current straight-line mode — a route to where something *was* is
  worse than a bearing to where it *is*.
- **Server-authoritative knockbacks/teleports** (duty mechanics) look
  like massive drift. The drift re-route handles them for free — but
  suppress the guide entirely in active combat; nobody wants routing
  speech during a mechanic.

---

## 8. Suggested build order

1. Route preview speech (section 4) over `Nav.Pathfind` results, on a
   hotkey and at auto-walk start. No movement code touched; immediately
   testable by ear against zones you know. This alone ships value.
2. Walk guide re-target: beacon and direction speech onto
   `route[cursor]` with reach radius + advance. (Sections 5.1–5.3.)
3. Skip-ahead and drift re-route (5.4) — add after first real walks show
   the failure modes; they will.
4. Unify arrival/blocked/cancel announcements across auto-walk and walk
   guide (section 6), migrating your existing stuck detection into it.
5. Flight/3D qualifiers and moving-target handling (section 7) last —
   they refine, not enable.

As with the room-shape guide: our thresholds (1 m bucket, 2–3 m reach,
~10 m drift, 5–8 s fallback repeat, 8 sectors) are starting points that
survived our tuning rounds in KOTOR's corridor scale. FFXIV's spaces are
larger; expect to scale some of them up, and log every guidance decision
so your users' reports map onto grep-able evidence. Questions welcome —
open an issue on our repo.
