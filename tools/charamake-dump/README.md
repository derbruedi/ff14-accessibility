# charamake-dump — offline extraction for the appearance descriptions

Not part of the plugin. Never built by `FF14Accessibility.csproj`, never
shipped, never loaded by the game. It exists so the next batch of icon descriptions
does not start by rebuilding the pipeline — which is what happened once already,
because the first version lived only in a session scratchpad.

Everything here runs **offline against the installed sqpack**. The game does not
have to be running, and nothing is written to the game folder.

## Build and run

`cmdump.csproj` references `Lumina.dll` and `Lumina.Excel.dll` out of
`$(DALAMUD_HOME)` — the same assemblies the plugin uses, so there is no NuGet
dependency and no version drift.

```powershell
dotnet build tools\charamake-dump\cmdump.csproj -c Release
$exe = "tools\charamake-dump\bin\Release\net10.0-windows\cmdump.exe"
```

The sqpack path is a constant at the top of `Program.cs`. Change it there if the
game moves.

## Modes

- `menus` — every `CharaMakeType` row with its menus: label, `SubMenuType`,
  `SubMenuNum`, `Customize`, `InitVal`, `SubMenuGraphic`, `SubMenuParam`. This is
  the table everything else is checked against.
- `icons <MenuLabel>` — resolves one menu family for all 32 rows, writes
  `icons/<MenuLabel>/<iconId>.raw` (u16 width, u16 height, then B8G8R8A8) and prints
  a TSV index of `row, who, menuIndex, entry, param, icon, via`. Use the menu's
  label exactly as the client shows it: `Face`, `Hairstyle`, `Tail Shape`,
  `Fur Pattern`, `Ear Shape`, `Face Paint`.
- `names <MenuLabel>` — **** does the GAME name any entry of this
  menu? Prints every `CharaMakeCustomize` param whose `Hint` (a `Lobby` row) or
  `HintItem` (the aesthetician unlock item) is non-empty, plus `IsPurchasable`, and a
  count. A game-supplied name has to beat anything the mod authors, so this is the check
  that has to come BEFORE writing descriptions for a menu. Result 2026-08-08:
  `Hairstyle` 0 of 879 named, `Face Paint` 0 of 833 named, none purchasable — so claim holds, and both menus really do have to be authored.
- `features` — `FacialFeatureOption` per row (7 icon ids per face) plus the
  Facial Features `InitVal`.
- `featicons` — **** the TYPE-4 family. Writes all 924
  `FacialFeatureOption` icons to `icons/Facial Features/` and an index in exactly the
  shape `icons <Menu>` produces, so `mksheet.py` and `verify.py` need no special case.
  The index's row key is `<race> <tribe> <sex> face<N>` because the seven slots are per
  FACE, and its "entry" column is the SLOT 1..7. Redirect it to
  `tools\icons\idx-Facial_Features.tsv`.
- `facemodels` — **** which body code and face-id offset each
  `CharaMakeType` row uses, searched and cross-checked (see `docs/game-api.md`). Prints
  its evidence per row and any `SURPLUS` where a model carries more shapes than the
  menu offers.
- `facecmp` — are the `f000N` and `f010N` face bands the same models? (They are not.)
- `mdl6 <path>` — everything `MdlV6.cs` parses out of one model: meshes, submesh
  attribute masks, per-mesh bounding boxes, shapes and their shape-mesh index ranges,
  plus a `trace:` line per section so a bad parse is diagnosable rather than silent.
- `shapedump` — **** the whole type-0 measurement. `K` lines are the
  lookup keys (`faceIcon, customizeByte, entry -> code/face/shape`), `M` lines the
  measured displacements per (body code, face, shape), `# SKIP` lines the
  (row, face, menu) triples where the model carries a surplus shape and the
  entry-to-shape mapping is therefore not determined. Redirect to
  `tools\icons\shapes.tsv`, then run `shapewords.py`.
