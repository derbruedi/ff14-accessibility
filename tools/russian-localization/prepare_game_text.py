"""Import only source-matched XIV Rus fields; list texts still needing translation."""
import argparse
import base64
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import urllib.request
import urllib.error
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
REVISION = '6a2733af6df0f86b133bcbf61c553dfacf80ec2d'
spec = importlib.util.spec_from_file_location('descriptions', HERE.parent/'russian-descriptions/generate.py')
descriptions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(descriptions)

def main(download):
    snapshot=json.loads(gzip.decompress((HERE/'game-text-sources.json.gz').read_bytes()))
    cache=ROOT/'.local/game-text-xliff'
    cache.mkdir(parents=True,exist_ok=True)
    imported={}
    hashes={}
    for key,rows in snapshot['sheets'].items():
        sheet=re.sub('(Name|Description|Tooltip)$','',key)
        versions={}
        for lang in ('en','ru'):
            path=cache/f'{sheet}.{lang}.xlf'
            if download or not path.exists():
                try:
                    with urllib.request.urlopen(f'https://raw.githubusercontent.com/xivrus/xiv_ru_weblate/{REVISION}/exd/{sheet}/{lang}.xlf') as response:
                        path.write_bytes(response.read())
                except urllib.error.HTTPError as ex:
                    if ex.code!=404:raise
                    versions[lang]={}
                    print(f'{sheet}.{lang}: not present in pinned upstream snapshot',flush=True)
                    continue
            hashes[path.name]=hashlib.sha256(path.read_bytes()).hexdigest()
            versions[lang]={r.attrib['id']:(r.findtext('target') or '').split('<tab>') for r in ET.parse(path).findall('.//trans-unit')}
        imported[key]={}
        for row,source in rows.items():
            en=versions['en'].get(row,[]); ru=versions['ru'].get(row,[])
            for col,original in enumerate(en):
                if col>=len(ru) or not re.search('[А-Яа-яЁё]',ru[col]): continue
                try:
                    if descriptions.encode(original)!=base64.b64decode(source['bytes']): continue
                    translated=descriptions.encode(ru[col])
                except ValueError:
                    continue
                imported[key][row]={'source':source['bytes'],'translation':ru[col], 'bytes':base64.b64encode(translated).decode()}
                break
        print(f'{key}: imported {len(imported[key])}/{len(rows)}',flush=True)
    result={'revision':REVISION,'source_files':hashes,'sheets':imported}
    (HERE/'game-text-imported.json.gz').write_bytes(gzip.compress(json.dumps(result,ensure_ascii=False,sort_keys=True).encode(),mtime=0))
    pending={source['text'] for table,rows in snapshot['sheets'].items() for row,source in rows.items() if row not in imported[table]}
    (ROOT/'.local/game-text-pending.json').write_text(json.dumps(sorted(pending),ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'{len(pending)} unique pending texts')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--download',action='store_true');args=p.parse_args();main(args.download)
