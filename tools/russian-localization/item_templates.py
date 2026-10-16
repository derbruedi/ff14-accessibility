"""Exact item-name templates. An unknown component never becomes a guessed name."""
import gzip
import json
from pathlib import Path
import re

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
ROLES={"Fending":"защиты", "Maiming":"увечья", "Striking":"ударов", "Scouting":"разведки",
       "Aiming":"прицеливания", "Healing":"исцеления", "Casting":"заклинаний", "Slaying":"уничтожения",
       "Crafting":"ремесла", "Gathering":"собирательства"}
VARIANTS={"Augmented":"Улучшенная версия", "Replica":"Копия", "Dated":"Старый образец",
          "Aetherial":"Эфирная версия", "Weathered":"Изношенная версия", "Antiquated":"Старинная версия",
          "Idealized":"Идеальная версия", "Ornate":"Украшенная версия", "Prototype":"Прототип",
          "Rarefied":"Коллекционная версия", "Authentic":"Подлинная версия", "Virtu":"Вирту",
          "Prestige":"Престижная версия", "Ultimate":"Абсолютная версия", "Approved":"Одобренная версия"}
COLORS={"Black":"чёрный", "White":"белый", "Red":"красный", "Blue":"синий", "Green":"зелёный",
        "Yellow":"жёлтый", "Brown":"коричневый", "Grey":"серый", "Gray":"серый", "Purple":"фиолетовый",
        "Pink":"розовый", "Orange":"оранжевый"}

def split_name(name,heads):
    name=name.strip()
    variants=[]; suffix=[]
    level=re.search(r' \(IL (\d+)\)$',name)
    if level:
        suffix.append('уровень предмета '+level[1]);name=name[:level.start()]
    number=re.search(r' (\+[1-9]\d*)$',name)
    if number:
        suffix.append(number[1]);name=name[:number.start()]
    roman=re.search(r' (VIII|XIII|XVIII|XVII|XVI|XIV|XII|VII|III|XV|XI|VI|IV|IX|II|XX|X|V|I)$',name)
    if roman:
        suffix.append(roman[1]);name=name[:roman.start()]
    color=re.search(r' \(('+'|'.join(COLORS)+r')\)$',name)
    if color:
        suffix.append('цвет: '+COLORS[color[1]]);name=name[:color.start()]
    for _ in range(4):
        found=next((v for v in VARIANTS if name.startswith(v+' ')),None)
        if not found:break
        variants.append(found);name=name[len(found)+1:]
    role=next((r for r in ROLES if name.endswith(' of '+r)),None)
    if role:name=name[:-len(' of '+role)]
    head=next((h for h in sorted(heads,key=len,reverse=True) if name==h or name.endswith(' '+h)),None)
    modifier=name[:-len(head)].strip() if head else name
    return modifier,head,role,variants,suffix

MATERIAL_CATEGORIES=set(range(1,44)) | set(range(48,54)) | {56,57} | set(range(64,81)) | {84,87,88,89,90,91,92,93,96,97,98,99,101,102,103,104,105,106,107,108,109,110,111,112,113}

def resolve_modifier(modifier,modifiers,depth=0):
    if modifier in modifiers:return modifiers[modifier]
    if depth>3:return None
    numbered=re.fullmatch(r'(Grade|Season|Class|Rank) (\d+) (.+)',modifier)
    if numbered:
        part=resolve_modifier(numbered[3],modifiers,depth+1)
        if part is not None:
            label={'Grade':'сорт','Season':'сезон','Class':'класс','Rank':'ранг'}[numbered[1]]
            return part+', '+label+' '+numbered[2]
    for prefix,label in (("Artisanal Skybuilders'",'искусных небесных строителей'),
                         ("Skybuilders'",'небесных строителей')):
        if modifier.startswith(prefix+' '):
            part=resolve_modifier(modifier[len(prefix)+1:],modifiers,depth+1)
            if part is not None:return part+', '+label
    # Each rule consumes an entire recognised material/series, never an unknown word.
    for prefix,label in (('Altered','переделанный образец'),('Heavy','тяжёлый образец'),
                         ('Decorated','с украшениями'),('Reinforced','укреплённый образец'),
                         ('Plumed','с перьями'),('Fingerless','без пальцев'),
                         ('Padded','с подкладкой'),('Engraved','с гравировкой'),
                         ('Oddly Specific','особого назначения'),('Oddly Delicate','особо тонкий образец'),
                         ('Cosmotized','космическое усиление'),('Tarnished','потускневший образец'),
                         ('Spiked','с шипами'),('Polished','полированный образец')):
        if modifier.startswith(prefix+' '):
            part=resolve_modifier(modifier[len(prefix)+1:],modifiers,depth+1)
            if part is not None:return part+', '+label
    return None

def translate(name,heads,modifiers,categories=None):
    modifier,head,role,variants,suffix=split_name(name,heads)
    if not head:return None
    qualifier=resolve_modifier(modifier,modifiers)
    if modifier and qualifier is None:return None
    # "Silver Shark" is an epithet of a fish, not a shark made of silver.
    # Source categories keep material grammar out of food, fauna and currencies.
    allowed=MATERIAL_CATEGORIES | ({61} if head.endswith('Coffer') or 'Rarefied' in variants else set())
    if head in {'Ingot','Nugget','Plate','Plating','Nails','Rivets','Shaft','Gear','Gauntlets','Trident','Kote',
                'Lumber','Leather','Cloth','Cluster','Fragment'}:
        allowed=allowed | {63}
    if qualifier and qualifier.startswith('из ') and categories is not None and not set(categories)<=allowed:
        return None
    result=heads[head]
    if modifier:result+=' '+qualifier
    if role:result+=' для '+ROLES[role]
    for variant in reversed(variants):result=VARIANTS[variant]+': '+result[0].lower()+result[1:]
    if suffix:result+=', '+', '.join(suffix)
    return result[0].upper()+result[1:]

def load_modifiers():
    modifiers=json.loads((HERE/'item-modifiers.json').read_text(encoding='utf-8-sig'))
    for name in ('item-modifiers-more.tsv','item-modifiers-series.tsv','item-modifiers-extra.tsv','item-modifiers-final.tsv'):
        path=HERE/name
        if path.exists():
            modifiers.update(line.split('\t',1) for line in path.read_text(encoding='utf-8-sig').splitlines() if line)
    return modifiers

def main():
    heads=json.loads((HERE/'item-heads.json').read_text(encoding='utf-8-sig'))
    modifiers=load_modifiers()
    (ROOT/'.local').mkdir(exist_ok=True)
    snapshot=json.loads(gzip.decompress((HERE/'game-text-sources.json.gz').read_bytes()))
    category_by_name={}
    for row,source in snapshot['sheets']['ItemName'].items():
        category_by_name.setdefault(source['text'],set()).add(snapshot['item_categories'].get(row,0))
    names=sorted(category_by_name)
    result={s:t for s in names if (t:=translate(s,heads,modifiers,category_by_name[s])) is not None}
    missing={}
    for name in names:
        if name in result:continue
        modifier,head,*_=split_name(name,heads)
        if head:missing[modifier]=missing.get(modifier,0)+1
    (HERE/'item-template-translations.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (ROOT/'.local/item-pending-modifiers.json').write_text(json.dumps(dict(sorted(missing.items(),key=lambda x:(-x[1],x[0]))),ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'.local/item-pending-full.json').write_text(json.dumps([s for s in names if s not in result],ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'Exact templates: {len(result)}/{len(names)}, unknown modifiers: {len(missing)}')

if __name__=='__main__':main()