- `faceprobe` / `shapecheck` — which face models exist per body code, and the shape
  keys each one declares, counted by prefix. `shapecheck` is what proved that the
  type-0 menus are shape keys.
- `shapes <path>` — shape names of one model **via Lumina's `MdlFile`, which THROWS
  on v6 face models**. Kept only to document that; use `mdlraw` instead.
- `mdlraw <path>` — parses the model header by hand out of `GetFile(path).Data` and
  prints the string block. This is the one that works.
- `strings <path>` — printable strings of any file; how a `.mtrl`'s texture paths
  were found.
- `tex <path> <name>` / `facetex <code> <faces>` — pull a texture to
  `icons/facetex/<name>.raw`. Used to prove that stubble, tribal stripes and fur
  patterns live in the face's own `_fac_base.tex` and not in a menu that can switch
  them off.

## sheets.py — looking at the results

Turns the `.raw` dumps into labelled contact sheets, one per CharaMakeType row, so a
whole race/tribe/sex menu can be examined in one image.

```powershell
$env:SHEET_SCALE=3; $env:SHEET_CROP="26,4,166,178"; $env:SHEET_COLS=2
python tools\charamake-dump\sheets.py Face
```

- `SHEET_CROP` is `left,top,right,bottom` in the thumbnail's own 192-pixel space.
  The bust render has a wide margin; cropping to the head before scaling puts the
  pixels where the differences are. `26,4,166,178` clears Lalafell chins, which a
  tighter crop cut off.
- **The `.raw` bytes are B8G8R8A8.** `sheets.py` swaps R and B by default; pass
  `--noswap` to see what the previous pass saw, which is where "the icons have
  a blue tint" came from.

## mksheet.py and verify.py — , added so batch 3 does not rebuild them

`sheets.py`'s own `__main__` looks for the icons under `charamake-dump/icons/<Menu>` and
the index at `<menu>_index.tsv`, but `cmdump icons` writes to `tools/icons/<Menu>`. Rather
than move either, `mksheet.py` points the same `sheets.sheet()` at the real paths:

```powershell
$env:SHEET_SCALE=2; $env:SHEET_COLS=9        # and NO SHEET_CROP - see below
python tools\charamake-dump\mksheet.py "Hairstyle" <dir holding idx-Hairstyle.tsv>
```

**Do not set `SHEET_CROP` for anything but Face.** `26,4,166,178` was tuned for the face
thumbnails and cuts the top off every other menu — hairstyles lose their hair, Viera ears
get decapitated, and the tail icons are not head renders at all.

