"""Check CharaMakeIconText.cs against the sheet dumps.

Run this BEFORE building after adding a batch. It catches two things the C# compiler
cannot see:

  1. the SAME icon id registered twice - the second F()/S() silently overwrites the
     first, so one entry would quietly describe a different picture;
  2. an icon id that no menu actually offers - a typo that can never fire and would
     look like "described" in any hand count.

It also prints coverage per menu and per row, which is the number
has to state.

    python tools\\charamake-dump\\verify.py <dir-with-idx-*.tsv>

The index files are whatever `cmdump icons <Menu>` wrote, saved as
`idx-<Menu_With_Underscores>.tsv`. Menus with no index present are simply not reported
(Face's index was dumped in an earlier session and is not kept).
"""
import re, os, sys, collections

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(REPO, 'FF14Accessibility', 'Services', 'CharaMakeIconText.cs')
IDXDIR = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))

src = open(SRC, encoding='utf-8').read()

# Every F(...) / S(...) call; the trailing numeric arguments are the icon ids.
# `firstpos` remembers where in the file each id was registered, which is what the
# entry-order check below needs - see its comment for why that check exists.
ids = []
firstpos = {}
for call, m in enumerate(re.finditer(r'\b[FS]\(\s*(.*?)\);', src, re.S)):
    found = [int(n) for n in re.findall(r'(?<![\w.])(\d{5,7})(?![\w.])', m.group(1))]
    ids.extend(found)
    for i in found:
        firstpos.setdefault(i, call)

dup = [i for i, c in collections.Counter(ids).items() if c > 1]
print(f'registered icon ids: {len(ids)}, distinct: {len(set(ids))}')
print(f'DUPLICATES: {sorted(dup) if dup else "none"}')

# ---------------------------------------------------------- transliterated German
# One batch came back with ue/ae/oe instead of ü/ä/ö - "ueber",
# "auslaeuft", "Nasenruecken" - across 350 words, while every other batch used real
# umlauts. It compiles, it passes every other check, and a screen reader voices it as
# spelt. The German string is the FIRST and THIRD argument of F()/S(); the English is
# the second and fourth, and English words must not be flagged. A small allow-list
# covers the German words that legitimately contain those pairs.
# A word is only suspicious if its ae/oe/ue is not explained by an ordinary German
# spelling: "aue" covers Braue / Augenbraue / graue / schlaue, "quer" covers quer /
# Querbalken / quert, and Roegadyn is a proper noun. Strip those, and anything with
# ae/oe/ue left really is a transliteration.
INNOCENT = ('aue', 'quer', 'roegadyn', 'zueinander', 'zuein')
susp = collections.Counter()
for m in re.finditer(r'\b[FS]\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*"(?:[^"\\]|\\.)*"'
                     r'(?:\s*,\s*"((?:[^"\\]|\\.)*)"\s*,\s*"(?:[^"\\]|\\.)*")?', src, re.S):
    for german in (m.group(1), m.group(2)):
        if not german:
            continue
        for word in re.findall(r'\b\w*(?:ae|oe|ue)\w*\b', german):
            stripped = word.lower()
            for ok in INNOCENT:
                stripped = stripped.replace(ok, '')
            if any(p in stripped for p in ('ae', 'oe', 'ue')):
                susp[word] += 1
print(f'transliterated German (ue/ae/oe where ü/ä/ö belongs): {sum(susp.values())} word(s)'
      + (f', e.g. {[w for w, _ in susp.most_common(6)]}' if susp else ''))

menus = {}
entryorder = {}
for fn in os.listdir(IDXDIR):
    if not (fn.startswith('idx-') and fn.endswith('.tsv')):
        continue
    offered = collections.OrderedDict()
    ordered = collections.OrderedDict()
    for line in open(os.path.join(IDXDIR, fn), encoding='utf-8-sig'):
        if line.startswith('#'):
            continue
        f = line.rstrip('\n').split('\t')
        if len(f) >= 7:
            offered.setdefault(f[1], set()).add(int(f[5]))
            ordered.setdefault(f[1], []).append((int(f[3]), int(f[5])))
    if offered:
        menus[fn[4:-4].replace('_', ' ')] = offered
        entryorder[fn[4:-4].replace('_', ' ')] = ordered

have = set(ids)
allknown = set()
print()
for name, rows in sorted(menus.items()):
    uniq = set().union(*rows.values())
    allknown |= uniq
    slots = sum(len(v) for v in rows.values())
    doneslots = sum(len(v & have) for v in rows.values())
    full = [w for w, v in rows.items() if v <= have]
    print(f'{name}: {len(uniq & have)}/{len(uniq)} unique icons described '
          f'({doneslots}/{slots} slots), {len(full)}/{len(rows)} rows COMPLETE')
    if 0 < len(full) < len(rows):
        for w in sorted(rows):
            mark = 'done' if rows[w] <= have else f'{len(rows[w] & have)}/{len(rows[w])}'
            print(f'    {w:38s} {mark}')

