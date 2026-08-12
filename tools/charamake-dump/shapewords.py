"""Turn the shape-key MEASUREMENTS into the mod's static table.

    cmdump shapedump > tools\\icons\\shapes.tsv
    python tools\\charamake-dump\\shapewords.py tools\\icons\\shapes.tsv

writes FF14Accessibility/Services/CharaMakeShapeText.cs.

WHY GENERATED AND NOT AUTHORED. The type-0 menus - Jaw, Eye Shape, Eyebrows, Nose,
Mouth, Fang Length, Elezen/Lalafell Ear Shape - have no thumbnail and no name anywhere
in the game data. They are morph targets on the face model, and the only thing that
exists per entry is a list of per-vertex displacements. There are ~1,500 (face, shape)
pairs, far past hand-authoring reach, and a hand-written sentence about geometry nobody
looked at would be a guess. So every word below is derived from a number, by a rule
that is written down here, and re-running this file reproduces the table exactly.

WHAT THE NUMBERS ARE. cmdump's ShapeMeasure walks each shape's
(BaseIndicesIndex -> ReplacingVertexIndex) pairs and subtracts the two vertex
positions. Axes were read off the model, not assumed: the face mesh is symmetric about
x = 0 (X is lateral), sits at y = 1.53..1.75 above the origin (+Y is up) and the eyes
are the most forward geometry (+Z is forward). Units are the game's metres, so this
file works in millimetres throughout.

WHAT THE WORDS MAY SAY. Only what a displacement can support: which way a region moved
and whether it got bigger or smaller along an axis. Nothing about colour, nothing about
"character", no adjective that a vertex delta cannot carry. The comparison is always
against ENTRY 1, which is the untouched base mesh - that is what makes "wider" mean
something.

THE MAGNITUDE WORD IS RELATIVE TO THE MENU. A 2 mm brow lift is large for a brow and
nothing for a jaw, so "leicht" / plain / "deutlich" are decided against the other
entries of the SAME menu on the SAME face, never against an absolute threshold.
"""
import sys, os, collections

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, 'FF14Accessibility', 'Services', 'CharaMakeShapeText.cs')

SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, 'tools', 'icons', 'shapes.tsv')

