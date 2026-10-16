"""Build accepted, source-checked game text. Machine drafts are never input."""
import argparse
import base64
import gzip
import hashlib
import json
from pathlib import Path
import re
from prepare_game_text import descriptions

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]

def read(path):
    raw=path.read_bytes()
    if path.suffix=='.gz':raw=gzip.decompress(raw)
    return json.loads(raw.decode('utf-8-sig'))

def main(require_complete=False):
    snapshot=read(HERE/'game-text-sources.json.gz')
    imported=read(HERE/'game-text-imported.json.gz')['sheets']
    # The game repeats some quest items under new row numbers. Reuse an accepted
    # translation only when the complete source and every accepted target agree.
    by_source={}
    for table,entries in imported.items():
        grouped={}
        for entry in entries.values():grouped.setdefault(entry['source'],set()).add(entry['bytes'])
        by_source[table]={source:next(iter(targets)) for source,targets in grouped.items() if len(targets)==1}
    manual=read(HERE/'complete-action-translations.json')
    # Preserve the wording released with the initial crafting/general names.
    manual.update(read(HERE/'action-name-translations.json'))
    manual.update(read(HERE/'game-ui-name-translations.json'))
    terms={s:t for s,t in manual.items() if len(s)>=3 and re.search('[A-Za-z]',s) and re.search('[А-Яа-яЁё]',t)}
    (ROOT/'FF14Accessibility/Resources/RussianDescriptionTerms.json.gz').write_bytes(gzip.compress(json.dumps(terms,ensure_ascii=False,sort_keys=True).encode(),mtime=0))
    items=read(HERE/'item-template-translations.json')
    direct=HERE/'item-direct-translations.json'
    if direct.exists():items.update(read(direct))
    for extra in ('item-misc-translations.json','item-extra-translations.json','item-supplement-translations.json','item-final-misc-translations.json','item-outfit-translations.json','item-final-extra-translations.json'):
        path=HERE/extra
        if path.exists():items.update(read(path))
    events=read(HERE/'event-item-translations.json') if (HERE/'event-item-translations.json').exists() else {}
    corrections=read(HERE/'game-text-corrections.json')
    catalog={};stats={};remaining={};unavailable={}
    for table,rows in snapshot['sheets'].items():
        catalog[table]={};remaining[table]={};unavailable[table]={}
        origins={}
        for row,source in rows.items():
            text=source['text']; target=None; origin=None
            correction=corrections.get(table,{}).get(row)
            if correction:
                if correction['source_base64']!=source['bytes']:
                    raise ValueError(f'Stale correction: {table}/{row}')
                target=descriptions.encode(correction['translation']);origin='manual_row'
            elif table=='ItemName' and text in items:
                target=items[text].encode();origin='exact_item_template_or_manual'
            elif table=='EventItemName' and text in events:
                target=events[text].encode();origin='manual_event_name'
            elif table.endswith('Name') and text in manual:
                target=manual[text].encode();origin='manual_name'
            elif (entry:=imported.get(table,{}).get(row)) and entry['source']==source['bytes']:
                target=base64.b64decode(entry['bytes']);origin='xiv_rus_source_matched'
            elif translated:=by_source.get(table,{}).get(source['bytes']):
                target=base64.b64decode(translated);origin='xiv_rus_identical_source'
            if text.startswith('_rsv_'):
                unavailable[table][row]=text
                continue
            if target is None:
                # A number or punctuation is already language independent.
                if not re.search(r'[^\W\d_]',text,flags=re.UNICODE):
                    target=base64.b64decode(source['bytes']);origin='language_independent'
                else:
                    remaining[table][row]=text
                    continue
            if not target:raise ValueError(f'Empty translation: {table}/{row}')
            catalog[table][row]=[source['bytes'],base64.b64encode(target).decode()]
            origins[origin]=origins.get(origin,0)+1
        stats[table]={'source_rows':len(rows),'translated_rows':len(catalog[table]),
                      'missing_rows':len(remaining[table]),'unresolved_server_keys':len(unavailable[table]),'origins':origins}
    output=ROOT/'FF14Accessibility/Resources/RussianActionNames.json.gz'
    output.write_bytes(gzip.compress(json.dumps(catalog,sort_keys=True,separators=(',',':')).encode(),mtime=0))
    report={'game_version':snapshot['game_version'],'catalog_sha256':hashlib.sha256(output.read_bytes()).hexdigest(),
            'scope':'Named source rows of the captured game tables; server keys lack offline text; no machine drafts included',
            'tables':stats,'remaining':remaining,'unresolved_server_keys':unavailable}
    report['player_and_job_actions']={}
    for group in ('player_action_ids','job_action_ids'):
        ids={str(row) for row in snapshot.get(group,[])}
        report['player_and_job_actions'][group]={'source_rows':len(ids),
            'translated_rows':len(ids & catalog['ActionName'].keys()),
            'missing':{row:snapshot['sheets']['ActionName'][row]['text'] for row in sorted(ids-catalog['ActionName'].keys())}}
    (ROOT/'docs/maintenance/russian-game-text-coverage.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for table,count in stats.items():print(f'{table}: {count["translated_rows"]}/{count["source_rows"]}, missing {count["missing_rows"]}, server keys {count["unresolved_server_keys"]}')
    if require_complete and any(remaining.values()):raise SystemExit('Game text coverage is incomplete; see the report.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--require-complete',action='store_true');args=parser.parse_args();main(args.require_complete)
