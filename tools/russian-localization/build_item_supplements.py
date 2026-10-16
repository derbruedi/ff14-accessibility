"""Expand finite, reviewed book/currency series; unknown components stay untranslated."""
import gzip
import json
from pathlib import Path
import re

from item_templates import translate as item_template, resolve_modifier, load_modifiers

HERE=Path(__file__).resolve().parent
def read(name):
    raw=(HERE/name).read_bytes()
    if name.endswith('.gz'):raw=gzip.decompress(raw)
    return json.loads(raw.decode('utf-8-sig'))

def main():
    series=read('item-supplement-series.json')
    heads=read('item-heads.json');modifiers=load_modifiers()
    supplement_heads={**heads,**series['heads']}
    names={}
    for file in ('item-template-translations.json','item-direct-translations.json','item-misc-translations.json','item-extra-translations.json','item-final-extra-translations.json'):
        if (HERE/file).exists():names.update(read(file))
    actions=read('complete-action-translations.json')
    normalized={}
    for source,target in actions.items():normalized.setdefault(source.casefold(),set()).add(target)
    def exact_name(source):
        if source in names:return names[source]
        targets=normalized.get(source.casefold(),set())
        if len(targets)==1:
            target=next(iter(targets));return target[0].upper()+target[1:]
        return item_template(source,heads,modifiers)
    def suffix(source,heading,terms):
        if source.startswith(heading):return terms.get(source[len(heading):])
        return None
    def convert(source):
        if (title:=suffix(source,'Painting of ',series['paintings'])):return 'Картина «'+title+'»'
        if (title:=suffix(source,'Maxims of Mahjong - ',series['mahjong'])):return 'Наставления по маджонгу: '+title
        if source.startswith('Field Notes on '):
            key=source[len('Field Notes on '):].removeprefix('the ')
            if key in series['fields']:return 'Полевые записи: '+series['fields'][key]
        m=re.fullmatch(r"(?:Grade (\d+) )?(Artisanal )?Skybuilders' (.+)",source)
        if m and m[3] in series['skybuilders']:
            return series['skybuilders'][m[3]]+(' искусных' if m[2] else '')+' небесных строителей'+(', сорт '+m[1] if m[1] else '')
        m=re.fullmatch(r"Highly Viscous (.+)'s Gobbiegoo",source)
        if m and m[1] in series['job']:return 'Особо вязкий гобби-клей '+series['job'][m[1]]
        m=re.fullmatch(r"(Obsolete )?Resplendent (.+)'s (Component|Material) ([ABC])",source)
        if m and m[2] in series['job']:
            title=('Деталь' if m[3]=='Component' else 'Материал')+' великолепного инструмента '+series['job'][m[2]]+', '+m[4]
            return ('Устаревший образец: '+title[0].lower()+title[1:]) if m[1] else title
        m=re.fullmatch(r'(Unsung )?(Helm|Armor|Gauntlets|Chausses|Greaves|Belt|Bangle|Ring|Blade) of (Lost Antiquity|Asphodelos|Abyssos|Anabaseios)',source)
        if m:
            head={'Helm':'Шлем','Armor':'Доспех','Gauntlets':'Латные перчатки','Chausses':'Шоссы','Greaves':'Наголенники','Belt':'Пояс','Bangle':'Браслет','Ring':'Кольцо','Blade':'Клинок'}[m[2]]
            area={'Lost Antiquity':'утраченной древности','Asphodelos':'Асфоделоса','Abyssos':'Абиссоса','Anabaseios':'Анабазейоса'}[m[3]]
            adjective=('Невоспетое' if m[2]=='Ring' else 'Невоспетые' if m[2] in {'Gauntlets','Chausses','Greaves'} else 'Невоспетый')
            return (adjective+' '+head.lower() if m[1] else head)+' '+area
        m=re.fullmatch(r'(Asphodelos|Abyssos|Anabaseios) Mythos ([IVX]+)',source)
        if m:return 'Мифы '+{'Asphodelos':'Асфоделоса','Abyssos':'Абиссоса','Anabaseios':'Анабазейоса'}[m[1]]+' '+m[2]
        m=re.fullmatch(r'(Light-heavy|Cruiser|Heavy) Holo(helm|armor|gauntlets|chausses|greaves|earring|blade|surcoat|trousers|sabatons|brooch|saber|ring)',source)
        if m:
            head={'helm':'Голошлем','armor':'Голодоспех','gauntlets':'Голоперчатки','chausses':'Голошоссы','greaves':'Голонаголенники','earring':'Голосерьга','blade':'Голоклинок','surcoat':'Голосюрко','trousers':'Голобрюки','sabatons':'Голосабатоны','brooch':'Голоброшь','saber':'Голосабля','ring':'Голокольцо'}[m[2]]
            return head+' '+{'Light-heavy':'полутяжёлого','Cruiser':'первого тяжёлого','Heavy':'тяжёлого'}[m[1]]+' веса'
        m=re.fullmatch(r'AAC Illustrated: (LHW|CW|HW) Edition ([IVX]+)',source)
        if m:return 'Иллюстрированный Аркадион: '+{'LHW':'полутяжёлый','CW':'первый тяжёлый','HW':'тяжёлый'}[m[1]]+' вес, выпуск '+m[2]
        m=re.fullmatch(r"(Bozjan Runner's Secrets|Runner's Plating|Orderly Runner's Secrets) \((Head|Body|Hand|Leg|Foot) Gear\)",source)
        if m:return {'Bozjan Runner\'s Secrets':'Тайны бозянского бегуна','Runner\'s Plating':'Пластины бегуна','Orderly Runner\'s Secrets':'Тайны дисциплинированного бегуна'}[m[1]]+': '+{'Head':'голова','Body':'тело','Hand':'руки','Leg':'ноги','Foot':'ступни'}[m[2]]
        m=re.fullmatch(r'(Hannish|Everkeep) Certificate of Grade (\d+) Import',source)
        if m:return 'Сертификат импорта '+{'Hannish':'Радз-ат-Хана','Everkeep':'Вечной крепости'}[m[1]]+', сорт '+m[2]
        for prefix,label,key in (('Modern Aesthetics - ','Современная эстетика: ','aesthetics'),
                                 ('Ballroom Etiquette - ','Бальный этикет: ','etiquette')):
            if (title:=suffix(source,prefix,series[key])):return label+title
        m=re.fullmatch(r'Magicked Prism \((.+)\)',source)
        if m and m[1] in series['prism']:return 'Магическая призма: '+series['prism'][m[1]]
        m=re.fullmatch(r'(Primed )?Grade (\d+) Wheel of (.+)',source)
        if m and m[3] in series['wheel']:
            return ('Заряженное ' if m[1] else '')+'Эфирное колесо '+series['wheel'][m[3]]+', сорт '+m[2]
        m=re.fullmatch(r'(Fledgling Chocobo Registration|Retired Chocobo Registration|Covering Permission) G(\d+)-([MF])',source)
        if m:
            title={'Fledgling Chocobo Registration':'Свидетельство молодого чокобо','Retired Chocobo Registration':'Свидетельство чокобо на покое','Covering Permission':'Разрешение на спаривание'}[m[1]]
            return title+': поколение '+m[2]+', '+('самец' if m[3]=='M' else 'самка')
        m=re.fullmatch(r'Chocobo Training Manual - (.+?)( (?:I|II|III|IV|V))?',source)
        if m and m[1] in series['chocobo']:return 'Учебник для чокобо: '+series['chocobo'][m[1]]+(m[2] or '')
        m=re.fullmatch(r'Tome of (Geological|Botanical|Ichthyological) Folklore - (.+)',source)
        if m and m[2] in series['location']:
            field={'Geological':'геологических','Botanical':'ботанических','Ichthyological':'рыболовных'}[m[1]]
            return 'Книга '+field+' преданий: '+series['location'][m[2]]
        m=re.fullmatch(r"(Traders'|Matron's) Favor \((.+)\)",source)
        if m and m[2] in series['location']:return ('Благосклонность Торговцев' if m[1]=="Traders'" else 'Благосклонность Матроны')+': '+series['location'][m[2]]
        m=re.fullmatch(r'(.+) for Beginners',source)
        if m and m[1] in series['craft']:return series['craft'][m[1]]+' для начинающих'
        m=re.fullmatch(r'(.+) Delineation',source)
        if m and m[1] in series['craft']:return 'Схема: '+series['craft'][m[1]].lower()
        m=re.fullmatch(r'Type-(\d+) (.+) (Counterfoil|Receipt)',source)
        if m and m[2] in series['craft']:return ('Талон' if m[3]=='Counterfoil' else 'Квитанция')+' типа '+m[1]+': '+series['craft'][m[2]].lower()
        m=re.fullmatch(r'Master (.+): Glamours',source)
        if m and m[1] in series['job']:return 'Книга мастера '+series['job'][m[1]]+': гламуры'
        m=re.fullmatch(r'(?:Grade (\d+) )?Glamour Prism \((.+)\)',source)
        if m and m[2] in series['craft']:return 'Призма гламура: '+series['craft'][m[2]].lower()+(', сорт '+m[1] if m[1] else '')
        m=re.fullmatch(r'Atma of the (.+)',source)
        if m and m[1] in series['constellation']:return 'Атма '+series['constellation'][m[1]]
        m=re.fullmatch(r'(Irregular Tomestone|Forgotten Fragment) of (.+)',source)
        if m and m[2] in series['concept']:return ('Необычный томокамень ' if m[1]=='Irregular Tomestone' else 'Забытый фрагмент ')+series['concept'][m[2]]
        m=re.fullmatch(r"Rowena's Token \((.+)\)",source)
        if m and m[1] in series['concept']:return 'Жетон Ровены: '+series['concept'][m[1]]
        m=re.fullmatch(r'(.+) Manifesto - Page (\d+)',source)
        if m and (term:=resolve_modifier(m[1],modifiers)):return 'Манифест '+term+', страница '+m[2]
        m=re.fullmatch(r'(.+) Datalog v(\d+\.\d+)',source)
        if m and (term:=resolve_modifier(m[1],modifiers)):return 'Журнал данных '+term+', версия '+m[2]
        m=re.fullmatch(r'(Season .+) Voucher ([A-Z])',source)
        if m:
            season=re.fullmatch(r'Season (.+) (Lone Wolf|Pack Wolf)',m[1])
            numbers='One Two Three Four Five Six Seven Eight Nine Ten Eleven Twelve Thirteen Fourteen Fifteen Sixteen Seventeen Eighteen Nineteen Twenty Twenty-one Twenty-two'.split()
            if season and season[1] in numbers:
                return 'Ваучер «'+('Одинокий волк' if season[2]=='Lone Wolf' else 'Стайный волк')+'», сезон '+str(numbers.index(season[1])+1)+', '+m[2]
            if m[1] in modifiers:return 'Ваучер '+modifiers[m[1]]+', '+m[2]
        m=re.fullmatch(r'Tales of Adventure: One (.+?)\'s Journey( [IVX]+(?:[-–][IVX]+)?)?',source)
        if m and m[1] in series['job']:return 'Истории приключений: путь '+series['job'][m[1]]+(m[2] or '')
        if (term:=suffix(source,'Tales of Adventure: ',series['chapter'])):return 'Истории приключений: '+term
        m=re.fullmatch(r'('+'|'.join(series['colors'])+r') (.+)',source)
        if m and m[2] in series['flowers']:return series['colors'][m[1]]+' '+series['flowers'][m[2]]
        m=re.fullmatch(r'Grade (\d+) (La Noscean|Shroud|Thanalan) Topsoil',source)
        if m:return 'Плодородная почва '+{'La Noscean':'Ла Носкеа','Shroud':'Покрова','Thanalan':'Таналана'}[m[2]]+', сорт '+m[1]
        if source.startswith('Faded Copy of '):
            base=source[len('Faded Copy of '):]
            # The source is explicitly a score, so reusing the music title is unambiguous.
            title=names.get(base+' Orchestrion Roll')
            if title and '«' in title:return 'Выцветшая копия нот '+title[title.index('«'):]
            title=modifiers.get(base)
            if title and title.startswith('«'):return 'Выцветшая копия нот '+title
            for key,value in modifiers.items():
                if key.casefold()==base.casefold() and value.startswith('«'):return 'Выцветшая копия нот '+value
        if source.startswith('Sphere Scroll: '):
            title=exact_name(source[len('Sphere Scroll: '):])
            if title:return 'Свиток сферы: '+title
        if source.startswith('Allagan Runestone - '):
            title=resolve_modifier(source[len('Allagan Runestone - '):],modifiers)
            if title:return 'Аллаганский рунный камень '+title
        # These labels reference one exact existing item; no guessing of components.
        for prefix,label in (('Modified ','Модифицированная версия: '),('Obsolete ','Устаревший образец: ')):
            if source.startswith(prefix):
                title=names.get(source[len(prefix):])
                if title:return label+title[0].lower()+title[1:]
        return None
    snapshot=read('game-text-sources.json.gz')
    categories={}
    for row,source in snapshot['sheets']['ItemName'].items():
        categories.setdefault(source['text'],set()).add(snapshot['item_categories'].get(row,0))
    result={}
    for source in sorted({row['text'] for row in snapshot['sheets']['ItemName'].values()}):
        target=convert(source)
        # The extra noun meanings are for materials, currency and miscellaneous
        # supplies. In particular they must not turn fish names into materials.
        if not target and source not in names and categories[source]<={48,50,51,52,53,54,55,56,60,61,63,82,83,85,91,92,93,94,95,100,101,102,103,104,105,112}:
            target=item_template(source,supplement_heads,modifiers,categories[source])
        if target:result[source]=target
    (HERE/'item-supplement-translations.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Accepted supplement names:',len(result))

if __name__=='__main__':main()