orphan = have - allknown
print(f'\nids described but absent from the indexes present here: {len(orphan)}')
print('(expected to be the 132 Face ids unless a Face index is in this directory)')

# ---------------------------------------------------------------- entry-order check
# The failure this catches, which every check above misses.
#
# Descriptions are authored from a contact sheet laid out in ENTRY order, but they are
# keyed by ICON ID - and the two orders are NOT the same. Hrothgar interleaves two runs
# outright (entry 1 = 137001, entry 2 = 137009, entry 3 = 137002), and Hyur Midlander
# male puts icon 131002 at entry 13. An author who describes the right pictures but
# writes them out against the ids in sorted order produces a block where EVERY id is
# registered to the wrong picture. The id set is complete, there are no duplicates and
# nothing is orphaned, so the checks above all pass - and the mod then confidently
# announces the wrong hairstyle. That is exactly what happened once already.
#
# The signal: descriptions are written in entry order, so a row's ids should appear in
# the SOURCE in entry order too. A row that appears in sorted-id order instead was
# written against the id list rather than against the sheet, which is the shape of the
# mistake.
#
# READ THE RESULT CORRECTLY - this is a WARNING, not a verdict. A block can be written
# in id order and still be right: Hyur Midlander male flags here, yet four of its
# descriptions were checked against their pictures and match (131002 chin-length, 131005
# the shaved scrollwork, 131014 the buzz cut, 131022 the crown tuft). All the check says
# is "this block was not laid out from the sheet, so nothing here guarantees the pairing
# - go look." Only eyes on the pictures can settle it.
print()
bad = 0
for name, rows in sorted(entryorder.items()):
    for who, items in rows.items():
        seq = [i for _, i in sorted(items) if i in firstpos]
        if len(seq) < 2:
            continue
        pos = [firstpos[i] for i in seq]
        if pos != sorted(pos):
            worst = next(k for k in range(1, len(pos)) if pos[k] < pos[k - 1])
            print(f'ENTRY-ORDER MISMATCH  {name} / {who}: '
                  f'entry {worst + 1} (icon {seq[worst]}) is registered before '
                  f'entry {worst} (icon {seq[worst - 1]})')
            bad += 1
print(f'entry-order check: {bad} row(s) out of order' if bad else
      'entry-order check: every row is registered in entry order')

# ------------------------------------------------- cursor-move summaries, icon table
# The shape table has had this check since it was generated; the
# ICON table never did, and it was the missing one. Two entries of the same menu on
# the same row that share a summary are indistinguishable to somebody steering by it:
# the arrow key moves, the same two words come back, and there is no way to tell that
# the cursor went anywhere. The full Ctrl+F10 text is the fallback, but the summary is
# what is heard on every move.
#
# Found on its first run, 2026-08-10: 96 pairs, ALL in Facial Features, and the full
# text is identical too. The pictures are NOT the same - they are the LEFT and RIGHT
# variants of one feature (verified pixelwise on 132116/132117 and 136116/136117:
# neither identical nor mirrored; the render shows the other side of the head). So the
# words are simply missing the side. The six reachable menus - Face, Hairstyle, Tail
# Shape, Fur Pattern, Ear Shape, Face Paint - are CLEAN.
#
# Entries the game draws no picture for are excluded: they have no description by
# design, not by omission. See cmtext.read_index.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cmtext                                                        # noqa: E402

print()
text = cmtext.load_text(SRC)
clashes = collections.OrderedDict()
for name in cmtext.menus_present(IDXDIR):
    groups, notex = cmtext.read_index(IDXDIR, name)
    for (rowid, who), items in groups.items():
        byword = collections.defaultdict(list)
        for entry, iconid in items:
            if iconid == 0 or iconid in notex or iconid not in text:
                continue
            brief = text[iconid][0]
            if brief:                       # no summary means "use the full text"
                byword[brief].append((entry, iconid))
        for brief, hits in byword.items():
            if len(hits) > 1:
                clashes.setdefault(name, []).append((who, brief, hits))

if not clashes:
    print('cursor-move summaries: every entry of every row has a distinct summary')
else:
    total = sum(len(v) for v in clashes.values())
    for name, hits in clashes.items():
        print(f'SUMMARY CLASH  {name}: {len(hits)} pair(s)')
        for who, brief, where in hits[:3]:
            ids = ', '.join(f'entry {e} (#{i})' for e, i in where)
            print(f'    {who}: "{brief}" on {ids}')
        if len(hits) > 3:
            print(f'    ... and {len(hits) - 3} more in this menu')
    print(f'{total} row(s) have two entries the cursor-move summary cannot tell apart')

