"""Exact compositional families with explicit Russian gender and possessives."""
from functools import lru_cache
from pathlib import Path
import re

HERE = Path(__file__).resolve().parent


def tsv(name):
    width = {'enemy-action-heads.tsv': 3, 'enemy-action-modifiers.tsv': 5,
             'enemy-action-genitives.tsv': 2, 'enemy-action-owners.tsv': 2}[name]
    rows, seen = [], set()
    for line in (HERE / name).read_text(encoding='utf-8').splitlines():
        if not line or line.startswith('#'):
            continue
        row = line.split('\t')
        if len(row) != width or row[0] in seen:
            raise ValueError(f'Malformed or duplicate dictionary entry: {name}: {line}')
        if name == 'enemy-action-heads.tsv' and row[2] not in 'mfnp':
            raise ValueError(f'Invalid grammatical gender: {name}: {line}')
        seen.add(row[0])
        rows.append(row)
    return rows


HEADS = {en: (ru, gender) for en, ru, gender in tsv('enemy-action-heads.tsv')}
MODIFIERS = {row[0]: dict(zip('mfnp', row[1:])) for row in tsv('enemy-action-modifiers.tsv')}
GENITIVES = dict(tsv('enemy-action-genitives.tsv'))
OWNERS = dict(tsv('enemy-action-owners.tsv'))
RANKS = {'I': 1, 'II': 2, 'III': 3, 'IV': 4, 'V': 5, 'VI': 6, 'VII': 7,
         'VIII': 8, 'IX': 9, 'X': 10, 'XI': 11, 'XII': 12}


@lru_cache(maxsize=None)
def phrase(name):
    """No guesses: return None unless every component has an authored rule."""
    if name in HEADS:
        return HEADS[name]
    # Compound spell names use the same explicitly authored noun and adjective.
    for prefix, modifier in [('Aether', 'Aetherial'), ('Hydro', 'Water'), ('Electro', 'Electric'),
                             ('Pyro', 'Fire'), ('Cryo', 'Ice'), ('Dark', 'Dark'),
                             ('Shadow', 'Shadow'), ('Ice', 'Ice'), ('Fire', 'Fire'),
                             ('Flame', 'Flame'), ('Thunder', 'Thunder'), ('Levin', 'Thunder'),
                             ('Frost', 'Frost'), ('Light', 'Light'), ('Earth', 'Earth'),
                             ('Stone', 'Stone'), ('Rock', 'Rock'), ('Star', 'Stellar'),
                             ('Moon', 'Lunar'), ('Sun', 'Solar'), ('Void', 'Void'),
                             ('Dragon', 'Dragon'), ('Blood', 'Blood'), ('Dream', 'Dream'),
                             ('Hell', 'Hellish'), ('Wind', 'Wind'), ('Death', 'Death')]:
        if name.startswith(prefix) and len(name) > len(prefix) and ' ' not in name:
            remainder = name[len(prefix):]
            base = HEADS.get(remainder[:1].upper() + remainder[1:])
            if base:
                return MODIFIERS[modifier][base[1]] + ' ' + base[0][:1].lower() + base[0][1:], base[1]
    for article in ('the ', 'The ', 'a ', 'A '):
        if name.startswith(article):
            return phrase(name[len(article):])
    rank = re.fullmatch(r'(.+) ([IVX]+)', name)
    if rank and rank[2] in RANKS and (base := phrase(rank[1])):
        return base[0] + ' ' + str(RANKS[rank[2]]), base[1]
    greek = re.fullmatch(r'(.+) ([αβγδ])', name)
    if greek and (base := phrase(greek[1])):
        return base[0] + ' ' + {'α': 'альфа', 'β': 'бета', 'γ': 'гамма', 'δ': 'дельта'}[greek[2]], base[1]
    if ' of ' in name:
        head, owner = name.rsplit(' of ', 1)
        if owner in GENITIVES and (base := phrase(head)):
            return base[0] + ' ' + GENITIVES[owner], base[1]
    for prefix, possessive in [('Soul ', 'души'), ('Mana ', 'маны'),
                              ('Abyssal ', 'бездны'), ('Choco ', 'чокобо'),
                              ("Hemitheos's ", 'полубога'), ("Archon's ", 'архонта')]:
        if name.startswith(prefix) and (base := phrase(name[len(prefix):])):
            return base[0] + ' ' + possessive, base[1]
    for prefix in sorted(MODIFIERS, key=len, reverse=True):
        if name.startswith(prefix + ' ') and (base := phrase(name[len(prefix) + 1:])):
            modifier = MODIFIERS[prefix][base[1]]
            separator = '' if modifier.endswith('-') else ' '
            return modifier + separator + base[0][:1].lower() + base[0][1:], base[1]
    for prefix in sorted(OWNERS, key=len, reverse=True):
        if name.startswith(prefix + ' ') and (base := phrase(name[len(prefix) + 1:])):
            return base[0] + ' ' + OWNERS[prefix], base[1]
    return None


def translate(name):
    stripped = name.strip()
    if match := re.fullmatch(r'([0-9,]+) Needles', stripped):
        return match[1].replace(',', ' ') + ' игл'
    if match := re.fullmatch(r'([0-9,]+)-(tonze|stone|kuponze|mina) (.+)', stripped, re.IGNORECASE):
        base = phrase(match[3]) or phrase(match[3][:1].upper() + match[3][1:])
        if base:
            unit = {'tonze': 'тонн', 'stone': 'стоунов', 'kuponze': 'купонз', 'mina': 'мин'}[match[2].lower()]
            return base[0] + ' весом ' + match[1].replace(',', ' ') + ' ' + unit
    if match := re.fullmatch(r'(-?[0-9]+) Gs', stripped):
        return 'Перегрузка: ' + match[1] + ' же'
    result = phrase(stripped)
    return result[0] if result else None
