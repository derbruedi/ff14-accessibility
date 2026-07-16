# Room-Shape Announcements on a Navmesh
## An implementation guide for FF14Accessibility, from the KOTOR accessibility mod

**From:** Voice of the Old Republic (KOTOR 1 accessibility mod) — https://github.com/JeanStiletto/voice-of-the-old-republic
**To:** the FF14Accessibility project (derbruedi/ff14-accessibility)
**Date:** 2026-07-15
**Status:** design guide — based on reading your `NavigationService.cs` / `AutoWalkService.cs` and the vnavmesh IPC surface (awgil/ffxiv_navmesh, master as of July 2026). We have not run your plugin; treat concrete API details as "verified by reading source", not "verified at runtime".

---

## 1. What this document is

Our mod ships a feature we call **area descriptions**: when a blind player
enters a distinct walkable space, the mod speaks one short line describing
its shape and its exits, for example:

- "Area. Exits north and east."
- "Area, elongated north–south. Exits north, south and west."
- "Corridor heading east."
- "Dead end."

Players tell us this is one of the highest-value navigation features we
have: it replaces the single most important thing sighted players get from
one glance at the screen — "what kind of space am I in and where can I go".
Your plugin already covers *objects* (what is near me) and *transport*
(auto-walk); this feature covers *space* (where am I).

This guide describes how to build the same feature on top of what you
already have — the live object table plus vnavmesh — **without building or
persisting any navgraph of your own**. It also passes on the failures we
paid for so you can skip them.

---

## 2. Lessons we paid for (read this before the algorithm)

We went through three failed or discarded iterations before our shipped
system. The distilled lessons transfer directly to you:

- **The shape signal lives in the local geometry around a sample point.**
  Not in wall identity, not in connectivity. Our first attempt detected
  "wall pairs" (two close parallel walls = corridor) — it failed because
  real corridors are too wide and real rooms have parallel walls. Our
  second attempt flood-filled connected walkable faces into cells — it
  failed because everything connects through doorways (17 rooms merged
  into 1 cell). Both approaches ignored the local neighbourhood of the
  player, which is where the answer actually is.
- **Do not build a navgraph for this.** We ultimately classified shape
  using BioWare's hand-placed AI nav points — but only because that data
  already existed in the engine and was designer-validated. FFXIV exposes
  no such authored graph to you, and deriving one from vnavmesh's Recast
  mesh would re-do segmentation work Recast has already done, badly.
  Everything below works from geometry queries at announce time; nothing
  is precomputed or persisted per zone.
- **The speech design matters more than the geometry.** Whatever backend
  you choose, most of the user-facing quality comes from the announcement
  rules in section 7. We tuned those with a blind developer over multiple
  releases; they are the most transferable part of this document.

---

## 3. What your stack gives you (and what it doesn't)

Verified against vnavmesh's `IPCProvider.cs` (master, July 2026):

**You have:**
- `Nav.BuildBitmap(Vector3 startingPos, string filename, float pixelSize)`
  and `Nav.BuildBitmapBounded(..., Vector3 minBounds, Vector3 maxBounds)` —
  rasterize the navmesh into a walkable/blocked bitmap. **Note: this
  writes a .bmp file to the given path on disk; it does not return pixel
  data.** The bounded variant lets you clamp the Y range, which is how
  you keep other floors of a multi-level dungeon out of your grid.
- `Query.Mesh.IsPointOnMesh(Vector3 p, ...)` — point-walkability test.
  Dalamud IPC is an in-process delegate call, so sampling a few thousand
  points per announcement is cheap. This is your disk-free alternative to
  the bitmap.
- `Query.Mesh.NearestPoint`, `Query.Mesh.PointOnFloor` — you already use
  these in `AutoWalkService`.
- `Nav.IsReady` / `Nav.BuildProgress` — you already gate on these; the
  shape system must gate on them too.

**You do not have (over IPC):**
- No raycast against the mesh or against collision.
- No access to the Detour polygons, edges, or wall geometry.

So both algorithms below are built purely from **walkability sampling on a
local 2D grid** around the player. That is enough.

