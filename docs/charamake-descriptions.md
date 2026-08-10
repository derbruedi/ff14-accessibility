# Appearance descriptions — the batching plan

Written 2026-08-08. Field list closed by the user the same day:
*"ear clasps, and that appears to be all the fields that will need descriptions.
will do one final pass when we're done batching."*

**Status 2026-08-09, after the finishing pass: ALL SIX ICON MENUS ARE COMPLETE —
1,943 descriptions, 1,943 distinct, no duplicates.** All in
`FF14Accessibility/Services/CharaMakeIconText.cs`, German first with English through the
same `Loc.IsGerman` switch every other local string uses. Full write-up in
`

- **Face — 132/132**, all 32 rows. COMPLETE. (batch 1)
- **Tail Shape — 64/64**, all 12 rows. COMPLETE.
- **Fur Pattern — 20/20**, all 4 Hrothgar rows. COMPLETE.
- **Ear Shape (Viera) — 16/16**, all 4 rows. COMPLETE.
- **Hairstyle — 879/879** (1551/1551 slots), all 32 rows / all 18 blocks. COMPLETE.
- **Face Paint — 832/832** real icons, all 32 rows. COMPLETE. `verify.py` prints
  `832/833` and `0/32 rows COMPLETE` — **that IS the finished state**, see "Face Paint"
  below: id 2401 is entry 1 "no paint", its icon is 0, and registering 0 is forbidden on
  purpose. Do not "fix" the count.

**The other two families, updated 2026-08-09** — neither belongs in `CharaMakeIconText`:

- **Type 0** (Jaw, Eye Shape, Eyebrows, Nose, Mouth, Fang Length, Elezen/Lalafell Ear
  Shape) is **BUILT and MEASURED**: `Services/CharaMakeShapeText.cs`, 2,509 entries,
  generated from the face models' own vertex deltas. See "Family C" below.
- **Type 4** (Facial Features, Tattoos, Limbal Ring, Other Features, Ear Clasps) is
  **UNBLOCKED and 910/924 AUTHORED**, all eight races, in `CharaMakeIconText.cs`. The
  "5 + 2 + 5 = 12 against 7 slots" contradiction was arithmetic on a row that does not
  exist. **The strings are not reachable yet** — the mod has no path from a type-4
  toggle to its icon until the bit question below is answered in game. See "Family C".

Every entry from batch 2 onward also carries a **one-or-two-word summary** for the cursor
move (`S(...)` instead of `F(...)`), which is what asked for and what
`CharaMakeIconText.Summarize()` serves. Face entries have no summary and therefore fall
back to the full text — deliberately, until test item 1 is answered.

### READ THIS BEFORE AUTHORING ANYTHING: entry order is NOT icon order

This shipped as a real defect and survived a spot-check. Three hairstyle blocks (125
entries) had every description attached to the **wrong picture**, because the contact
sheets labelled each cell with the **entry number** while the table is keyed by **icon
id**, and the two orders differ — Hrothgar interleaves two id runs outright (entry 1 =
`137001`, entry 2 = `137009`, entry 3 = `137002`), and Hyur Midlander male puts icon
`131002` at entry 13. Full account in `
must stay:

1. **Sheets label cells `<entry> #<iconId>`.** The id is copied from the index, never
   inferred from position. Do not go back to entry-only labels.
2. **`verify.py` has an entry-order check** — it flags a row whose ids appear in the
   source in sorted order rather than entry order. It is a WARNING meaning "this block
   was not laid out from the sheet, go look", not a verdict. All 32 rows pass today.
3. The Hairstyle section comment in `CharaMakeIconText.cs` records the trap.

**A spot-check of the distinctive entries does not clear a block.** Four entries were
sampled on the Midlander block and passed — the shaved scrollwork, the buzz cut, the bald
head with a crown tuft, i.e. exactly the ones that are hardest to misdescribe. The plain
cuts were the wrong ones.

### Two measured facts that shape the rest of the work

1. **The game names none of them.** All 879 hairstyle and all 833 face-paint
   `CharaMakeCustomize` params have an **empty `Hint` and an empty `HintItem`**, and
   none is `IsPurchasable` — `cmdump names Hairstyle` / `names "Face Paint"`, added for
   exactly this check. A game-supplied name would have to beat authored text, so this
   had to be verified per entry rather than taken from summary. It holds.