# ── the lexicon, one entry per (prefix, axis, sign): (German, English) ──────────
#
# Axes, all measured in millimetres against the base mesh:
#   extX  the moved region's full span across the face      -> wider / narrower
#   extY  its span top to bottom                            -> longer / shorter
#   extZ  its span front to back                            -> deeper / shallower
#   up    where its mass sits vertically                    -> higher / lower
#   fwd   where its mass sits front to back                 -> forward / back
#   out   how far its mass sits from the centre line        -> outward / inward
#
# `out` is only used for the features that straddle the centre line (nose, mouth,
# jaw). For a PAIRED feature - eyes, brows, ears - the two sides move away from their
# own centres and cancel against the face's centre line, so `out` reads ~0 there and
# would be a lie; those use extX instead. Proven on the one shape whose meaning the
# game itself states: shp_irs_a is Iris Size "Small", and it reads out=+0.015 mm
# (nothing) but extX -0.74 mm and extY -0.77 mm, i.e. a smaller iris.
WORDS = {
    'chk': {   # Jaw - the chin and jawline
        'extX': (('breiter', 'wider'), ('schmaler', 'narrower')),
        'extY': (('länger', 'longer'), ('kürzer', 'shorter')),
        'extZ': (('tiefer geformt', 'deeper'), ('flacher geformt', 'shallower')),
        'up':   (('höher', 'set higher'), ('tiefer', 'set lower')),
        'fwd':  (('vorstehender', 'more prominent'), ('zurückgesetzt', 'set back')),
        'out':  (('breiter ausladend', 'flaring wider'), ('schmaler zulaufend', 'tapering narrower')),
    },
    'eye': {   # Eye Shape
        'extX': (('weiter', 'wider'), ('schmaler', 'narrower')),
        'extY': (('offener', 'more open'), ('schmaler geöffnet', 'less open')),
        'extZ': (('tiefer liegend', 'deeper set'), ('flacher liegend', 'shallower set')),
        'up':   (('höher sitzend', 'set higher'), ('tiefer sitzend', 'set lower')),
        'fwd':  (('weiter vorn', 'further forward'), ('tiefer liegend', 'further back')),
    },
    'brw': {   # Eyebrows
        'extX': (('breiter', 'wider'), ('schmaler', 'narrower')),
        'extY': (('dicker', 'thicker'), ('dünner', 'thinner')),
        'extZ': (('stärker gewölbt', 'more arched'), ('flacher gewölbt', 'less arched')),
        'up':   (('höher', 'higher'), ('tiefer', 'lower')),
        'fwd':  (('weiter vorn', 'further forward'), ('weiter hinten', 'further back')),
    },
    'nse': {   # Nose
        'extX': (('breiter', 'wider'), ('schmaler', 'narrower')),
        'extY': (('länger', 'longer'), ('kürzer', 'shorter')),
        'extZ': (('ausgeprägter', 'more pronounced'), ('flacher', 'flatter')),
        'up':   (('höher ansetzend', 'set higher'), ('tiefer ansetzend', 'set lower')),
        # `fwd` (where the mass sits) must not share a word with `extZ` (how deep the
        # region is). Both used to say "flacher" on the negative side, and a shape whose
        # span grew while its mass moved back came out as "ausgeprägter, flacher" - a
        # contradiction in words about two different measurements.
        'fwd':  (('vorstehender', 'more prominent'), ('zurückgesetzt', 'set back')),
        # `out` must NOT reuse extX's word. Both firing on one shape produced
        # "deutlich schmaler, deutlich schmaler"; they measure different things (the
        # span of the moved region vs how far its mass sits from the centre line) and
        # now say different things. Dropping `out` instead was worse: Lalafell
        # Dunesfolk male face 2's fifth nose moves ONLY outward (-1.52 mm) and went
        # silent.
        'out':  (('nach außen versetzt', 'shifted outward'), ('nach innen versetzt', 'shifted inward')),
    },
    'mth': {   # Mouth - and Fang Length on Hrothgar, which has no Mouth menu
        'extX': (('breiter', 'wider'), ('schmaler', 'narrower')),
        'extY': (('länger', 'longer'), ('kürzer', 'shorter')),
        'extZ': (('voller', 'fuller'), ('flacher', 'flatter')),
        'up':   (('höher', 'set higher'), ('tiefer', 'set lower')),
        'fwd':  (('vorstehender', 'more prominent'), ('zurückgesetzt', 'set back')),
        'out':  (('nach außen versetzt', 'shifted outward'), ('nach innen versetzt', 'shifted inward')),
    },
    # Byte 19 is TWO menus. Everywhere but Hrothgar it is "Mouth"; Hrothgar has no
    # Mouth menu and puts "Fang Length" on the same byte and the same shp_mth_* shapes.
    # The measurement is identical, the right words are not: a taller mouth region is
    # fuller lips, a taller fang region is a longer fang. cmdump carries the sheet's
    # English label through so the two can be told apart - and the numbers bear the
    # split out, Hrothgar's fang shapes reaching 8-19 mm of vertical extent where a
    # Hyur mouth shape moves under 2 mm.
    'mth@Fang Length': {
        'extX': (('breiter', 'wider'), ('schmaler', 'narrower')),
        'extY': (('länger', 'longer'), ('kürzer', 'shorter')),
        'extZ': (('kräftiger', 'heavier'), ('feiner', 'finer')),
        # `up` is NOT used for fangs - see AXES. A fang's vertical mass shift and its
        # vertical span say the same thing (a longer fang IS one that reaches further
        # down), so keeping both produced "deutlich länger, deutlich weniger weit
        # herabreichend" - self-contradictory, because the sign was also inverted:
        # a fang grows DOWNWARD, i.e. `up` goes NEGATIVE. Left in the table only so the
        # inversion is documented rather than rediscovered.
        'up':   (('weniger weit herabreichend', 'reaching less far down'), ('weiter herabreichend', 'reaching further down')),
        'fwd':  (('vorstehender', 'more prominent'), ('zurückgesetzt', 'set back')),
        'out':  (('weiter auseinander', 'further apart'), ('enger beieinander', 'closer together')),
    },
    # Ear Shape (Elezen, Lalafell). The moved region IS the ear - that is what the
    # shp_etc_ prefix means, proven by the count check - so a change in its lateral
    # span reads directly as how far the ears reach out from the head. It is the
    # dominant axis by a wide margin: Elezen Wildwood male face 1's third and fourth
    # ears pull the span in by 56 and 74 mm while every other axis moves under 13 mm.
    'etc': {
        'extX': (('weiter abstehend', 'sticking out further'), ('enger anliegend', 'lying closer to the head')),
        'extY': (('länger', 'longer'), ('kürzer', 'shorter')),
        'extZ': (('weiter nach hinten reichend', 'reaching further back'), ('weniger weit nach hinten reichend', 'reaching less far back')),
        'up':   (('höher angesetzt', 'set higher'), ('tiefer angesetzt', 'set lower')),
        'fwd':  (('weiter vorn sitzend', 'sitting further forward'), ('weiter hinten sitzend', 'sitting further back')),
    },
}

