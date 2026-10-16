"""Expand human-written names and exact grammar families for captured game rows.

The pinned, byte-matched XIV Rus import supplies established names. English
translation targets, unresolved server keys, and guesses are never accepted.
"""
import argparse
import gzip
import json
from pathlib import Path
import re
from enemy_name_patterns import translate as translate_enemy_pattern

HERE = Path(__file__).resolve().parent
TABLES = ('ActionName', 'CraftActionName', 'GeneralActionName', 'BuddyActionName',
          'TraitName', 'PetActionName', 'MountName', 'CompanionName', 'StatusName')
ROMAN = {value: number for number, value in enumerate(
    ['', 'I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI',
     'XII', 'XIII', 'XIV', 'XV', 'XVI', 'XVII', 'XVIII', 'XIX', 'XX'])}


def read(path):
    data = path.read_bytes()
    if path.suffix == '.gz':
        data = gzip.decompress(data)
    return json.loads(data.decode('utf-8-sig'))


def build():
    snapshot = read(HERE / 'game-text-sources.json.gz')['sheets']
    imports = read(HERE / 'game-text-imported.json.gz')['sheets']
    glossary = {}
    for table in TABLES:
        for row, entry in imports.get(table, {}).items():
            original = snapshot[table].get(row)
            value = entry['translation']
            if (original and original['bytes'] == entry['source']
                    and re.search('[А-Яа-яЁё]', value)
                    and not re.search('[A-Za-z<>]', value)):
                glossary.setdefault(original['text'], value)
    for filename in ('manual-action-names.tsv', 'manual-trait-names.tsv',
                     'manual-job-action-names.tsv', 'manual-status-names.tsv',
                     'manual-enemy-action-names.tsv'):
        path = HERE / filename
        if not path.exists():
            continue
        seen = set()
        for line in path.read_text(encoding='utf-8').splitlines():
            if not line or line.startswith('#'):
                continue
            source, target = line.split('\t', 1)
            if source in seen:
                raise ValueError(f'Duplicate manual key in {filename}: {source}')
            seen.add(source)
            if not re.search('[А-Яа-яЁё]', target):
                raise ValueError(f'No Russian translation in {filename}: {source}')
            glossary[source] = target
    # Wording already shipped takes precedence over new editorial choices.
    glossary.update(read(HERE / 'action-name-translations.json'))

    def translate(name):
        if name in glossary:
            return glossary[name]
        if name[:1].islower() and name[:1].upper() + name[1:] in glossary:
            return glossary[name[:1].upper() + name[1:]]
        if name.startswith('The ') and 'the ' + name[4:] in glossary:
            return glossary['the ' + name[4:]]
        if name.startswith('the ') and 'The ' + name[4:] in glossary:
            return glossary['The ' + name[4:]]
        if name.endswith(' (Limited)'):
            base = translate(name[:-10])
            if base:
                return base + ' (ограниченное действие)'
        plus = re.fullmatch(r'(.+) ([+][0-9]+)', name)
        if plus and (base := translate(plus[1])):
            return base + ' ' + plus[2]
        for suffix, label in [(' Ready', 'Готовность: '), (' Primed', 'Подготовлено: '),
                              (' Charged', 'Заряжено: '), (' Up', 'Повышение: '),
                              (' Down', 'Понижение: ')]:
            if name.endswith(suffix) and (base := translate(name[:-len(suffix)])):
                return label + base
        for prefix, label in [('Lost ', 'Забытое действие'), ('Occult ', 'Оккультное действие'),
                              ('Variant ', 'Вариативное действие'),
                              ('PvP Role Action: ', 'Ролевое действие против игроков')]:
            if name.startswith(prefix) and (base := translate(name[len(prefix):])):
                return label + ' «' + base + '»'
        for prefix, label in [('Leveilleur ', 'Левейёра'), ('Ronkan ', 'Ронки')]:
            if name.startswith(prefix) and (base := translate(name[len(prefix):])):
                return base + ' ' + label
        if name.endswith(' L') and (base := translate(name[:-2])):
            return 'Действие логоса «' + base + '»'
        rank = re.fullmatch(r'(.+) ([IVX]+)', name)
        if rank and rank[2] in ROMAN and (base := translate(rank[1])):
            return base + ' ' + str(ROMAN[rank[2]])
        if name.startswith('Enhanced ') and (base := translate(name[9:])):
            return 'Улучшение «' + base + '»'
        if name.endswith(' Mastery') and (base := translate(name[:-8])):
            return 'Мастерство «' + base + '»'
        return translate_enemy_pattern(name)

    universe = {v['text'] for table in TABLES for v in snapshot[table].values()
                if not v['text'].startswith('_rsv_')}
    result = {source: target for source in sorted(universe) if (target := translate(source))}
    remaining = {table: sorted({v['text'] for v in snapshot[table].values()
                                if v['text'] not in result and not v['text'].startswith('_rsv_')})
                 for table in TABLES}
    return result, remaining


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--remaining', type=Path)
    args = parser.parse_args()
    result, remaining = build()
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if args.remaining:
        args.remaining.write_text(json.dumps(remaining, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'{len(result)} translated names')
    for table, values in remaining.items():
        print(f'{table}: {len(values)} untranslated unique names')
