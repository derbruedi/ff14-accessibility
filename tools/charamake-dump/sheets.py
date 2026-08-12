"""Turn the raw RGBA icon dumps into labelled contact sheets, one per
CharaMakeType row, so a whole race/tribe/gender's icon menu can be looked at in
one image at native resolution."""
import struct, sys, os
from collections import OrderedDict
from PIL import Image, ImageDraw, ImageFont

BASE = os.path.dirname(os.path.abspath(__file__))


SCALE = int(os.environ.get('SHEET_SCALE', '1'))
CROP = os.environ.get('SHEET_CROP', '')       # "l,t,r,b" in 192-space


def load(path, swap):
    with open(path, 'rb') as f:
        w, h = struct.unpack('<HH', f.read(4))
        data = f.read(w * h * 4)
    img = Image.frombytes('RGBA', (w, h), data)
    if swap:                       # stored B8G8R8A8
        b, g, r, a = img.split()
        img = Image.merge('RGBA', (r, g, b, a))
    if CROP:
        # The thumbnail is a 192x192 bust render with a wide margin; the head sits
        # in the upper middle. Cropping to it before scaling puts the pixels where
        # the differences are instead of on shoulders and background.
        l, t, r, b = [int(v) for v in CROP.split(',')]
        img = img.crop((l * w // 192, t * h // 192, r * w // 192, b * h // 192))
    if SCALE != 1:
        img = img.resize((img.width * SCALE, img.height * SCALE), Image.LANCZOS)
    return img


def sheet(entries, out, swap=True, bg=(255, 255, 255), cols=None, cell_label=True):
    """entries: list of (label, raw_path)."""
    n = len(entries)
    cols = cols or min(int(os.environ.get('SHEET_COLS', '4')), n)
    rows = (n + cols - 1) // cols
    first = load(entries[0][1], swap)
    cw, ch = first.size
    pad, lab = 6, (18 if cell_label else 0)
    W = cols * (cw + pad) + pad
    H = rows * (ch + pad + lab) + pad
    out_img = Image.new('RGB', (W, H), (30, 30, 30))
    try:
        font = ImageFont.truetype('arial.ttf', 14)
    except OSError:
        font = ImageFont.load_default()
    d = ImageDraw.Draw(out_img)
    for i, (label, path) in enumerate(entries):
        c, r = i % cols, i // cols
        x = pad + c * (cw + pad)
        y = pad + r * (ch + pad + lab)
        icon = load(path, swap)
        plate = Image.new('RGB', icon.size, bg)
        plate.paste(icon, (0, 0), icon)
        out_img.paste(plate, (x, y + lab))
        if cell_label:
            d.text((x + 2, y + 1), label, fill=(255, 255, 255), font=font)
    out_img.save(out)
    return out_img.size


if __name__ == '__main__':
    menu = sys.argv[1]
    swap = '--noswap' not in sys.argv
    idx = os.path.join(BASE, f'{menu.lower()}_index.tsv')
    icons = os.path.join(BASE, 'icons', menu)
    outdir = os.path.join(BASE, 'sheets', menu)
    os.makedirs(outdir, exist_ok=True)
    groups = OrderedDict()
    for line in open(idx, encoding='utf-8-sig'):
        if line.startswith('#'):
            continue
        f = line.rstrip('\n').split('\t')
        if len(f) < 7:
            continue
        rowid, who, menuidx, entry, param, icon, via = f
        groups.setdefault((rowid, who), []).append((entry, icon))
    for (rowid, who), items in groups.items():
        ents = [(f'{e}', os.path.join(icons, f'{i}.raw')) for e, i in items
                if os.path.exists(os.path.join(icons, f'{i}.raw'))]
        if not ents:
            continue
        name = f'{int(rowid):02d}_{who.replace(" ", "_").replace(chr(39), "")}.png'
        sz = sheet(ents, os.path.join(outdir, name), swap=swap)
        print(f'{name}  {len(ents)} icons  {sz[0]}x{sz[1]}')