2. **Hairstyle blocks never overlap.** The 32 rows collapse to **18 distinct icon
   sets** — both tribes of a race always share the list, except Hyur, where Midlander
   and Highlander genuinely differ — and the intersection between those 18 sets is
   **empty** (measured: Midlander male's 53 icons appear in no other row). So no
   description is reusable across races and coverage grows one block at a time. That is
   fine: one character sees only 27–54 hairstyles, so a finished block is *complete*
   coverage for the characters it belongs to.

### Hairstyle blocks — all 18 done

All 18 icon sets are authored, 879 icons over all 32 rows. Both tribes of a race always
share the list, except Hyur, where Midlander and Highlander genuinely differ; the
intersection between the 18 sets is empty, so nothing here was reusable and each block
was read on its own sheet.

**10 of the 18 blocks contain colour-variant clusters** — same mesh, differing only in a
baked ornament's colour (clips, hairpins, headbands, crown ornaments, a feather). These
are the ONE exception to the no-colour rule; see "Structural only" below for why, and
`
`133204/133214/133215/133216`, `132203/132213/132214/132215`,
`131201/131216/131217/131218`, `135003/135014/135015`, `134201/134213/134214`.

### Face Paint — 26 designs, not 833. Measured 2026-08-09

Face Paint is **not** shaped like Hairstyle, and the difference is worth knowing before
anyone budgets the remaining work:

- **32 rows × 27 entries = 864 slots, 833 params, 832 real icons.**
- **Entry 1 of every row is "no paint" and cannot be described here.** Its param is
  `2401` and `CharaMakeCustomize[2401].Icon` is **0**, so `CharaMakeReader` stores icon
  id **0** for it — and 0 is also what every **type-0** menu leaves in its `Icons`
  array. Registering a description against 0 would put it on every Jaw, Nose, Mouth and
  Eye Shape entry in the game. `cmdump`'s index shows `2401` there only because it falls
  back to the param when `Icon == 0`; that is a dumper artefact, not an icon id.
  Consequence for `verify.py`: a Face Paint row can never read `done`, because id 2401
  is in the index and can never be registered. `26/27` is the complete state.
- **The 32 rows' icon sets are pairwise DISJOINT** (measured: 32 sets of 26, union 832,
  zero intersections once the shared `2401` is dropped). So an id always identifies its
  row, exactly like Hairstyle.
- **But unlike Hairstyle, the DESIGNS repeat — now confirmed across all 32 rows.** The
  same 26 designs appear **in the same entry order** in every row, rendered on each race's
  own face. One catalogue; one string covers many rows' ids. This started as an
  expectation from four sets and was closed out by checking the rest: every tribe pair
  within a race and sex is byte-for-byte identical on all 26 entries (Elezen, Lalafell,
  Miqo'te, Roegadyn, Au Ra, Viera all zero differing pixels; Hrothgar female differs on 2
  entries by 0.23 %, antialiasing). **The only genuine split is Hyur, Midlander vs
  Highlander** — the same split Hairstyle has. So the 27 open rows collapsed to 15 visual
  checks, each read entry-by-entry against the reference row, with the confusable groups
  checked deliberately (2/3/4 are three different eyeshadows; 17 vs 19 are both broad
  diagonal bands; 5 vs 6 is edgeless flush vs hard-edged oval). All 15 matched.
- **Two things that are FACE, not paint,** and must not be read as designs: the Miqo'te
  cheek marking (present on every entry, including the plain one) and the Lalafell cheek
  dimple, which sits exactly where a cheek design goes.

How far that is proven, because the distinction is the same one Fur Pattern taught:

- **MEASURED, safe:** Helions vs The Lost is **0.00 %** of pixels differing by more than
  16, on all 26 entries, per sex — byte-for-byte identical renders. Au Ra Raen vs Xaela
  likewise 0.00 %.
- **NOT measurable, stated as a visual comparison:** male vs female scores 67 % and
  Hrothgar vs Hyur 82 %, but that is the base render. The control kills the metric for
  this question: two plainly *different* designs inside one row score **1.6–4.4 %**,
  because the paint is a few per cent of the pixels. The same-design claim across sexes
  and races therefore comes from looking at all 26 entries in three sets, and says so in
  the code.

Contact sheets: `mksheet.py` at `SHEET_SCALE=2 SHEET_COLS=9` (no crop) is enough to sort
the motifs, but **not** enough for the five subtle eye/cheek entries (2–6) — those were
read at `SHEET_SCALE=5..7` with a crop on the eye region (`55,25,150,95`) or the cheek
(`70,55,180,140`), nine icons per sheet so each one survives the viewer's downscale.

### The loop, kept for re-runs after a patch

Nothing in the six icon menus is open, so this is here for re-verification and for
whenever a patch adds entries — not as a to-do.

```powershell
dotnet build tools\charamake-dump\cmdump.csproj -c Release
$exe = "tools\charamake-dump\bin\Release\net10.0-windows\cmdump.exe"
& $exe icons "Hairstyle" | Out-File -Encoding utf8 idx-Hairstyle.tsv   # writes tools\icons\Hairstyle\*.raw
```

Then one contact sheet per row (`SHEET_SCALE=2 SHEET_COLS=9`, **no crop** — the face
crop cuts off hair, ears and tails), **labelled `<entry> #<iconId>`** (see the entry-order
trap above — this is not optional), look at it, and write one `S(brief, briefEn, full,
fullEn, iconIds...)` per entry. Note the dumper writes to `tools\icons\<Menu>` while
`sheets.py` looks under `tools\charamake-dump\icons\<Menu>`; the driver used in batch 2
just points `sheets.sheet()` at the real path.

For the **type-4** family the same loop runs with `featicons` instead of `icons`, and
the sheets come out one per (row, face) rather than one per row:

```powershell
& $exe featicons | Out-File -Encoding utf8 tools\icons\idx-Facial_Features.tsv
$env:SHEET_SCALE=2; $env:SHEET_COLS=7
python tools\charamake-dump\mksheet.py "Facial Features" tools\icons
```