# Which axes each prefix is allowed to speak about, most telling first. `out` is left
# out of the paired features on purpose - see the comment on WORDS.
AXES = {
    'chk': ['extX', 'out', 'fwd', 'extY', 'up', 'extZ'],
    'eye': ['extY', 'extX', 'up', 'fwd', 'extZ'],
    'brw': ['up', 'extY', 'extX', 'fwd', 'extZ'],
    'nse': ['fwd', 'extX', 'out', 'extY', 'up', 'extZ'],
    'mth': ['extX', 'out', 'extY', 'fwd', 'up', 'extZ'],
    'mth@Fang Length': ['extY', 'fwd', 'extX', 'out', 'extZ'],
    'etc': ['extX', 'extY', 'extZ', 'up', 'fwd'],
}

BYTE_PREFIX = {14: 'brw', 16: 'eye', 17: 'nse', 18: 'chk', 19: 'mth', 22: 'etc'}

# A displacement under this many millimetres is not spoken at all. It is a floor on
# what the measurement can honestly distinguish, not a rendering threshold: 0.1 mm on
# a 220 mm head is under a twentieth of a per cent.
FLOOR_MM = 0.10


def load(path):
    keys, meas = [], {}
    for line in open(path, encoding='utf-8-sig'):
        f = line.rstrip('\n').split('\t')
        if f[0] == 'K':
            # K, faceIcon, byte, entry, code, face, shape, who, menuLabel
            keys.append((int(f[1]), int(f[2]), int(f[3]), f[4], int(f[5]), f[6], f[7],
                         f[8] if len(f) > 8 else ''))
        elif f[0] == 'M':
            # M, code, face, shape, moved, values, bad, meanMag, maxMag, cx,cy,cz,
            #    dOut, dUp, dFwd, dExtX, dExtY, dExtZ, dTop, dBottom
            meas[(f[1], int(f[2]), f[3])] = dict(
                moved=int(f[4]), values=int(f[5]), bad=int(f[6]),
                mean=float(f[7]), mx=float(f[8]),
                cx=float(f[9]), cy=float(f[10]), cz=float(f[11]),
                out=float(f[12]), up=float(f[13]), fwd=float(f[14]),
                extX=float(f[15]), extY=float(f[16]), extZ=float(f[17]))
    return keys, meas


