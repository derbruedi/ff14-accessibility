"""Build guarded translations for the 50 beast-book entries and FF15 FATE."""
import base64
import gzip
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
source = json.loads((ROOT.parents[1] / 'tests/Regression.Tests/Fixtures/author-text-sources.json').read_text(encoding='utf-8'))
translations = json.loads((ROOT / 'author-beast-translations.json').read_text(encoding='utf-8'))
assert {p['number'] for p in source['pets']} == set(range(1, 51))
assert set(translations) == {str(n) for n in range(1, 51)}
result = {'PetName': {}, 'XBMPetDescription': {}, 'FateName': {}, 'AddonText': {}, 'ENpcResidentName': {}}
giver_translations = json.loads((ROOT / 'author-giver-translations.json').read_text(encoding='utf-8'))
giver_names = giver_translations['imported'] | giver_translations['manual']
assert set(giver_names) == {str(row['id']) for row in source['givers']}
for row in source['givers']:
    russian = giver_names[str(row['id'])]
    assert base64.b64decode(row['name_bytes']) == row['name'].encode('utf-8')
    assert russian and not any(c.isascii() and c.isalpha() for c in russian)
    result['ENpcResidentName'][str(row['id'])] = [row['name_bytes'],
        base64.b64encode(russian.encode('utf-8')).decode()]
for pet in source['pets']:
    translated = translations[str(pet['number'])]
    for sheet, row_id, field in [('PetName', pet['pet_id'], 'name'),
                                  ('XBMPetDescription', pet['number'], 'description')]:
        original = pet[field + '_bytes']
        # These measured fields contain plain UTF-8, so no payload is lost.
        assert base64.b64decode(original) == pet[field].encode('utf-8')
        assert translated[field] and not any(c.isascii() and c.isalpha() for c in translated[field])
        result[sheet][str(row_id)] = [original, base64.b64encode(translated[field].encode('utf-8')).decode()]
result['FateName']['1409'] = [source['fate']['en_bytes'],
    base64.b64encode('Как по часам'.encode('utf-8')).decode()]
lottery_ru = {
    9260: 'Мини-кактпот', 9262: 'Открой клетки', 9263: 'Выбери линию для подсчёта суммы.',
    9264: 'Подтвердить', 9265: 'Выигрыш', 9266: 'Сумма', 9267: 'МГП',
    9269: 'ОК', 9270: 'Отмена', 9271: 'Закрыть', 9272: 'Джамбо-кактпот',
    9274: 'Случайный номер', 9275: 'Купить', 9277: 'Выигрыш', 9279: 'Выигрышный номер',
    9281: 'Первый приз', 9282: 'Совпали все цифры', 9283: 'Второй приз',
    9284: 'Совпали последние три цифры', 9285: 'Третий приз',
    9286: 'Совпали последние две цифры', 9287: 'Четвёртый приз',
    9288: 'Совпала последняя цифра', 9289: 'Утешительный приз', 9290: 'Нет совпавших цифр',
}
lottery = {}
for row in source['lottery']:
    if row['id'] not in lottery_ru:
        continue
    result['AddonText'][str(row['id'])] = [row['en_bytes'],
        base64.b64encode(lottery_ru[row['id']].encode('utf-8')).decode()]
    if b'\x02' not in base64.b64decode(row['en_bytes']):
        lottery[str(row['id'])] = [row['en'], row['de']]
target = ROOT.parents[1] / 'FF14Accessibility/Resources/RussianAuthorText.json.gz'
payload = json.dumps(result, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
target.write_bytes(gzip.compress(payload, mtime=0))
(target.parent / 'RussianLotteryLabels.json').write_text(
    json.dumps(lottery, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f"{len(result['PetName'])} names, {len(result['XBMPetDescription'])} descriptions, 1 FATE")