**Verify before building:** `python tools\charamake-dump\verify.py tools\icons` — the
index directory is a REQUIRED argument; run with none and it reports every id as an
orphan, which looks like a catastrophe and is not one. A repeated icon id silently
overwrites the earlier description and the compiler cannot see it. The verifier extracts
every id from the `F(...)`/`S(...)` calls, reports duplicates, prints coverage per menu
and per row against the dumped index, and runs the entry-order check. Current state:
**1,943 registered / 1,943 distinct / no duplicates / 0 unknown ids / every row in entry
order.**

**`tools/icons/` is gitignored** (`.gitignore:37`), so the indexes and contact sheets
exist on this machine only; a fresh clone regenerates them with `cmdump icons <Menu>` and
`mksheet.py`.

**Entry order is not icon order** (Midlander male entry 13 is icon 131002, entry 17 is
131008) and **the two sexes can order the same shapes differently** (Viera ears: male
reads long-shaggy, drooping, long-smooth, short-shaggy; female reads short-shaggy,
long-shaggy, drooping, long-smooth). Both are why the table is keyed by icon id. An
index-keyed table would have swapped three of four descriptions on every female Viera.

### When a pixel comparison is valid, and when it lies

"These rows share a description" must be measured, not eyeballed — but the measurement
has to be of the thing being described.

- **Valid**, and used: Viera ears Rava vs Veena, 0.00 % of pixels differing by >16, with
  a working control (male vs female 18.2 %). Same for all four Miqo'te tail rows, Au Ra
  per sex, Hrothgar per sex.
- **Invalid**, and thrown away: Fur Pattern. Helions vs The Lost differ ~30 % — but that
  is the coat colour, which the descriptions deliberately exclude, so the number answers
  the wrong question. A high-pass "pattern mask" variant was built to remove the base
  tone and **failed its own control**: two visibly different patterns from the same row
  scored 72–76 % overlap against 62 % for the same pattern across tribes, because it was
  measuring the outline of the ruff rather than the markings. Discarded rather than
  reported. The fur sharing is a **visual** comparison and says so in the code.

Always run a control pair you already know differs. A metric that cannot tell those
apart cannot support a sharing claim either.

### What the thumbnails actually show, per menu

Worth knowing before choosing a crop, and one of them corrects an earlier note:

- **Hairstyle** — head and shoulders in three-quarter view. Readable at scale 2.
- **Tail Shape** — the tail ALONE against the vignette, no body. Shape, length,
  thickness and what sits at the tip are the whole content.
- **Fur Pattern** — the fur ruff only: **male renders show the chest from the front,
  female renders the shoulders and upper back.** Same five patterns either way. This
  **corrects /"The Lost's white coat shows no pattern at all"** — that was
  about the FACE icons and does not carry over; on the Fur Pattern icons all five
  patterns are plainly visible on The Lost.
- **Ear Shape (Viera)** — the ears and the top of the head. A face-height crop
  decapitates them.

Three findings from that first batch change what follows. All three are recorded in
full in `docs/game-api.md`:

1. **The type-0 menus are NOT undescribable after all.** Jaw / Eye Shape / Eyebrows
   / Nose / Mouth / Iris Size / Ear Shape / Fang Length are **shape keys on the face
   model** (`shp_chk_*`, `shp_eye_*`, `shp_brw_*`, `shp_nse_*`, `shp_mth_*`,
   `shp_irs_*`, `shp_etc_*`). Proven, not guessed: across all 18 body codes and every
   face model, the number of shapes carrying a prefix is exactly `SubMenuNum - 1`,
   entry 1 being the untouched base mesh. See "Family C" below, which is rewritten.
2. **The icons were being read with R and B swapped.** The "blue tint" this file
   used to give as the reason for the no-colour rule was an extraction bug
   (`TexFile.ImageData` is B8G8R8A8). The rule stands anyway — see below.
3. **Facial Features DO have icons**: `CharaMakeType.FacialFeatureOption` is 7 icon
   ids per face, numbered off the face's own icon. Family C is smaller than this
   file claimed.

## What this is for

Most Appearance menus can only be announced as a position. `Nose, Type 3, 3 of 6`
tells a blind player where the cursor is and nothing at all about the nose. The
game has no name and no description for these entries — that is not a gap in the
mod, it is a gap in the game data (checked: empty `Hint`, no `HintItem`; the only
named entries are aesthetician unlocks that are not offered during creation).

So the descriptions have to be **authored**. Two rules that follow from that:

- **Mark them as mod-authored.** They are not the game's words and must never be
  presented as if they were, in code, in docs, or in the announcement itself. As
  shipped: the class comment on `CharaMakeIconText` says so, and the Ctrl+F10
  appearance summary ends with `LocalStrings.CharaMakeAuthoredNote` — **but only
  when that summary actually used one**, and never on the per-arrow announcement,
  where repeating it would cost more than it informs.
- **Structural only — never colour.** Not because of a tint (that was a channel-swap
  bug, corrected 2026-08-08) but because the thumbnail is a **fixed preview render**:
  its hair, skin and eye colour are whatever the render used, not what the player
  picked. Those are separate menus, already named from the real palette by
  `Services/ColorNamer.cs`. So a description may talk about shape, length, width,
  parting and angle — and about skin/fur DETAIL, which is a different thing (below).