def clauses(prefix, m, scale):
    """The ordered (German, English, axis, mm) clauses for one shape.

    `scale` is the largest absolute value the SAME menu reaches on the SAME face for
    each axis, which is what turns a millimetre into a word a player can use."""
    out = []
    said = set()
    for axis in sorted(AXES[prefix], key=lambda a: -abs(m[a])):
        v = m[axis] * 1000.0
        if abs(v) < FLOOR_MM:
            continue
        de, en = WORDS[prefix][axis][0 if v > 0 else 1]
        # NEVER SAY THE SAME WORD TWICE. Two axes can legitimately land on one word
        # (a wider span and a wider spread are both "breiter"), and repeating it reads
        # as an error to a listener and hides the second-strongest real difference.
        # The axes are walked strongest-first so the survivor is the larger move.
        if de in said:
            continue
        said.add(de)
        top = scale.get(axis, 0.0)
        # THREE BANDS, all relative to the strongest move of the same axis in the same
        # menu on the same face. This is the only calibration available: a millimetre
        # means nothing on its own, but "this is the most/least X of the six on offer"
        # is exactly what a player choosing between them wants. It is also what keeps
        # two entries of one menu from reading identically - see the disambiguation
        # pass in main(), and verify.py, which fails the build if any pair still does.
        if top > 0:
            if abs(v) >= 0.85 * top:
                de, en = 'deutlich ' + de, 'markedly ' + en
            elif abs(v) <= 0.45 * top:
                de, en = 'leicht ' + de, 'slightly ' + en
        out.append((de, en, axis, v))
    out.sort(key=lambda c: -abs(c[3]))
    return out


