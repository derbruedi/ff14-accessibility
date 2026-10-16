"""Exact, offline Russian translations of plugin-authored character descriptions.

The English sentence is the key: changed upstream wording never reuses a stale
translation. Shape vocabulary is applied only to the measured shape table, never
to icon descriptions, names, chat, or arbitrary game text.
"""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
STRING = r'"(?:[^"\\]|\\.)*"'
CALL = re.compile(r'\b(S|F)\(\s*(' + STRING + r')\s*,\s*(' + STRING +
                  r')(?:\s*,\s*(' + STRING + r')\s*,\s*(' + STRING + r'))?')


def phrases(path):
    found = set()
    for match in CALL.finditer(path.read_text(encoding="utf-8-sig")):
        found.add(json.loads(match[3]))
        if match[1] == "S":
            if not match[5]:
                raise ValueError(f"Unrecognised S call: {path}:{match.start()}")
            found.add(json.loads(match[5]))
    return found


def translate_shape(text, vocabulary):
    parts = []
    for phrase in text.split(", "):
        prefix = ""
        for en, ru in (("markedly ", "заметно "), ("slightly ", "немного ")):
            if phrase.startswith(en):
                prefix, phrase = ru, phrase[len(en):]
                break
        if phrase not in vocabulary:
            raise ValueError(f"Missing shape phrase: {phrase}")
        parts.append(prefix + vocabulary[phrase])
    return ", ".join(parts)


def generate(output, report_path):
    vocabulary = json.loads((HERE / "shape-vocabulary.json").read_text(encoding="utf-8"))
    manual = json.loads((HERE / "character-translations.json").read_text(encoding="utf-8"))
    tables = {name: phrases(ROOT / "FF14Accessibility/Services" / f"CharaMake{name}Text.cs")
              for name in ("Icon", "Shape")}
    all_phrases = tables["Icon"] | tables["Shape"]
    unknown = manual.keys() - all_phrases
    if unknown:
        raise ValueError(f"Stale manual translations: {sorted(unknown)}")
    catalog = {text: translate_shape(text, vocabulary) for text in sorted(tables["Shape"])}
    catalog.update(manual)
    for source, translation in catalog.items():
        if not translation.strip() or not re.search("[А-Яа-яЁё]", translation):
            raise ValueError(f"Empty/non-Russian translation: {source}")
    raw = json.dumps(catalog, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(gzip.compress(raw, mtime=0))
    report = {"catalog_sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
              "unique_source_phrases": len(all_phrases), "translated_phrases": len(catalog),
              "tables": {name: {"total": len(values), "translated": len(values & catalog.keys()),
                                "remaining": sorted(values - catalog.keys())}
                         for name, values in tables.items()}}
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Character phrases: {len(catalog)}/{len(all_phrases)} translated.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=ROOT / "FF14Accessibility/Resources/RussianCharacterText.json.gz")
    parser.add_argument("--report", type=Path, default=ROOT / "docs/maintenance/russian-character-coverage.json")
    args = parser.parse_args()
    generate(args.output, args.report)