- **The ONE exception: a baked ORNAMENT's colour, by user decision 2026-08-09.** In 10
  hairstyle blocks there are clusters of entries that are the same mesh differing only in
  the colour of a clip, hairpin, headband, crown ornament or feather. Proven the strong
  way: the **alpha channel is byte-identical** across each cluster, so the silhouette is
  the same and only the hue moves. The no-colour rule exists because the render's *hair*
  colour is not the player's choice — that rationale does not reach an ornament whose
  colour is baked per entry and is the only thing a sighted player picks on. Such an
  entry is otherwise indistinguishable in words.
  - **The colour word is MEASURED, never impressionistic** — hue/saturation/value over
    the most saturated pixels of the differing mask. This changed three of them: a clip a
    first pass called "pink" is 5 % saturated and became "pale"; "turquoise" is 20 % at
    hue 188° and became "muted blue-green". A hue is named only where saturation supports
    it (red at 50 %, Miqo'te's green at 55 %, blue-violet at 47 %).
  - **NOT verified, and deliberately not claimed anywhere:** whether the game tints these
    ornaments with the player's hair colour on the 3D model. The icons cannot answer that.
    The text describes the THUMBNAIL, as all of this file does.
- **Skin and fur detail IS fair game, and was verified before being used.** Stubble,
  freckles, age lines, tribal stripes, scales and fur patterns sit in the entry's own
  `..._fac_base.tex` and cannot be switched off from another menu. Checked by pulling
  the textures: Hyur Midlander male face 7 carries the stubble, Miqo'te faces 2–4
  carry progressively heavier stripes, Hrothgar face 2 carries tiger stripes and face
  3 rosettes. The separate "Facial Features" menu has its own decals per face and is
  NOT what the thumbnails show.

## THE TRAP: a type-0 param is a `Lobby` row, NOT a `CharaMakeCustomize` row

Read this before writing a single line of extraction code. It cost a detour on
2026-08-08 and it fails *silently*, producing confident nonsense.

`CharaMakeType.CharaMakeStruct[].SubMenuParam[]` means different things per
`SubMenuType`, and the ids **overlap between sheets**:

- Jaw (type 0) has params `1050, 1051, 1052, 1053`.
- `Lobby[1050]` = `"Type 1"` — this is what the menu actually reads.
- `CharaMakeCustomize[1050]` ALSO exists: `FeatureID=10, Icon=134010`, and
  `CharaMakeCustomize[1053]`'s hint reads *"…to unlock this **hairstyle** at the
  aesthetician."*
- Those rows are **hairstyles**. `1050`, `1051` and `1052` are all in that same
  race's real Hairstyle param list.

So looking up `CharaMakeCustomize[param].Icon` for Jaw / Nose / Eye Shape /
Eyebrows / Mouth / Fang Length returns a **hairstyle thumbnail**. Describe from
it and the mod will tell a blind player their jaw is shoulder-length wavy hair.
Same family as the `Marker` SortOrder trap and the `Addon`-sheet fellowship
block: pin a row to its owner before trusting it.

## Three families, three pipelines

### A. Icon grids whose params are `CharaMakeCustomize` rows — offline, proven

`SubMenuParam[i]` → `CharaMakeCustomize` row → `.Icon` → `GetFile<TexFile>` →
RGBA → PNG → author. Verified end to end in : `ui/icon/131000/131101_hr1.tex`
… were pulled with Lumina, composited over white with PIL and read back, and the
seven Hyur Midlander male faces were plainly distinguishable.

- **Hairstyle** — params `1, 3, 4, 5…` → `CMC.Icon = 131001, 131003…`
- **Face Paint** — params `2401…` → `CMC.Icon = 130001…` (param 2401 has
  `Icon = 0`; that is the "none" entry)

### B. Icon grids whose params ARE icon ids — offline, same pipeline minus a hop

No `CharaMakeCustomize` row exists for these; the param is already the icon id.
recorded Face as "the exception"; the 2026-08-08 dump shows it is a family:

- **Face** — params `131101…`, `131301…`, `131601…`
- **Tail Shape** — params `134191…`, `136191…`
- **Fur Pattern** — params `137401…`
- **Ear Shape (Viera only, type 1)** — params `138191…`

**All four have since been extracted and read** — Face in batch 1, Tail Shape, Fur
Pattern and Viera Ear Shape in batch 2. The family holds; the caution that used to sit
here ("confirm the `.tex` exists before batching") was discharged by doing it.

### C. No thumbnail — but the type-0 menus are GEOMETRY, and the geometry is readable

**REWRITTEN 2026-08-08.** This section used to say these menus had no per-entry data
anywhere and needed screenshots. That was wrong, and the correction is the most
useful thing to come out of the Face batch.

**Type 0 = a shape key on the face model.** Full evidence in `docs/game-api.md`;
in short, `c<code>f<NNNN>_fac.mdl` declares `shp_chk_*` (Jaw), `shp_eye_*`,
`shp_brw_*`, `shp_nse_*`, `shp_mth_*` (Fang Length on Hrothgar), `shp_irs_*` and
`shp_etc_*` (Elezen/Lalafell ears), and the count of each is **always exactly
`SubMenuNum - 1`** on all 18 body codes. Entry 1 is the base mesh; entries 2..N are
shapes a..N-1, in order.

**BUILT 2026-08-09 — option 1, the measured route. 2,509 entries, no authoring.**

- `tools/charamake-dump/MdlV6.cs` is the v6 `.mdl` reader Lumina cannot be (it dies on
  the v6 bone table; four traps, all recorded in `docs/game-api.md`).
