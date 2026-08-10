"""Shared reader for CharaMakeIconText.cs and the dump indexes.

Extracted so `verify.py` (no image dependencies) and `reviewsheet.py` (Pillow)
parse the description table through exactly ONE piece of code. Two parsers that
drift is how a check comes to pass against a file the mod reads differently.

Deliberately imports nothing outside the standard library.
"""
import os
import re
import collections

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
SRC = os.path.join(REPO, 'FF14Accessibility', 'Services', 'CharaMakeIconText.cs')


def parse_args(src, i):
    """`i` points just past the opening paren of an F(/S( call.

    Returns (args, end) where each arg is the string literal it contains, or the
    raw token for a non-string argument (the trailing icon ids). A hand-written
    scanner rather than a regex because the descriptions contain commas and
    escaped quotes, and both break the obvious pattern.
    """
    args, strs, raw, depth = [], [], '', 1
    while i < len(src):
        c = src[i]
        if c == '"':
            j, buf = i + 1, ''
            while j < len(src):
                if src[j] == '\\':
                    buf += src[j + 1]
                    j += 2
                    continue
                if src[j] == '"':
                    break
                buf += src[j]
                j += 1
            strs.append(buf)
            i = j + 1
            continue
        if c == '(':
            depth += 1
        elif c == ')':
            depth -= 1
            if depth == 0:
                args.append(strs[0] if strs else raw.strip())
                return args, i + 1
        elif c == ',' and depth == 1:
            args.append(strs[0] if strs else raw.strip())
            strs, raw = [], ''
            i += 1
            continue
        raw += c
        i += 1
    return args, i


def load_text(path=SRC):
    """iconId -> (briefDe, briefEn, de, en).

    Mirrors what F()/S() do at runtime, INCLUDING last-write-wins, so a duplicate
    id reads here exactly as the mod would speak it. Reporting duplicates is
    verify.py's job; this only has to agree with the runtime.
    """
    src = open(path, encoding='utf-8').read()
    out = {}
    for m in re.finditer(r'(?<![\w.])([FS])\(', src):
        args, _ = parse_args(src, m.end())
        head = 2 if m.group(1) == 'F' else 4
        if len(args) <= head:
            continue
        if m.group(1) == 'F':
            de, en, brief_de, brief_en = args[0], args[1], '', ''
        else:
            brief_de, brief_en, de, en = args[0], args[1], args[2], args[3]
        for a in args[head:]:
            d = re.fullmatch(r'(\d{5,7})', str(a).strip())
            if d:
                out[int(d.group(1))] = (brief_de, brief_en, de, en)
    return out


def read_index(idxdir, menu):
    """(rowid, who) -> [(entry, iconId)] in entry order, plus the ids the dumper
    reported as having NO TEXTURE.

    The id comes from column 5 and is NEVER inferred from position: entry order
    and icon-id order are not the same, and pairing a description with the wrong
    id is the failure that once shipped 125 hairstyles attached to the wrong hair
    (

    `# NO TEX for icon <id>` marks an entry the GAME draws no picture for - Face
    Paint entry 1 is "no paint", whose CharaMakeCustomize row has Icon == 0, so
    cmdump falls back to the param. It is emitted once but occurs on every row,
    so it is collected as a set and applied to all of them. Such an entry cannot
    carry a picture-derived description and is not a coverage gap.
    """
    tsv = os.path.join(idxdir, f'idx-{menu.replace(" ", "_")}.tsv')
    groups = collections.OrderedDict()
    notex = set()
    for line in open(tsv, encoding='utf-8-sig'):
        if line.startswith('#'):
            m = re.match(r'#\s*NO TEX for icon\s+(\d+)', line)
            if m:
                notex.add(int(m.group(1)))
            continue
        f = line.rstrip('\n').split('\t')
        if len(f) >= 7:
            groups.setdefault((f[0], f[1]), []).append((f[3], int(f[5])))
    return groups, notex


def menus_present(idxdir):
    """Menu names for which an idx-*.tsv exists in this directory."""
    return sorted(fn[4:-4].replace('_', ' ') for fn in os.listdir(idxdir)
                  if fn.startswith('idx-') and fn.endswith('.tsv'))