(If you ever need true 3D wall rays instead: Dalamud plugins can call the
game's own collision raycast via ClientStructs' `BGCollisionModule`, as
vnavmesh itself does. We recommend against starting there — it hits render
collision, not walkable space, and answers a subtly different question.)

---

## 4. Recommended implementation: skeleton analysis on a local walkability grid

This is the approach we recommend you build directly. In the robotics /
computational-geometry literature it is medial-axis (Generalized Voronoi
Diagram) room segmentation. On raw 3D geometry it is genuinely hard — which
is why we never built it for KOTOR. **On a rasterized grid it collapses to
two standard image-processing passes**, and vnavmesh hands you the raster
for free. You are in the rare position where the "fancy" solution is also
a reasonable amount of code.

### 4.1 Acquire the local grid

- Center: the player's position snapped to the mesh
  (`Query.Mesh.NearestPoint`).
- Extent: roughly 60–80 m square. Big enough to see a whole room and its
  doorways, small enough to stay cheap and local.
- Resolution: ~0.5 m per cell → a 120×160-ish boolean grid.
- Y bounds: player Y ± ~5 m via `BuildBitmapBounded`, so overlapping
  floors, balconies and bridges don't bleed into the grid. This is the
  single most important correctness parameter in FFXIV dungeons.

Two ways to get the pixels, pick whichever proves cleaner:

1. **`Nav.BuildBitmapBounded` + read the .bmp back.** One IPC call, one
   small file write/read in your plugin config directory, then delete.
   Fine at announcement cadence (seconds apart), ugly if called per frame
   — don't call it per frame.
2. **Grid-sample `Query.Mesh.IsPointOnMesh`** over the same extent. No
   disk. ~20–40k in-process calls per rebuild; measure it, but we expect
   single-digit milliseconds. If it's fast enough, prefer this — no file
   format parsing, no IO, explicit control of the Y tolerance per sample.

Cache the grid and rebuild only when the player has moved more than ~10 m
from the grid's center, or on zone change, or when `Nav.Rebuild`/`IsReady`
cycles. Rebuild on a background task; never block the framework thread.

### 4.2 Distance transform

Compute, for every walkable cell, the distance to the nearest blocked
cell (standard two-pass chamfer or exact Euclidean distance transform;
either is fine at this resolution and both are textbook algorithms).

This map alone already answers questions:
- Distance at the player's cell ≈ half the local passage width.
- Large stable distance values = open space.
- The **ridges** of the distance map are the medial axis.

### 4.3 Skeletonize