def main():
    keys, meas = load(SRC)
    bad = [k for k, v in meas.items() if v['bad'] > 0]
    if bad:
        print(f'ABORT: {len(bad)} measured shapes had unresolved indices, e.g. {bad[:3]}')
        return 1

    # Group the keys by the menu they belong to, so a magnitude can be judged against
    # its siblings: (faceIcon, byte) -> [(entry, code, face, shape)]
    menus = collections.OrderedDict()
    for icon, b, entry, code, face, shape, who, menulabel in keys:
        # The lexicon key is the shape prefix, with the menu label appended where two
        # menus share one prefix (byte 19 = Mouth / Fang Length).
        pre = BYTE_PREFIX[b]
        if f'{pre}@{menulabel}' in WORDS:
            pre = f'{pre}@{menulabel}'
        menus.setdefault((icon, b, who, pre), []).append((entry, code, face, shape))

    # text -> list of keys, so one string can serve many faces exactly like the icon
    # table does. Keys are icon*1000 + byte*10 + entry.
    table = collections.OrderedDict()
    skipped = 0
    for (icon, b, who, prefix), items in menus.items():
        rows = []
        for entry, code, face, shape in sorted(items):
            m = meas.get((code, face, shape))
            if m is None:
                skipped += 1
                continue
            rows.append((entry, shape, m))
        if not rows:
            continue
        scale = {ax: max(abs(m[ax]) * 1000.0 for _, _, m in rows) for ax in AXES[prefix]}
        cls = {entry: clauses(prefix, m, scale) for entry, shape, m in rows}

        # DISAMBIGUATE WITHIN THE MENU. Two entries of one menu can lead with the same
        # two clauses ("dünner, leicht tiefer" is both Eyebrows 3 and 4 on Hyur
        # Midlander male face 1) and a summary that cannot tell two entries apart is
        # the one thing the cursor-move text must not do. So the brief grows by one
        # more measured clause until it is unique, and only stops when the shape has
        # no clauses left to give.
        # Start at ONE clause. 61e asked for "a precise one- or two-word summary" on the
        # cursor move, and a summary that runs to eight words is the long-form text
        # wearing the wrong hat - the player hears it on every arrow press and the next
        # press cuts it off. So the brief is the single strongest measured clause, and
        # grows ONLY where that would make two entries of the same menu indistinguishable.
        depth = {entry: 1 for entry in cls}
        for _ in range(4):
            seen = collections.defaultdict(list)
            for entry, cl in cls.items():
                seen[', '.join(c[0] for c in cl[:depth[entry]])].append(entry)
            clash = [e for v in seen.values() if len(v) > 1 for e in v]
            if not clash:
                break
            grew = False
            for e in clash:
                if depth[e] < len(cls[e]):
                    depth[e] += 1
                    grew = True
            if not grew:
                break

        for entry, shape, m in rows:
            cl = cls[entry]
            if not cl:
                # No axis clears the floor. Two different things can cause that, and
                # they must not be reported as the same thing:
                #   - the shape really does almost nothing (mean displacement tiny);
                #   - the shape MOVES the mesh but its directions cancel, e.g. Lalafell
                #     Dunesfolk male face 2's third nose averages 0.84 mm per vertex
                #     with every aggregate axis under 0.1 mm. That nose IS different;
                #     what the measurement cannot give it is a direction.
                # Saying "barely changed" for the second case would be false.
                if m['mean'] * 1000.0 >= 0.20:
                    brief_de, brief_en = 'anders geformt', 'shaped differently'
                    full_de = 'anders geformt, ohne messbare Verschiebung in Breite, Höhe oder Tiefe'
                    full_en = 'shaped differently, with no measurable shift in width, height or depth'
                else:
                    brief_de, brief_en = 'kaum verändert', 'barely changed'
                    full_de, full_en = 'kaum verändert', 'barely changed'
            else:
                d = depth[entry]
                brief_de = ', '.join(c[0] for c in cl[:d])
                brief_en = ', '.join(c[1] for c in cl[:d])
                full_de = ', '.join(c[0] for c in cl[:max(3, d)])
                full_en = ', '.join(c[1] for c in cl[:max(3, d)])
            note = ' '.join(f'{c[2]}{c[3]:+.2f}mm' for c in cl[:3]) or 'all axes under the floor'
            key = icon * 1000 + b * 10 + entry
            table.setdefault((brief_de, brief_en, full_de, full_en),
                             {'keys': [], 'note': []})
            table[(brief_de, brief_en, full_de, full_en)]['keys'].append(key)
            table[(brief_de, brief_en, full_de, full_en)]['note'].append(f'{icon}/{prefix}{entry}: {note}')

    lines = []
    lines.append(HEADER)
    lines.append('        // ── generated: <brief de> | <brief en> | <full de> | <full en> | keys')
    for (bd, be, fd, fe), v in table.items():
        # ONE note per string, and it names the key it belongs to. A string is shared by
        # every (face, menu, entry) that measured the same way, and those entries do NOT
        # have the same millimetres - they only landed in the same words. Saying so is
        # the difference between a traceable figure and a misleading one; the rest are
        # in tools/icons/shapes.tsv, which is the actual record.
        more = f' (+{len(v["keys"]) - 1} more keys measured separately, see shapes.tsv)' if len(v['keys']) > 1 else ''
        lines.append(f'        // {v["note"][0]}{more}')
        lines.append(f'        S("{bd}", "{be}",')
        lines.append(f'          "{fd}", "{fe}",')
        for chunk in [sorted(v['keys'])[i:i + 12] for i in range(0, len(v['keys']), 12)]:
            lines.append('          ' + ', '.join(str(k) for k in chunk) + ',')
        lines[-1] = lines[-1].rstrip(',') + ');'
    lines.append(FOOTER)
    open(OUT, 'w', encoding='utf-8', newline='\r\n').write('\n'.join(lines))

    total = sum(len(v['keys']) for v in table.values())
    print(f'{OUT}: {total} entries, {len(table)} distinct strings, {skipped} keys had no measurement')
    return 0