- `cmdump facemodels` resolves every row to its body code and face-id offset, searched
  and cross-checked rather than assumed.
- `cmdump shapedump` measures every shape on every face model — displacement per
  vertex, aggregated into six axes — and writes `tools/icons/shapes.tsv`.
- `tools/charamake-dump/shapewords.py` turns those numbers into German and English by
  a rule written down in that file, and emits
  `FF14Accessibility/Services/CharaMakeShapeText.cs`. **Do not hand-edit that file.**
- `verify.py` checks it: no duplicate keys, full coverage against the dump, and — the
  check that matters most for a cursor-move summary — no two entries of one menu on
  one face sharing a summary.

The words say only what a displacement can carry: wider/narrower, longer/shorter,
higher/lower, more or less prominent, always against entry 1 (the untouched base mesh),
with a magnitude word graded against the OTHER entries of the same menu on the same
face. The comment above every generated entry carries the millimetres it came from.

**WHERE IT IS HEARD, and the one half that is not built.** Type-0 menus open
`CMFRadio*` windows and `CharaMakeReader` deliberately stands down while one is open
(`IsRadioPickerOpen`, added after the user's double-announcement report). So the text
arrives on the **category focus** (full text, landing on "Nase" reads the current
value) and on **Strg+F10** — Ctrl+F10 half. The **short summary on the arrow
press** does not: the utterance it would ride on is
`UIReaderService.TryReadCharaMakeRadioPosition`'s, and that call site was out of scope.
It is a one-line change there — `LocalStrings.CharaMakeOption`'s last parameter is
already the description clause — and it cannot reintroduce the double announcement,
because it adds to the utterance that already fires rather than adding a second one.
**Do not fix it by loosening `IsRadioPickerOpen`.**

**16 (row, face, menu) triples are deliberately left undescribed**: Miqo'te Eyebrows on
all four rows and all four faces, and Hyur Highlander male Eyebrows on all four faces.
Those models carry one `shp_brw_*` MORE than the menu can reach and nothing says which
of them the menu offers, so "entry k is shape k-2" is not established there. `shapedump`
emits a `# SKIP` line instead of a guess.

The alternative that was NOT taken: render each shape with a software rasteriser and
describe it by eye. More faithful to what a sighted player sees, far more work per
entry, and it re-introduces the drift-into-a-plausible-guess risk the measurement
avoids.

**Type 4 is smaller than this file claimed.** `CharaMakeType.FacialFeatureOption`
holds **7 UI icon ids per face**, numbered off the face's own icon (face `131101` →
`131111…131117`), so these entries *are* picturable through the family-A/B pipeline.

**THE TYPE-4 BLOCKER IS GONE — 2026-08-09. The arithmetic it rested on was wrong.**
This file used to say *"Au Ra rows want 5 + 2 + 5 = 12 against the same 7"*. Au Ra rows
have **no Facial Features menu at all**; they have Limbal Ring (2) + Other Features (5).
Counted over all 32 rows:

- **Every row has exactly TWO type-4 menus, always one of 5 entries and one of 2** —
  `SubMenuNum` pairs across the whole sheet are `{(2, 5): 32}`. Both on byte 12,
  `SubMenuMask` 0 in every row.
- **5 + 2 = 7** = the FacialFeature bits in byte 12 (bit 7 is `LegacyTattoo`, not
  offered) = the `FacialFeatureOption` slots per face.
- **MEASURED from the icons: the 5-entry menu owns slots 1–5 and the 2-entry menu owns
  slots 6–7.** Hyur Midlander male's slots 6–7 are tattoos, Elezen Wildwood's are ear
  clasps, Au Ra Raen's are limbal rings — and in the Elezen and Au Ra rows the 2-entry
  menu comes FIRST in `CharaMakeStruct`, which rules out menu order as the rule.
- **Still an inference:** that slot *i* is bit *i*−1 of byte 12. One in-game toggle
  settles it; `CharaMakeReader.LogFeatureBitProbe` writes the line and its comment has
  the four-step test. Nothing is spoken off that mapping until it is answered.

So the descriptions can be authored NOW — they are keyed by icon id and do not depend
on the bit question at all. `cmdump featicons` writes the index and all **924** option
icons; `mksheet.py "Facial Features"` makes 132 contact sheets, one per (row, face),
seven cells each. The ids are all new: **zero overlap with the 1,943 already in
`CharaMakeIconText`.** Note one thing that makes this family SAFER than Hairstyle was:
slot order and icon order are the same here (slot *k* is always the id ending in *k*),
so the entry-order trap cannot bite.

**Authored 2026-08-09: 910 of 924, all eight races** — Hyur 140, Elezen 100, Lalafell
112, Miqo'te 110, Roegadyn 112, Au Ra 112, Hrothgar 112, Viera 112.

**14 icons are deliberately undescribed**, because the feature could not be identified
and a guess is worse than silence: Miqo'te `134142` / `134642`, Elezen Wildwood female
slots 1–2 on all four faces (`132311 132312 132321 132322 132331 132332 132341 132342`)
and Duskwight female slot 1 on all four (`132811 132821 132831 132841`). Both readers
ran a working control first — FFT-aligned differencing that cleanly isolated a KNOWN
scar on a control pair surfaced nothing on these — so this is a measured "nothing
visible", not a shrug. **Settle them by toggling in game.**