Thin the walkable region to its 1-cell-wide skeleton (Zhang–Suen or
Guo–Hall thinning — both are ~40 lines of C# and well documented), or
equivalently extract the ridge cells of the distance transform. Then
prune stub branches shorter than ~2 m; raster skeletons always grow
hairs into corners and they are pure noise.

The skeleton *is* the topology of the space, computed fresh from local
geometry with zero authored data:

- A skeleton cell with **2 neighbours** lies on a corridor centerline.
- A skeleton cell with **3+ neighbours** is a junction.
- A skeleton **endpoint** (1 neighbour) is a dead end — unless it exits
  the grid border, in which case it is a passage leading out of view.

### 4.4 Classify the player's space

Find the skeleton point nearest the player, then walk the skeleton
locally:

- **Corridor**: player's skeleton segment runs between two nodes without
  branching, and the distance-transform value along it is small
  (passage width below ~8 m — tune). Speak the axis (from the segment
  direction) and nothing else: "Corridor heading north."
- **Junction**: nearest node has degree ≥ 3 within a few meters. Speak
  the branch bearings: "Junction. Paths north, east and south."
- **Dead end**: player is near a pruned-safe endpoint whose branch does
  not reach the grid border: "Dead end."
- **Room / open area**: distance value at and around the player is large
  relative to passage width, or several short skeleton branches cluster
  inside one blob. Compute the walkable blob containing the player (one
  flood fill on the grid — safe here because the grid is local, unlike
  our failed global flood fill), take its principal axis, and speak per
  section 7.
- **Exits of a room**: walk the blob's boundary; every maximal run of
  walkable boundary cells wider than ~1.5 m that connects to walkable
  space outside the blob (or leaves the grid) is an exit. Report each as
  a compass direction from the blob centroid. Merge exits closer than
  ~30° apart.

Everything above is one grid, one distance transform, one thinning pass,
one flood fill — all local, all rebuilt in milliseconds, nothing stored.

### 4.5 Known risks (we could not test these from outside)

We reviewed your code and vnavmesh's source, but we have not run this in
FFXIV. Watch for:

- **What the bitmap actually rasterizes.** Confirm which mesh layer /
  area flags `BuildBitmap` includes (e.g. whether water or fly-only
  areas count as walkable) before trusting it. One evening with the .bmp
  output open in a diff-able form (or logged as ASCII art — that's how
  our blind developer reviews grids) settles it.
- **Y-slice artifacts.** Ramps and spiral stairs can enter/leave the ±5 m
  slice mid-grid and read as fake walls. If dungeons with big vertical
  motion misbehave, widen the slice locally or follow the player's Y.
- **Outdoor mesh noise.** Recast meshes of organic terrain produce ragged
  boundaries; the skeleton of a forest is spaghetti. Section 8's gating
  keeps you out of that regime entirely.
- **Skeleton instability between rebuilds.** Two grids built 10 m apart
  can skeletonize slightly differently. The debounce rules in section 7
  exist precisely to keep that from reaching speech.

If any of these bite harder than expected, drop to the fallback below —
it is strictly simpler and shares the same speech layer, so nothing above
it needs to change.

---

## 5. Fallback: the ray-march probe (cheaper, less expressive)

This is what our shipped classifier's probe does, adapted to your
primitives. Build it if the skeleton approach hits real-world problems,
or build it *first* in a day as a de-risking prototype — the two share
the grid acquisition and the entire speech layer, so no work is wasted.

- From the player's mesh-snapped position, march 16 rays (every 22.5°)
  outward through the walkability grid (or directly via repeated
  `IsPointOnMesh` samples), step 0.5 m, max ~40 m.
- Record the free distance per direction.
- Classify the 16-distance pattern:
  - Two opposite long directions, the rest short → **corridor** along
    that axis.
  - Three or more long directions separated by short ones →
    **junction**, speak the long bearings.
  - One long direction, rest short → **dead end** behind / passage ahead.
  - Most directions long → **open area**; elongation from the ratio of
    the longest opposite-pair sum to the perpendicular pair.
- Exits fall out of the same pattern: each maximal group of adjacent
  long directions is one exit bearing.

Limitations versus the skeleton (why it is the fallback and not the
recommendation): it only sees what is ray-visible from the player's
current position, so an L-shaped room reads differently from each end,
exits hidden behind a pillar are missed, and it cannot describe the room
as a whole — only the view from here. Published accuracy for this class
of local-feature classification is around 92%; the failure cases are
exactly the interesting rooms.

---

## 6. A cheap side product of the grid: wall-proximity cues

Once the local walkability grid exists, obstacle awareness while walking
costs almost nothing extra: each tick, sample the grid's clearance to the
player's **left, right and ahead** (relative to heading, short range —
3 to 5 m). A wall appearing beside the player, or an opening appearing in
a wall they were following, becomes one short panned audio cue. The rules
that keep it from being noise — all learned the hard way in our mod:

- **Cue on change only, never continuously.** A wall that stays beside
  you is silence. Continuous proximity tones fatigue users within
  minutes.
- **Quantize distances into two or three bands** (near / mid) before
  comparing, so centimeter jitter can't retrigger the cue.
- **Debounce:** a changed state must persist a few hundred milliseconds
  before it sounds; corners and doorway thresholds otherwise fire bursts.
- Ship it **as a toggle**. Honest note: in our mod this is one of the
  less-loved features — some users navigate by it, others switch it off
  on day one. The shape announcements and route guidance carry more
  value; treat this as seasoning, and don't let it delay the main
  feature.

