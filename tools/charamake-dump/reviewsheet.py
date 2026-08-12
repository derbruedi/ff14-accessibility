"""Render every appearance icon NEXT TO the words the mod speaks
for it, so the pairing can be reviewed by eye.

WHY THIS EXISTS, and why it is not the same thing as mksheet.py.

`CharaMakeIconText.cs` is ~6,700 lines of authored prose. Nothing in it can be
checked by reading it: the compiler cannot tell a right description from a wrong
one, `verify.py` proves only that the KEYS are sound (no duplicates, nothing
orphaned, full coverage, registered in entry order), and `mksheet.py` shows the
pictures with no words on them. The one question that actually matters - *does
this sentence describe THIS picture?* - has never had an artifact that lets
somebody answer it. This is that artifact.

It is also what makes the upstream pull request reviewable. A maintainer cannot
be asked to take 1,943 descriptions on trust; he can be asked to run one command
and look at eight sheets.

    dotnet build tools\\charamake-dump\\cmdump.csproj -c Release
    $exe = "tools\\charamake-dump\\bin\\Release\\net10.0-windows\\cmdump.exe"
    & $exe icons Hairstyle | Out-File -Encoding utf8 tools\\icons\\idx-Hairstyle.tsv
    python tools\\charamake-dump\\reviewsheet.py "Hairstyle" tools\\icons

Writes `tools/icons/review/<Menu>/NN_<who>.png` plus a `.md` sidecar carrying the
same pairing as text - the sidecar exists because a reviewing AI reads text far
more reliably than it reads words rendered into a PNG, and because a text diff of
two runs is readable while an image diff is not.

Environment:
    REVIEW_LANG   de | en | both     (default both)
    REVIEW_SCALE  icon magnification (default 1; the dumps are 192x192)
    REVIEW_WIDTH  text column width in pixels (default 1000)

THE TRAP THIS FILE MUST NOT FALL INTO - the same one `mksheet.py` documents.
Entries are laid out in ENTRY order and keyed by ICON ID, and the two orders are
NOT the same (Hrothgar interleaves two id runs; Hyur Midlander male puts icon
131002 at entry 13). The id is always copied from the index, never inferred from
position, and every cell is labelled with both. Describing a picture correctly
but pairing it with the wrong id is the failure that once shipped 125 hairstyle
descriptions attached to the wrong hair
"""
import os
import sys
import struct
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
from cmtext import load_text, read_index          # noqa: E402  shared with verify.py

LANG = os.environ.get('REVIEW_LANG', 'both').lower()
SCALE = int(os.environ.get('REVIEW_SCALE', '1'))
TEXTW = int(os.environ.get('REVIEW_WIDTH', '1000'))

BG = (28, 28, 30)
FG = (236, 236, 238)
DIM = (150, 150, 155)
WARN = (255, 120, 120)
PLATE = (255, 255, 255)


# --------------------------------------------------------------- the icon side
def load_icon(path):
    with open(path, 'rb') as f:
        w, h = struct.unpack('<HH', f.read(4))
        data = f.read(w * h * 4)
    img = Image.frombytes('RGBA', (w, h), data)
    b, g, r, a = img.split()                    # dumps are B8G8R8A8
    img = Image.merge('RGBA', (r, g, b, a))
    if SCALE != 1:
        img = img.resize((img.width * SCALE, img.height * SCALE), Image.LANCZOS)
    return img


# --------------------------------------------------------------- rendering
def font(size, bold=False):
    for name in (('arialbd.ttf', 'arial.ttf') if bold else ('arial.ttf',)):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


F_HEAD = font(17, bold=True)
F_BODY = font(15)
F_TAG = font(13, bold=True)


def wrap(draw, text, fnt, width):
    words, lines, cur = text.split(), [], ''
    for w in words:
        trial = f'{cur} {w}'.strip()
        if draw.textlength(trial, font=fnt) <= width or not cur:
            cur = trial
        else:
            lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines or ['']


def lines_for(iconid, entry, text, notex=()):
    """The label lines of one cell: what the mod says, tagged by when it says it.
    'kurz' is the cursor-move summary (Summarize); the unlabelled line is the full
    Ctrl+F10 text (Describe). An entry with no summary falls back to the full text
    at runtime, and is shown saying so rather than left blank."""
    out = [('head', f'Eintrag {entry}   #{iconid}')]
    if iconid == 0 or iconid in notex:
        # Not a gap: the game draws no picture for this entry, so there is nothing
        # to describe. Icon id 0 is additionally REFUSED on purpose - the reader
        # leaves Icons[i] at 0 for every type-0 menu, so a single description keyed
        # to 0 would be spoken for every Jaw, Nose and Mouth in the game. See the
        # NotAnIcon guard in CharaMakeIconText.cs.
        out.append(('tag', 'kein Bild im Spiel - nichts zu beschreiben / '
                           'no picture in the game, nothing to describe'))
        return out
    if text is None:
        out.append(('warn', 'KEINE BESCHREIBUNG / NO DESCRIPTION'))
        return out
    brief_de, brief_en, de, en = text
    if LANG in ('de', 'both'):
        out.append(('tag', 'DE kurz: ' + (brief_de if brief_de else
                                          '(keine - nutzt den vollen Text)')))
        out.append(('body', 'DE: ' + de))
    if LANG in ('en', 'both'):
        out.append(('tag', 'EN brief: ' + (brief_en if brief_en else
                                           '(none - falls back to full text)')))
        out.append(('body', 'EN: ' + en))
    return out


