"""Contact-sheet driver for the icon dumps.

Exists because the paths do not line up on their own: `cmdump icons <Menu>` writes to
`tools\\icons\\<Menu>`, while sheets.py looks under `tools\\charamake-dump\\icons\\<Menu>`
and wants its index at `<menu>_index.tsv`. This points the same sheets.sheet() at the
real dump directory and at whichever index file you saved.

    set SHEET_SCALE=2
    set SHEET_COLS=9
    python tools\\charamake-dump\\mksheet.py "Hairstyle" <dir-with-idx-Hairstyle.tsv> [outdir]

DO NOT set SHEET_CROP for these menus. The `26,4,166,178` crop was tuned for the FACE
thumbnails and cuts the top off everything else: hairstyles lose their hair, Viera ears
are decapitated, and the tail icons are not head renders at all.
"""
import os, sys
from collections import OrderedDict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import sheets

menu = sys.argv[1]
idxdir = sys.argv[2] if len(sys.argv) > 2 else HERE
outdir = sys.argv[3] if len(sys.argv) > 3 else os.path.join(idxdir, 'sheets', menu.replace(' ', '_'))

tsv = os.path.join(idxdir, f'idx-{menu.replace(" ", "_")}.tsv')
icons = os.path.join(REPO, 'tools', 'icons', menu)
os.makedirs(outdir, exist_ok=True)

groups = OrderedDict()
for line in open(tsv, encoding='utf-8-sig'):
    if line.startswith('#'):
        continue
    f = line.rstrip('\n').split('\t')
    if len(f) >= 7:
        groups.setdefault((f[0], f[1]), []).append((f[3], f[5]))

for (rowid, who), items in groups.items():
    # The label MUST carry the icon id, not just the entry number.
    # Descriptions are keyed by ICON ID and the two orders are not the same (Hrothgar
    # interleaves two id runs; Midlander male puts 131002 at entry 13). Labelling with
    # the entry alone is what put 125 hairstyle descriptions on the wrong pictures -
    #
    ents = [(f'{e} #{i}', os.path.join(icons, f'{i}.raw')) for e, i in items
            if os.path.exists(os.path.join(icons, f'{i}.raw'))]
    if not ents:
        continue
    name = f'{int(rowid):02d}_{who.replace(" ", "_").replace(chr(39), "")}.png'
    sz = sheets.sheet(ents, os.path.join(outdir, name))
    print(f'{name}  {len(ents)} icons  {sz[0]}x{sz[1]}')