`mksheet.py` labels every cell **`<entry> #<iconId>`**. That is not cosmetic and must not
be reverted to entry-only labels: the table is keyed by ICON ID and the two orders differ,
which is how 125 hairstyle descriptions once shipped attached to the wrong pictures
(`

`verify.py` is the pre-build check. It extracts every icon id from the `F(...)`/`S(...)`
calls in `CharaMakeIconText.cs` and reports **duplicate ids** — which silently overwrite
an earlier description and are invisible to the compiler — ids that no menu offers, and
coverage per menu and per row against the dumped indexes. It also runs the entry-order
warning. Run it before every build that adds a batch; batch 2 came out 357 registered /
357 distinct / no duplicates.

**It now also checks the ICON table's cursor-move summaries** — the
one check the shape table had and the icon table did not. Two entries of the same menu
on the same row sharing a summary are indistinguishable to somebody steering by it: the
arrow key moves and the same two words come back. First run found **96 pairs, all in
Facial Features**, where the full text is identical as well; the pictures are the LEFT
and RIGHT variants of one feature, so the words are missing the side. The six reachable
menus are clean.

**It now also checks `CharaMakeShapeText.cs`**, the generated type-0
table: duplicate keys, coverage against `shapes.tsv`, and — the one that matters most for
a cursor-move summary — that no two entries of the same menu on the same face share a
summary. A summary that cannot tell entry 3 from entry 4 is useless to someone steering
by it, and the generator's disambiguation pass exists to prevent it; this is what proves
the pass ran.

## reviewsheet.py — , the one artifact that can catch a WRONG description

Everything above proves the table's KEYS are sound. Nothing above answers the question
that actually matters: *does this sentence describe THIS picture?* `verify.py` cannot —
a description attached to the wrong icon passes every check it runs. `mksheet.py` cannot —
it shows the pictures with no words on them. So the pairing was only ever checked by a
human holding a sheet in one window and the C# in another, which is how 125 hairstyles
once shipped attached to the wrong hair.

`reviewsheet.py` renders each icon **next to the words the mod speaks for it**:

```powershell
python tools\charamake-dump\reviewsheet.py "Hairstyle" tools\icons
```

Writes `tools/icons/review/<Menu>/NN_<who>.png` and a `.md` sidecar with the same
pairing as text. The sidecar matters as much as the image: a reviewing AI reads text
far more reliably than words rendered into a PNG, and two runs can be diffed.

- `REVIEW_LANG` `de` | `en` | `both` (default `both`), `REVIEW_SCALE`, `REVIEW_WIDTH`.
- Cells are labelled `Eintrag <n>  #<iconId>` and laid out in ENTRY order with the id
  copied from the index — the same rule `mksheet.py` documents, for the same reason.
- Entries the game draws no picture for are labelled as such, not as gaps. Face Paint
  entry 1 is "no paint": its `CharaMakeCustomize` row has `Icon == 0`, so `cmdump`
  falls back to the param and emits `# NO TEX for icon 2401` once. That comment is
  parsed and applied to all 32 rows. **Do not "fix" the resulting 832/833.**

Current state, all seven menus: Face 132, Hairstyle 1551 slots, Tail Shape 64,
Fur Pattern 20, Ear Shape 16, Face Paint 832 (+32 with no picture), Facial Features
910 of 924 — the 14 gaps are the known type-4 shortfall.

`cmtext.py` holds the `F()`/`S()` parser and the index reader, shared by `verify.py` and
`reviewsheet.py` so the two cannot drift into disagreeing about what the table says. It
imports nothing outside the standard library, which is what keeps `verify.py` runnable
without Pillow.

## MdlV6.cs and shapewords.py — , the type-0 pipeline

`Lumina`'s `MdlFile` throws `EndOfStreamException` on every v6 face model, so
`MdlV6.cs` reads them by hand. Its class comment says exactly what it assumes and how
each assumption is checked; `docs/game-api.md` records the four traps (the v6 bone
table, LOD-relative vertex buffer offsets, mesh-relative shape base indices, and ~200 kB
of the runtime section that nothing here accounts for).

Full run:

```powershell
dotnet build tools\charamake-dump\cmdump.csproj -c Release
$exe = "tools\charamake-dump\bin\Release\net10.0-windows\cmdump.exe"
& $exe shapedump | Out-File -Encoding utf8 tools\icons\shapes.tsv
python tools\charamake-dump\shapewords.py tools\icons\shapes.tsv
python tools\charamake-dump\verify.py tools\icons
dotnet build FF14Accessibility\FF14Accessibility.csproj -c Release
```

`shapewords.py` writes `FF14Accessibility/Services/CharaMakeShapeText.cs`, which is
generated — **never hand-edit it**, change the lexicon in `shapewords.py` and re-run.
Each entry carries the millimetre figures it came from as a comment.

## What it must never be used for

`CharaMakeCustomize[param]` is only valid for **type 1** menus. For a type-0 menu the
param is a `Lobby` row, and `CharaMakeCustomize` has rows at the same ids that are
HAIRSTYLES — Jaw's params 1050–1053 resolve to hairstyle thumbnails. `IconTable`
refuses anything that is not type 1 for that reason. See
`docs/charamake-descriptions.md`, "THE TRAP".