STYLE = {'head': (F_HEAD, FG), 'tag': (F_TAG, DIM),
         'body': (F_BODY, FG), 'warn': (F_HEAD, WARN)}


def render(menu, who, items, text, icondir, out_png, out_md, notex=()):
    probe = ImageDraw.Draw(Image.new('RGB', (1, 1)))
    pad, gap = 14, 10

    cells = []
    for entry, iconid in items:
        raw = os.path.join(icondir, f'{iconid}.raw')
        icon = load_icon(raw) if os.path.exists(raw) else None
        blocks = []
        for kind, s in lines_for(iconid, entry, text.get(iconid), notex):
            fnt, col = STYLE[kind]
            for ln in wrap(probe, s, fnt, TEXTW):
                blocks.append((ln, fnt, col))
        th = sum(fnt.size + 6 for _, fnt, _ in blocks)
        ih = icon.height if icon else 192 * SCALE
        iw = icon.width if icon else 192 * SCALE
        cells.append((icon, iw, ih, blocks, max(th, ih)))

    width = pad * 2 + max(w for _, w, _, _, _ in cells) + gap + TEXTW
    height = pad * 2 + sum(h for *_, h in cells) + gap * (len(cells) - 1) + 40
    img = Image.new('RGB', (width, height), BG)
    d = ImageDraw.Draw(img)

    d.text((pad, pad), f'{menu}  -  {who}   ({len(cells)} Eintraege)',
           fill=FG, font=F_HEAD)
    y = pad + 34
    iconcol = max(w for _, w, _, _, _ in cells)
    for icon, iw, ih, blocks, h in cells:
        if icon is not None:
            plate = Image.new('RGB', icon.size, PLATE)
            plate.paste(icon, (0, 0), icon)
            img.paste(plate, (pad, y))
        else:
            d.rectangle([pad, y, pad + iw, y + ih], outline=WARN)
            d.text((pad + 8, y + ih // 2), 'kein Icon-Dump', fill=WARN, font=F_TAG)
        ty = y
        for ln, fnt, col in blocks:
            d.text((pad + iconcol + gap, ty), ln, fill=col, font=fnt)
            ty += fnt.size + 6
        y += h + gap
    img.save(out_png)

    with open(out_md, 'w', encoding='utf-8') as f:
        f.write(f'# {menu} - {who}\n\n')
        f.write('Icon-Datei / icon file, dann was der Mod spricht.\n\n')
        for entry, iconid in items:
            t = text.get(iconid)
            f.write(f'## Eintrag {entry} - Icon {iconid}\n\n')
            f.write(f'`{iconid}.raw`\n\n')
            if iconid == 0 or iconid in notex:
                f.write('Kein Bild im Spiel - nichts zu beschreiben / '
                        'no picture in the game, nothing to describe.\n\n')
                continue
            if t is None:
                f.write('**KEINE BESCHREIBUNG / NO DESCRIPTION**\n\n')
                continue
            brief_de, brief_en, de, en = t
            f.write(f'- DE kurz: {brief_de or "(keine - nutzt den vollen Text)"}\n')
            f.write(f'- DE: {de}\n')
            f.write(f'- EN brief: {brief_en or "(none - falls back to full text)"}\n')
            f.write(f'- EN: {en}\n\n')
    return img.size


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        raise SystemExit(2)
    menu = sys.argv[1]
    idxdir = sys.argv[2] if len(sys.argv) > 2 else os.path.join(REPO, 'tools', 'icons')
    outdir = (sys.argv[3] if len(sys.argv) > 3
              else os.path.join(idxdir, 'review', menu.replace(' ', '_')))
    os.makedirs(outdir, exist_ok=True)

    text = load_text()
    icondir = os.path.join(REPO, 'tools', 'icons', menu)
    groups, notex = read_index(idxdir, menu)

    described = missing = skipped = 0
    for (rowid, who), items in groups.items():
        safe = who.replace(' ', '_').replace(chr(39), '')
        stem = os.path.join(outdir, f'{int(rowid):02d}_{safe}')
        sz = render(menu, who, items, text, icondir,
                    stem + '.png', stem + '.md', notex)
        n = sum(1 for _, i in items if i in text)
        skip = sum(1 for _, i in items if i == 0 or i in notex)   # see lines_for
        described += n
        skipped += skip
        missing += len(items) - n - skip
        print(f'{os.path.basename(stem)}.png  {len(items)} entries, '
              f'{n} described  {sz[0]}x{sz[1]}')
    print(f'\n{menu}: {described} described, {missing} without a description'
          + (f', {skipped} entries the game draws no picture for' if skipped else ''))


if __name__ == '__main__':
    main()