# ------------------------------------------------------------- type-0 shape-key table
# CharaMakeShapeText.cs is GENERATED (shapewords.py) from
# tools/icons/shapes.tsv, so the failure modes are different from the hand-authored
# icon table and need their own checks:
#
#   1. a duplicate KEY - the second S() would silently overwrite the first, exactly the
#      way a duplicate icon id does;
#   2. a key the game never asks for, or a menu entry the game DOES ask for that the
#      table has no answer to - i.e. the generator and the dump drifting apart;
#   3. two entries of the SAME menu on the SAME face sharing a cursor-move summary. A
#      summary that cannot tell entry 3 from entry 4 is worse than useless to someone
#      steering by it, and the generator's disambiguation pass exists to prevent it.
#      This is what proves that pass actually ran.
SHAPESRC = os.path.join(REPO, 'FF14Accessibility', 'Services', 'CharaMakeShapeText.cs')
SHAPETSV = os.path.join(IDXDIR, 'shapes.tsv')
print()
if not os.path.exists(SHAPESRC):
    print('CharaMakeShapeText.cs: not present, skipped')
else:
    shp = open(SHAPESRC, encoding='utf-8').read()
    skeys, sbrief = [], {}
    for m in re.finditer(r'\bS\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*,'
                         r'\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*,(.*?)\);', shp, re.S):
        found = [int(n) for n in re.findall(r'(?<![\w.])(\d{7,12})(?![\w.])', m.group(5))]
        skeys.extend(found)
        for k in found:
            sbrief[k] = m.group(1)
    sdup = [k for k, c in collections.Counter(skeys).items() if c > 1]
    print(f'shape-key table: {len(skeys)} keys, {len(set(skeys))} distinct, '
          f'DUPLICATES: {sorted(sdup) if sdup else "none"}')

    if not os.path.exists(SHAPETSV):
        print(f'  (no {os.path.basename(SHAPETSV)} here - run `cmdump shapedump` to check coverage)')
    else:
        wanted, skips = set(), 0
        for line in open(SHAPETSV, encoding='utf-8-sig'):
            f = line.rstrip('\n').split('\t')
            if f[0] == 'K':
                wanted.add(int(f[1]) * 1000 + int(f[2]) * 10 + int(f[3]))
            elif line.startswith('# SKIP'):
                skips += 1
        have = set(skeys)
        print(f'  coverage: {len(have & wanted)}/{len(wanted)} menu entries described, '
              f'{len(have - wanted)} keys the dump does not ask for, '
              f'{skips} (row, face, menu) skipped by the dump as undetermined')

    # No string may contradict itself or repeat a clause. Both are reachable because the
    # clauses come from independent axes: two axes can land on the same word (a wider
    # span and a wider spread are both "breiter"), and two axes can land on OPPOSITE
    # words while both being true of different measurements (the nose's depth grew while
    # its mass moved back read as "ausgeprägter, flacher"). Neither is a lie about the
    # geometry, but both read as an error to someone listening, so the generator keeps
    # its vocabularies disjoint and this is what proves it did.
    OPPOSITES = [('breiter', 'schmaler'), ('länger', 'kürzer'), ('höher', 'tiefer'),
                 ('vorstehender', 'zurückgesetzt'), ('dicker', 'dünner'),
                 ('offener', 'schmaler geöffnet'), ('ausgeprägter', 'flacher'),
                 ('voller', 'flacher'), ('weiter abstehend', 'enger anliegend'),
                 ('nach außen versetzt', 'nach innen versetzt'),
                 ('weiter auseinander', 'enger beieinander')]
    contradict, repeat = [], []
    for m in re.finditer(r'S\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*"(?:[^"\\]|\\.)*"\s*,'
                         r'\s*"((?:[^"\\]|\\.)*)"\s*,', shp, re.S):
        for txt in (m.group(1), m.group(2)):
            parts = [p.strip() for p in txt.split(',')]
            if len(set(parts)) != len(parts):
                repeat.append(txt)
            base = [re.sub(r'^(deutlich|leicht) ', '', p) for p in parts]
            for a, b in OPPOSITES:
                if a in base and b in base:
                    contradict.append(txt)
    print(f'  clause hygiene: {len(repeat)} string(s) repeat a clause, '
          f'{len(contradict)} contradict themselves'
          + (f' e.g. {(repeat + contradict)[0]!r}' if repeat or contradict else ''))

    # summaries must be unique inside one menu on one face: key = icon*1000 + byte*10 + entry
    permenu = collections.defaultdict(list)
    for k, b in sbrief.items():
        permenu[k // 10].append((k % 10, b))
    clash = [(g, v) for g, v in permenu.items()
             if len({b for _, b in v}) != len(v)]
    if clash:
        for g, v in clash[:10]:
            print(f'  SUMMARY CLASH  face icon {g // 100}, byte {g % 100}: '
                  + '; '.join(f'entry {e} "{b}"' for e, b in sorted(v)))
        print(f'  {len(clash)} menu(s) have two entries with the same cursor-move summary')
    else:
        print('  summaries: every entry of every menu has a distinct cursor-move summary')