What the reading turned up and where its edges are:

- **Only Elezen WILDWOOD has Ear Clasps.** Duskwight male and female are
  `Facial Features(5) + Tattoos(2)`, and their slots 6–7 are a temple mark and a
  cheekbone mark. Easy to get wrong when writing the brief for a batch.

- **Tribe pairs share the renders**, measured rather than assumed: Au Ra Raen vs Xaela
  at 0.96–0.99 edge-map correlation, Viera Veena = Rava's set at +500, Hrothgar Helions
  and The Lost the same features per face. The duplicated wording is deliberate.
- **Slots 6 and 7 are usually the same design mirrored** — the same tattoo, ear ring or
  limbal ring on one side and the other, not two designs. Three Hyur pairs are genuinely
  different designs; the rest are mirrors.
- **LEFT/RIGHT IS NOT SETTLED, and the blocks disagree.** Hyur, Elezen, Miqo'te and
  Roegadyn name a side; Au Ra and Lalafell refuse it. Worse, Hyur reads the convention as
  **not constant** across its own sheets while Miqo'te reads it as constant (slot 6 = the
  character's right, over eight verified pairs). Each is defensible on its own evidence —
  Hyur's tattoos are large enough to orient, Au Ra's 28-pixel irises are not — but the
  result is that a player hears "links/rechts" on some races and "das andere Auge" on
  others. **This needs a decision, and the disagreement needs settling in game rather
  than by more image analysis.** Note also that a side read off the render is a fact
  about the THUMBNAIL and has not been checked against the decal on the 3D model.
- **Watch for transliterated German in a returned batch.** One came back with `ueber` /
  `auslaeuft` / `Nasenruecken` across 350 words. It compiles and passes every id check,
  and a screen reader voices it as spelt. `verify.py` now reports it.
- **Base-face detail was controlled for.** Midlander male face 7 renders every slot with
  stubble and Midlander female face 2 with freckles; four Lalafell base faces carry
  freckles, rosy cheeks or eye shading in the face texture. Each was checked against the
  Face-menu baseline icon so the description is the DELTA, not the render. That is
  layer-1/layer-3 separation, done on the faces where said it had not been.

## Not on the list, and why

- **Iris Size** — the game names these itself: "Large" / "Small". Nothing to add.
- **Voice** — names are "Type 1".."Type 12", but the differentiator is the audio
  sample, which the player hears directly. Navigation and position are the job,
  not naming. (The seven sample-category buttons beside it have no name in the
  game either; the mod says `Sample, 2 of 7`.)
- **Colour palettes** — already named from `human.cmp` via `Services/ColorNamer.cs`.
- **Sliders** — the game supplies both end labels and a read-out sentence.

## Work list — every menu, every row, counted offline (2026-08-08)

Counts are `SubMenuNum` summed across the 32 `CharaMakeType` rows, i.e. the
number of entries a full pass would have to describe. Races SHARE many entries
(Hairstyle is 1551 slots but only ~879 unique icons), so the unique count is
lower wherever a family-A/B icon id repeats. For family C there is no id to
dedupe on, so assume the full number until screenshots say otherwise.

Only ~4–7 faces and ~27–54 hairstyles exist for any ONE character, so **partial
coverage already helps** — this does not have to be finished to be useful.

**DONE: Face — 132/132.** Every row, every entry, in `Services/CharaMakeIconText.cs`.
Fewer than 132 description strings were needed because tribes share faces, and the
sharing was measured rather than assumed: **Au Ra Raen and Xaela face icons are
byte-for-byte identical**, and the Lalafell and Roegadyn-female tribe pairs differ
only in the render's skin tone (under 0.5 % of pixels past a difference of 16).
Elezen, Miqo'te, Roegadyn-male and Viera tribe pairs share the same face shapes with
a different default hairstyle in the render. Hrothgar does NOT share: Helions and
The Lost are separate models, and The Lost's white coat shows no pattern at all,
so those eight are described separately and say so.

**DONE in batch 2 (2026-08-08): Tail Shape 64/64, Fur Pattern 20/20, Viera Ear Shape
16/16 — all three menus complete — plus the first 125 hairstyles.** Those 125 were
**re-authored on 2026-08-09** after the entry-order defect at the top of this file; the
text shipped in batch 2 is gone.