HEADER = '''// <auto-generated>
//   Produced by tools/charamake-dump/shapewords.py from tools/icons/shapes.tsv, which
//   cmdump's `shapedump` measures out of the game's own face models. DO NOT EDIT BY
//   HAND - regenerate. The comment above each entry carries the millimetre figures the
//   words came from, so any line can be traced back to the geometry.
// </auto-generated>
#nullable enable
using System.Collections.Generic;

namespace FF14Accessibility.Local;

/// <summary>
/// Spoken descriptions for the character-creation TYPE-0 menus:
/// Jaw, Eye Shape, Eyebrows, Nose, Mouth, Fang Length and the Elezen/Lalafell Ear
/// Shape. Iris Size is deliberately absent - the game names those itself ("Large" /
/// "Small") and the mod must not talk over the game's own word.
/// WHY THESE ARE MEASURED AND NOT AUTHORED. Unlike the icon menus, a type-0 entry has
/// no thumbnail: it is a morph target on the face model, and the only per-entry data
/// in the game is a list of vertex displacements. Entry 1 is the untouched mesh and
/// entries 2..N are shapes a..N-1 (docs/game-api.md). So the text is DERIVED from the
/// geometry by a rule written down in shapewords.py, not written by hand from a
/// picture - which is also why it covers every face model in the game rather than the
/// handful anyone could look at.
/// STILL MOD-AUTHORED WORDS. The measurement is the game's, the vocabulary is not.
/// <see cref="LocalStrings.CharaMakeAuthoredNote"/> covers these the same way it
/// covers <see cref="CharaMakeIconText"/>.
/// WHAT IT CAN AND CANNOT SAY. Only direction and extent along an axis, always
/// against entry 1: wider/narrower, longer/shorter, higher/lower, more or less
/// prominent. Never colour, never a judgement. The magnitude word ("leicht") is
/// relative to the other entries of the SAME menu on the SAME face, because 2 mm is a
/// lot for an eyebrow and nothing for a jaw.
/// KEY. <c>faceIcon * 1000 + customizeByte * 10 + entry</c>. The face icon id is what
/// the reader already resolves for the Face menu, it is the same number in every
/// client language, and it pins the description to one race/tribe/sex AND one face -
/// which matters, because the same menu on a different face is a different shape.
/// The CUSTOMIZE BYTE identifies the menu without touching its label, which arrives
/// in the client's language (14 Eyebrows, 16 Eye Shape, 17 Nose, 18 Jaw, 19 Mouth or
/// Fang Length, 22 Ear Shape).
/// WHAT IS DELIBERATELY MISSING. Miqo'te Eyebrows, all four rows and all four faces
/// each: the face model carries FIVE shp_brw_* shapes while the menu offers five
/// entries, i.e. one more shape than the four the menu can reach. Nothing in the data
/// says which four, so "entry k is shape k-2" - which holds everywhere else and is
/// what this table rests on - is not established there. Those entries get no
/// description rather than a plausible wrong one.
/// </summary>
public static class CharaMakeShapeText
{
    /// <summary>Builds the lookup key. Entry is the 1-based menu position, so entry 1
    /// (the untouched base mesh) is never in the table.</summary>
    public static uint Key(uint faceIcon, uint customizeByte, int entry)
        => faceIcon * 1000u + customizeByte * 10u + (uint)entry;

    /// <summary>The full description, or null when this entry has none.</summary>
    public static string? Describe(uint faceIcon, uint customizeByte, int entry)
        => faceIcon != 0 && Text.TryGetValue(Key(faceIcon, customizeByte, entry), out var t)
            ? (Loc.IsGerman ? t.De : t.En)
            : null;

    /// <summary>The one- or two-word form for the cursor move (61e).</summary>
    public static string? Summarize(uint faceIcon, uint customizeByte, int entry)
        => faceIcon != 0 && Text.TryGetValue(Key(faceIcon, customizeByte, entry), out var t)
            ? (Loc.IsGerman ? t.BriefDe : t.BriefEn)
            : null;

    /// <summary>True when this entry has a description.</summary>
    public static bool Has(uint faceIcon, uint customizeByte, int entry)
        => faceIcon != 0 && Text.ContainsKey(Key(faceIcon, customizeByte, entry));

    /// <summary>How many entries the table covers. Used by the mod's own diagnostics
    /// so a regeneration that silently emptied the table cannot pass unnoticed.</summary>
    public static int Count => Text.Count;

    private static readonly Dictionary<uint, (string De, string En, string BriefDe, string BriefEn)> Text = new();

    private static void S(string briefDe, string briefEn, string de, string en, params uint[] keys)
    {
        foreach (var k in keys) Text[k] = (de, en, briefDe, briefEn);
    }

    static CharaMakeShapeText()
    {'''

FOOTER = '''    }
}
'''

if __name__ == '__main__':
    sys.exit(main())
