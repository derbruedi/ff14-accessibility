"""Build the offline spoken-description catalog from a pinned XIV Rus revision.

No game files are changed. XLIFF payloads are encoded as SeStrings, not stripped:
the game's evaluator must retain level-dependent numbers and conditional text.
See https://github.com/xivrus/exd_conversion_tools/blob/main/lib/Engine.psm1
for the published <var HEX ... ((string)) /var> interchange format.
"""
import argparse
import base64
import gzip
import hashlib
import json
from pathlib import Path
import re
import urllib.request
import xml.etree.ElementTree as ET

REVISION = "6a2733af6df0f86b133bcbf61c553dfacf80ec2d"
FIELDS = {
    "Item": {"Item": 2},
    "ActionTransient": {"Action": 0},
    "CraftAction": {"CraftAction": 1},
    "TraitTransient": {"Trait": 0},
    "BuddyAction": {"BuddyAction": 1},
    "GeneralAction": {"GeneralAction": 1},
    "PetAction": {"PetAction": 1},
    "MainCommand": {"MainCommand": 1},
    "AozActionTransient": {"AozDescription": 1, "AozStats": 0},
}


def integer(value):
    if value < 0xCF:
        return bytes([value + 1])
    mask, payload = 0, bytearray()
    for i in range(3, -1, -1):
        b = (value >> (8 * i)) & 255
        if b:
            mask |= 1 << i
            payload.append(b)
    return bytes([0xEF + mask]) + payload


def encode(text):
    """Strict recursive parser; malformed control syntax never becomes speech."""
    pos = 0

    def string(nested=False):
        nonlocal pos
        out = bytearray()
        while pos < len(text):
            if nested and text.startswith("))", pos):
                # A literal closing parenthesis may precede the two delimiters.
                if text.startswith(")))", pos):
                    out.extend(b")")
                    pos += 1
                    continue
                pos += 2
                return bytes(out)
            if text.startswith("<var ", pos):
                pos += 5
                match = re.match(r"[0-9A-Fa-f]{2}", text[pos:])
                if not match:
                    raise ValueError("invalid macro type")
                code = int(match[0], 16)
                pos += 2
                payload = bytearray()
                while True:
                    while pos < len(text) and text[pos].isspace():
                        pos += 1
                    if text.startswith("/var>", pos):
                        pos += 5
                        break
                    if text.startswith("((", pos):
                        pos += 2
                        child = string(True)
                        payload.extend(b"\xff" + integer(len(child)) + child)
                    else:
                        match = re.match(r"[0-9A-Fa-f]+", text[pos:])
                        if not match or len(match[0]) % 2:
                            raise ValueError(f"invalid macro expression near {pos}")
                        payload.extend(bytes.fromhex(match[0]))
                        pos += len(match[0])
                out.extend(bytes([2, code]) + integer(len(payload)) + payload + b"\x03")
            elif text.startswith("<br>", pos) or text[pos] == "\n":
                pos += 4 if text.startswith("<br>", pos) else 1
                out.extend(b"\x02\x10\x01\x03")
            elif text.startswith("<nl>", pos):
                pos += 4
                out.extend(b"\n")
            elif text.startswith(("<var", "/var>", "<tab>", "<color2", "<glow2"), pos):
                raise ValueError(f"unsupported or malformed tag near {pos}")
            else:
                out.extend(text[pos].encode("utf-8"))
                pos += 1
        if nested:
            raise ValueError("unterminated string expression")
        return bytes(out)

    return string()


def read_rows(path):
    return {int(r.attrib["id"]): r.find("target")
            for r in ET.parse(path).findall(".//trans-unit")}


def generate(cache, output, report_path, revision=REVISION, download=False):
    catalog, report = {}, {"revision": revision, "files": {}, "sheets": {}}
    overrides_path = Path(__file__).with_name("overrides.json")
    overrides = json.loads(overrides_path.read_text(encoding="utf-8"))
    for sheet, fields in FIELDS.items():
        paths = {}
        for lang in ("en", "ru"):
            path = cache / (sheet + (".en" if lang == "en" else "") + ".xlf")
            if download:
                url = f"https://raw.githubusercontent.com/xivrus/xiv_ru_weblate/{revision}/exd/{sheet}/{lang}.xlf"
                path.parent.mkdir(parents=True, exist_ok=True)
                with urllib.request.urlopen(url) as response:
                    path.write_bytes(response.read())
            report["files"][path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
            paths[lang] = read_rows(path)
        for key, column in fields.items():
            entries, skipped, partial = {}, [], []
            for row_id, en_node in paths["en"].items():
                source = (en_node.text or "").split("<tab>")[column]
                if not source.strip():
                    continue
                ru_node = paths["ru"].get(row_id)
                ru_fields = [] if ru_node is None else (ru_node.text or "").split("<tab>")
                target = ru_fields[column] if column < len(ru_fields) else ""
                override = overrides.get(key, {}).get(str(row_id))
                if override:
                    if source != override["source"]:
                        raise ValueError(f"stale override: {key}/{row_id}")
                    target = override["translation"]
                if not re.search("[А-Яа-яЁё]", target):
                    skipped.append({"id": row_id, "reason": "no Russian description"})
                    continue
                if not override and ru_node.get("state") not in ("translated", "final"):
                    # A half-translated tooltip must not be counted as complete.
                    partial.append(row_id)
                try:
                    english, russian = encode(source), encode(target)
                except ValueError as error:
                    skipped.append({"id": row_id, "reason": str(error)})
                    continue
                entries[str(row_id)] = [base64.b64encode(english).decode("ascii"),
                                        base64.b64encode(russian).decode("ascii")]
            catalog[key] = entries
            report["sheets"][key] = {"included": len(entries), "unfinished_source": partial, "skipped": skipped}
    local_path = Path(__file__).with_name("local-game-overrides.json")
    if local_path.exists():
        local = json.loads(local_path.read_text(encoding="utf-8"))
        report["local_game_version"] = local["game_version"]
        for entry in local["entries"]:
            key = entry["sheet"]
            source = entry["source_base64"]
            base64.b64decode(source, validate=True)
            translated = base64.b64encode(encode(entry["translation"])).decode("ascii")
            for row_id in entry["ids"]:
                catalog[key][str(row_id)] = [source, translated]
            replaced = set(entry["ids"])
            stats = report["sheets"][key]
            stats["skipped"] = [x for x in stats["skipped"] if x["id"] not in replaced]
            stats["unfinished_source"] = [x for x in stats["unfinished_source"] if x not in replaced]
            stats["local_overrides"] = stats.get("local_overrides", 0) + len(replaced)
            stats["included"] = len(catalog[key])
    output.parent.mkdir(parents=True, exist_ok=True)
    raw = json.dumps(catalog, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    output.write_bytes(gzip.compress(raw, mtime=0))
    report["catalog_sha256"] = hashlib.sha256(output.read_bytes()).hexdigest()
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for sheet, result in report["sheets"].items():
        print(f"{sheet}: {result['included']} included, {len(result['unfinished_source'])} unfinished, {len(result['skipped'])} skipped")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--cache", type=Path, default=Path(".local/russian-descriptions"))
    p.add_argument("--output", type=Path, default=Path("FF14Accessibility/Resources/RussianDescriptions.json.gz"))
    p.add_argument("--report", type=Path, default=Path("docs/maintenance/russian-descriptions-coverage.json"))
    p.add_argument("--revision", default=REVISION)
    p.add_argument("--download", action="store_true")
    args = p.parse_args()
    generate(args.cache, args.output, args.report, args.revision, args.download)