**DONE 2026-08-09, the finishing pass: Hairstyle 879/879 (all 18 blocks, all 32 rows) and
Face Paint 832/832 (all 32 rows).** All six icon menus are complete at 1,943
descriptions. Details at the top of this file, full account in `

- **Family C, type 0** — BUILT. `Services/CharaMakeShapeText.cs`, 2,509 entries generated
  from the face models' vertex deltas by `tools/charamake-dump/shapewords.py`. 16
  (row, face, menu) triples are deliberately skipped where the model carries a surplus
  shape.
- **Type 4** — UNBLOCKED, icons extracted (924, `cmdump featicons`), 132 contact sheets
  made. The 5-entry menu owns option slots 1–5 and the 2-entry menu slots 6–7, measured
  from the pictures. One in-game toggle still has to confirm that slot *i* is bit *i*−1
  of byte 12 before anything is spoken off that mapping.

---

- **Ear Clasps** — 6 entries to describe, over 3 of the 32 rows (SubMenuType 4)
  - 2 (type 4): Elezen Wildwood male, Elezen Wildwood female, Miqo'te Keeper of the Moon female
- **Ear Shape** — 48 entries to describe, over 12 of the 32 rows (SubMenuType 0/1)
  - 4 (type 0): Elezen Wildwood male, Elezen Wildwood female, Elezen Duskwight male, Elezen Duskwight female, Lalafell Plainsfolk male, Lalafell Plainsfolk female, Lalafell Dunesfolk male, Lalafell Dunesfolk female
  - 4 (type 1): Viera Rava male, Viera Rava female, Viera Veena male, Viera Veena female
- **Eye Shape** — 177 entries to describe, over 32 of the 32 rows (SubMenuType 0)
  - 5 (type 0): Hyur Midlander female, Roegadyn Sea Wolf male, Roegadyn Sea Wolf female, Roegadyn Hellsguard male, Roegadyn Hellsguard female, Au Ra Raen female, Au Ra Xaela female, Hrothgar Helions male, Hrothgar Helions female, Hrothgar The Lost male, Hrothgar The Lost female, Viera Rava male, Viera Rava female, Viera Veena male, Viera Veena female
  - 6 (type 0): Hyur Midlander male, Hyur Highlander male, Hyur Highlander female, Elezen Wildwood male, Elezen Wildwood female, Elezen Duskwight male, Elezen Duskwight female, Lalafell Plainsfolk male, Lalafell Plainsfolk female, Lalafell Dunesfolk male, Lalafell Dunesfolk female, Miqo'te Seeker of the Sun male, Miqo'te Seeker of the Sun female, Miqo'te Keeper of the Moon male, Miqo'te Keeper of the Moon female, Au Ra Raen male, Au Ra Xaela male
- **Eyebrows** — 159 entries to describe, over 32 of the 32 rows (SubMenuType 0)
  - 4 (type 0): Hyur Highlander male, Hyur Highlander female, Roegadyn Sea Wolf male, Roegadyn Sea Wolf female, Roegadyn Hellsguard male, Roegadyn Hellsguard female, Au Ra Raen female, Au Ra Xaela female
  - 5 (type 0): Hyur Midlander female, Elezen Wildwood female, Elezen Duskwight female, Lalafell Plainsfolk female, Lalafell Dunesfolk female, Miqo'te Seeker of the Sun male, Miqo'te Seeker of the Sun female, Miqo'te Keeper of the Moon male, Miqo'te Keeper of the Moon female, Hrothgar Helions male, Hrothgar Helions female, Hrothgar The Lost male, Hrothgar The Lost female, Viera Rava male, Viera Rava female, Viera Veena male, Viera Veena female
  - 6 (type 0): Hyur Midlander male, Elezen Wildwood male, Elezen Duskwight male, Lalafell Plainsfolk male, Lalafell Dunesfolk male, Au Ra Raen male, Au Ra Xaela male
- **Face** — 132 entries to describe, over 32 of the 32 rows (SubMenuType 1)
  - 4 (type 1): Hyur Highlander male, Hyur Highlander female, Elezen Wildwood male, Elezen Wildwood female, Elezen Duskwight male, Elezen Duskwight female, Lalafell Plainsfolk male, Lalafell Plainsfolk female, Lalafell Dunesfolk male, Lalafell Dunesfolk female, Miqo'te Seeker of the Sun male, Miqo'te Seeker of the Sun female, Miqo'te Keeper of the Moon male, Miqo'te Keeper of the Moon female, Roegadyn Sea Wolf male, Roegadyn Sea Wolf female, Roegadyn Hellsguard male, Roegadyn Hellsguard female, Au Ra Raen male, Au Ra Raen female, Au Ra Xaela male, Au Ra Xaela female, Hrothgar Helions male, Hrothgar Helions female, Hrothgar The Lost male, Hrothgar The Lost female, Viera Rava male, Viera Rava female, Viera Veena male, Viera Veena female
  - 5 (type 1): Hyur Midlander female
  - 7 (type 1): Hyur Midlander male
- **Facial Features** — 130 entries to describe, over 26 of the 32 rows (SubMenuType 4)
  - 5 (type 4): Hyur Midlander male, Hyur Midlander female, Hyur Highlander male, Hyur Highlander female, Elezen Wildwood male, Elezen Wildwood female, Elezen Duskwight male, Elezen Duskwight female, Lalafell Plainsfolk male, Lalafell Plainsfolk female, Lalafell Dunesfolk male, Lalafell Dunesfolk female, Miqo'te Seeker of the Sun male, Miqo'te Seeker of the Sun female, Miqo'te Keeper of the Moon male, Miqo'te Keeper of the Moon female, Roegadyn Sea Wolf male, Roegadyn Sea Wolf female, Roegadyn Hellsguard male, Roegadyn Hellsguard female, Hrothgar Helions male, Hrothgar The Lost male, Viera Rava male, Viera Rava female, Viera Veena male, Viera Veena female
- **Fang Length** — 12 entries to describe, over 4 of the 32 rows (SubMenuType 0)
  - 3 (type 0): Hrothgar Helions male, Hrothgar Helions female, Hrothgar The Lost male, Hrothgar The Lost female
- **Fur Pattern** — 20 entries to describe, over 4 of the 32 rows (SubMenuType 1)
  - 5 (type 1): Hrothgar Helions male, Hrothgar Helions female, Hrothgar The Lost male, Hrothgar The Lost female
- **Jaw** — 118 entries to describe, over 32 of the 32 rows (SubMenuType 0)
  - 3 (type 0): Roegadyn Sea Wolf male, Roegadyn Sea Wolf female, Roegadyn Hellsguard male, Roegadyn Hellsguard female, Au Ra Raen female, Au Ra Xaela female, Viera Rava male, Viera Rava female, Viera Veena male, Viera Veena female
  - 4 (type 0): Hyur Midlander male, Hyur Midlander female, Hyur Highlander male, Hyur Highlander female, Elezen Wildwood male, Elezen Wildwood female, Elezen Duskwight male, Elezen Duskwight female, Lalafell Plainsfolk male, Lalafell Plainsfolk female, Lalafell Dunesfolk male, Lalafell Dunesfolk female, Miqo'te Seeker of the Sun male, Miqo'te Seeker of the Sun female, Miqo'te Keeper of the Moon male, Miqo'te Keeper of the Moon female, Au Ra Raen male, Au Ra Xaela male, Hrothgar Helions male, Hrothgar Helions female, Hrothgar The Lost male, Hrothgar The Lost female
- **Limbal Ring** — 8 entries to describe, over 4 of the 32 rows (SubMenuType 4)
  - 2 (type 4): Au Ra Raen male, Au Ra Raen female, Au Ra Xaela male, Au Ra Xaela female
- **Mouth** — 112 entries to describe, over 28 of the 32 rows (SubMenuType 0)
  - 4 (type 0): Hyur Midlander male, Hyur Midlander female, Hyur Highlander male, Hyur Highlander female, Elezen Wildwood male, Elezen Wildwood female, Elezen Duskwight male, Elezen Duskwight female, Lalafell Plainsfolk male, Lalafell Plainsfolk female, Lalafell Dunesfolk male, Lalafell Dunesfolk female, Miqo'te Seeker of the Sun male, Miqo'te Seeker of the Sun female, Miqo'te Keeper of the Moon male, Miqo'te Keeper of the Moon female, Roegadyn Sea Wolf male, Roegadyn Sea Wolf female, Roegadyn Hellsguard male, Roegadyn Hellsguard female, Au Ra Raen male, Au Ra Raen female, Au Ra Xaela male, Au Ra Xaela female, Viera Rava male, Viera Rava female, Viera Veena male, Viera Veena female
- **Nose** — 181 entries to describe, over 32 of the 32 rows (SubMenuType 0)
  - 5 (type 0): Hyur Midlander female, Au Ra Raen female, Au Ra Xaela female, Hrothgar Helions male, Hrothgar Helions female, Hrothgar The Lost male, Hrothgar The Lost female, Viera Rava male, Viera Rava female, Viera Veena male, Viera Veena female
  - 6 (type 0): Hyur Midlander male, Hyur Highlander male, Hyur Highlander female, Elezen Wildwood male, Elezen Wildwood female, Elezen Duskwight male, Elezen Duskwight female, Lalafell Plainsfolk male, Lalafell Plainsfolk female, Lalafell Dunesfolk male, Lalafell Dunesfolk female, Miqo'te Seeker of the Sun male, Miqo'te Seeker of the Sun female, Miqo'te Keeper of the Moon male, Miqo'te Keeper of the Moon female, Roegadyn Sea Wolf male, Roegadyn Sea Wolf female, Roegadyn Hellsguard male, Roegadyn Hellsguard female, Au Ra Raen male, Au Ra Xaela male
- **Other Features** — 30 entries to describe, over 6 of the 32 rows (SubMenuType 4)
  - 5 (type 4): Au Ra Raen male, Au Ra Raen female, Au Ra Xaela male, Au Ra Xaela female, Hrothgar Helions female, Hrothgar The Lost female
- **Tail Shape** — 64 entries to describe, over 12 of the 32 rows (SubMenuType 1)
  - 4 (type 1): Au Ra Raen male, Au Ra Raen female, Au Ra Xaela male, Au Ra Xaela female, Hrothgar Helions male, Hrothgar Helions female, Hrothgar The Lost male, Hrothgar The Lost female
  - 8 (type 1): Miqo'te Seeker of the Sun male, Miqo'te Seeker of the Sun female, Miqo'te Keeper of the Moon male, Miqo'te Keeper of the Moon female
- **Tattoos** — 50 entries to describe, over 25 of the 32 rows (SubMenuType 4)
  - 2 (type 4): Hyur Midlander male, Hyur Midlander female, Hyur Highlander male, Hyur Highlander female, Elezen Duskwight male, Elezen Duskwight female, Lalafell Plainsfolk male, Lalafell Plainsfolk female, Lalafell Dunesfolk male, Lalafell Dunesfolk female, Miqo'te Seeker of the Sun male, Miqo'te Seeker of the Sun female, Miqo'te Keeper of the Moon male, Roegadyn Sea Wolf male, Roegadyn Sea Wolf female, Roegadyn Hellsguard male, Roegadyn Hellsguard female, Hrothgar Helions male, Hrothgar Helions female, Hrothgar The Lost male, Hrothgar The Lost female, Viera Rava male, Viera Rava female, Viera Veena male, Viera Veena female

_Generated offline with Lumina against the installed sqpack; the generator is in
the session scratchpad and is trivial to re-run after a patch._