---

## 7. The speech layer (transferable verbatim — this is the real product)

These rules came out of iterative tuning with a blind developer. They
apply unchanged regardless of which geometry backend produced the labels.

- **One neutral noun.** Speak "Bereich" / "area" for every room-like
  space. Never guess taxonomy ("hall", "chamber", "plaza") from geometry
  — wrong guesses cost trust, and the exits carry the useful information
  anyway. Corridor / junction / dead end are the only shape words that
  earn their place.
- **Long axis only when clearly elongated.** We speak an axis
  ("elongated north–south") only when the principal-axis ratio exceeds
  ~1.8. Below that, shape words are noise.
- **Never speak size.** We tried "large area", "small room" — our users'
  verdict: nobody cares. Exits and axis are what players act on.
- **Exits after the shape, as compass directions.** "Area, elongated
  east–west. Exits east, south and west." Cap the list (we cap at 4);
  beyond that say "several exits".
- **Small spaces announce immediately, large spaces announce delayed.**
  In a corridor or small room the player needs the information now; in a
  big open space an instant announcement fires while they are still
  crossing the doorway and gets talked over by object announcements.
- **Stability debounce — do not skip this.** Keep `lastSpoken`,
  `pendingLabel`, `pendingSince`. A new classification only becomes
  `pending`; it is spoken only when it has stayed stable for a quiet
  window (we use ~1 s) *and* differs from `lastSpoken`. This single
  pattern absorbs skeleton flicker, doorway-threshold oscillation, and
  camera-turn jitter. Without it the feature is unshippable; with it,
  even a mediocre classifier feels solid.
- **Suppress during auto-walk.** Your `AutoWalkService` already
  suppresses target-change announcements while active (`IsActive`); route
  shape announcements through the same gate. A player being driven
  through five rooms does not want five descriptions; announce once on
  arrival.
- **Log every verdict, speak only the stable ones.** Before wiring the
  classifier to speech at all, run it silently for a while and log
  position + verdict each time it *would* have spoken. Grep the log
  against known dungeons. This is how we validated ours without any
  sighted checking: the log is the ground truth channel, speech is the
  product. (Your STATUS.md workflow suggests you already work this way.)

---

## 8. FFXIV-specific gating

KOTOR is corridor architecture everywhere, so we announce everywhere.
FFXIV is not. Recommendation:

- **Enable the full shape system indoors and in duties**: dungeons,
  trials, raids, guildhalls, inn rooms, residential interiors. This is
  where "corridor / junction / room with exits" is both accurate and
  valuable — and conveniently where your users most need independent
  navigation (duty finder groups won't wait).
- **In the overworld, stay off by default.** The medial axis of open
  terrain is meaningless. At most, reuse the distance transform for two
  cheap signals: "open terrain" (large clearance in all directions) and
  "boundary ahead" (clearance collapsing in the movement direction —
  cliff edge, zone wall, shoreline). Your existing POI bearings +
  quest markers + auto-walk already cover overworld navigation better
  than shape ever could.
- Territory intended-use data (the `TerritoryIntendedUse` sheet field
  you likely already consult for other features) gives you the
  indoor/duty gate without heuristics.

---

## 9. Suggested build order

1. Grid acquisition + ASCII-art grid logging (validates bitmap semantics
   and Y-slicing on their own, before any classification exists).
2. Distance transform + skeleton + silent classification logging in two
   or three familiar dungeons.
3. Speech layer with debounce, gated to duties, behind a config toggle.
4. Tune thresholds (corridor width cap, elongation ratio, exit merge
   angle) from user feedback — expect two or three rounds; we needed them.
5. Only if step 2 shows systematic skeleton problems: swap the classifier
   for the section-5 ray probe and keep everything else.
6. Optional, last: the wall-proximity cues (section 6) — they reuse the
   stable grid directly and should never come before the main feature.

Questions, or want the tuning history behind any threshold above — open
an issue on our repo or reach out. We are two projects solving the same
problem in two engines; the more of this that converges, the better for
the players.
