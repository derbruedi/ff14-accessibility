"""Create a resumable, local-only English-to-Russian machine translation draft.

Requires CTranslate2, sentencepiece and a model converted to CTranslate2.
The optional NLLB backend also needs transformers and its saved local tokenizer.
This tool does not download anything or contact any service.
The result is a draft, not a claim of reviewed Russian game terminology.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import time
import unicodedata


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_MODEL = ROOT / ".local/translation-model"


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def save_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def normalize(text: str) -> str:
    # Original OPUS preprocessing replaces Unicode punctuation, removes control
    # characters and normalizes spaces before SentencePiece encoding.
    text = text.translate(str.maketrans({"\u2018": "'", "\u2019": "'", "\u201c": '"',
                                        "\u201d": '"', "\u2026": "...", "\u00a0": " "}))
    text = "".join(c for c in text if not unicodedata.category(c).startswith("C") or c.isspace())
    return " ".join(text.split())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="JSON array of English strings")
    parser.add_argument("output", type=Path, help="JSON mapping English strings to Russian drafts")
    parser.add_argument("--model-root", type=Path, default=DEFAULT_MODEL)
    parser.add_argument("--architecture", choices=("marian", "nllb"), default="marian")
    parser.add_argument("--batch-size", type=int, default=64)
    parser.add_argument("--threads", type=int, default=6)
    parser.add_argument("--beam-size", type=int, default=4)
    parser.add_argument("--max-output-tokens", type=int, default=192)
    parser.add_argument("--max-input-tokens", type=int, default=512)
    parser.add_argument("--save-every", type=int, default=4, help="Save after this many batches")
    parser.add_argument("--limit", type=int, default=0, help="Maximum new strings; 0 means all")
    parser.add_argument("--source-overrides", type=Path, help="Optional JSON mapping of source to clearer English input")
    args = parser.parse_args()
    for field in ("batch_size", "threads", "beam_size", "max_output_tokens", "max_input_tokens", "save_every"):
        if getattr(args, field) < 1:
            parser.error(f"--{field.replace('_', '-')} must be positive")
    if args.limit < 0:
        parser.error("--limit must be nonnegative")
    if args.input.resolve() == args.output.resolve():
        parser.error("Input and output must be different files")

    sources = load_json(args.input)
    if not isinstance(sources, list) or any(not isinstance(s, str) or not s.strip() for s in sources):
        parser.error("Input must be a JSON array of nonempty strings")
    sources = list(dict.fromkeys(sources))
    translated = load_json(args.output) if args.output.exists() else {}
    if not isinstance(translated, dict) or any(not isinstance(k, str) or not isinstance(v, str) or not v.strip()
                                              for k, v in translated.items()):
        parser.error("Existing output must be a JSON mapping of strings to nonempty strings")
    overrides = load_json(args.source_overrides) if args.source_overrides else {}
    if not isinstance(overrides, dict) or any(not isinstance(k, str) or not isinstance(v, str) or not v.strip()
                                             for k, v in overrides.items()):
        parser.error("Source overrides must be a JSON mapping of strings to nonempty strings")

    pending = [s for s in sources if s not in translated]
    if args.limit:
        pending = pending[:args.limit]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    log_path = args.output.with_name(args.output.name + ".log.jsonl")
    report_path = args.output.with_name(args.output.name + ".report.json")
    state_path = args.output.with_name(args.output.name + ".state.json")
    started = time.monotonic()
    completed = 0

    def log(event: str, **values) -> None:
        entry = {"time": datetime.now(timezone.utc).isoformat(), "event": event, **values}
        with log_path.open("a", encoding="utf-8") as log_file:
            log_file.write(json.dumps(entry, ensure_ascii=False) + "\n")
        print(json.dumps(entry, ensure_ascii=False), flush=True)

    if not pending:
        log("already_complete", input_strings=len(sources), output_strings=len(translated))
        return 0

    import ctranslate2
    import sentencepiece

    model_dir = args.model_root / "ct2"
    source_model = args.model_root / ("original/source.spm" if args.architecture == "marian"
                                     else "original/sentencepiece.bpe.model")
    target_model = args.model_root / ("original/target.spm" if args.architecture == "marian"
                                     else "original/sentencepiece.bpe.model")
    model_info_path = args.model_root / "model-info.json"
    model_info = load_json(model_info_path) if model_info_path.exists() else {}
    metadata = {
        "kind": "unreviewed_machine_translation_draft",
        "model": model_info.get("model", "Helsinki-NLP/opus-mt-en-ru" if args.architecture == "marian"
                                else "facebook/nllb-200-distilled-600M"),
        "original_revision": model_info.get("original_revision", "opus-2020-02-11" if args.architecture == "marian"
                                            else "unknown; identify by model hash"),
        "architecture": args.architecture,
        "model_bin_sha256": sha256(model_dir / "model.bin"),
        "source_spm_sha256": sha256(source_model),
        "target_spm_sha256": sha256(target_model),
        "input_sha256": sha256(args.input),
        "source_overrides_sha256": sha256(args.source_overrides) if args.source_overrides else None,
        "ctranslate2": ctranslate2.__version__,
        "sentencepiece": sentencepiece.__version__,
        "device": "cpu", "compute_type": "int8", "threads": args.threads,
        "beam_size": args.beam_size, "batch_size": args.batch_size,
        "max_input_tokens": args.max_input_tokens, "max_output_tokens": args.max_output_tokens,
    }
    if state_path.exists():
        previous_state = load_json(state_path)
        for key in ("model_bin_sha256", "source_spm_sha256", "target_spm_sha256", "architecture",
                    "source_overrides_sha256"):
            if previous_state.get(key) != metadata.get(key):
                parser.error(f"Resume would mix different model/settings ({key}); use a separate output file")
    save_json(state_path, metadata)
    if args.architecture == "marian":
        tokenizer = sentencepiece.SentencePieceProcessor(model_file=str(source_model))
        detokenizer = sentencepiece.SentencePieceProcessor(model_file=str(target_model))
        encode = lambda value: tokenizer.encode(value, out_type=str)
        decode = lambda value: detokenizer.decode(value)
    else:
        from transformers import AutoTokenizer
        tokenizer = AutoTokenizer.from_pretrained(str(args.model_root / "original"), src_lang="eng_Latn",
                                                  local_files_only=True)
        encode = lambda value: tokenizer.convert_ids_to_tokens(tokenizer.encode(value))
        decode = lambda value: tokenizer.decode(tokenizer.convert_tokens_to_ids(value[1:]),
                                                skip_special_tokens=True)
    translator = ctranslate2.Translator(str(model_dir), device="cpu", compute_type="int8",
                                       intra_threads=args.threads, inter_threads=1)
    log("start", pending=len(pending), already_translated=len(translated), **metadata)
    try:
        for index in range(0, len(pending), args.batch_size):
            batch = pending[index:index + args.batch_size]
            tokens = [encode(normalize(overrides.get(s, s))) for s in batch]
            for source, encoded in zip(batch, tokens):
                if not encoded or len(encoded) > args.max_input_tokens:
                    raise ValueError(f"Input is empty or exceeds token limit: {source!r} ({len(encoded)} tokens)")
            options = {"target_prefix": [["rus_Cyrl"] for _ in batch]} if args.architecture == "nllb" else {}
            results = translator.translate_batch(tokens, beam_size=args.beam_size,
                                                 max_batch_size=args.batch_size,
                                                 max_input_length=args.max_input_tokens,
                                                 max_decoding_length=args.max_output_tokens,
                                                 length_penalty=1.0, repetition_penalty=1.05, **options)
            for source, result in zip(batch, results):
                hypothesis = result.hypotheses[0]
                if len(hypothesis) >= args.max_output_tokens:
                    raise ValueError(f"Output reached token limit; refusing possible truncation: {source!r}")
                russian = decode(hypothesis).strip()
                if not russian:
                    raise ValueError(f"Model returned an empty translation: {source!r}")
                translated[source] = russian
                completed += 1
            elapsed = time.monotonic() - started
            log("batch", completed=completed, pending_this_run=len(pending) - completed,
                seconds=round(elapsed, 2), strings_per_second=round(completed / elapsed, 2))
            if (index // args.batch_size + 1) % args.save_every == 0:
                save_json(args.output, translated)
    except BaseException as error:
        save_json(args.output, translated)
        log("interrupted", error=str(error), saved_strings=len(translated))
        raise
    save_json(args.output, translated)
    covered = sum(s in translated for s in sources)
    report = {**metadata, "input_unique_strings": len(sources), "covered_strings": covered,
              "missing_strings": len(sources) - covered, "new_strings": completed,
              "elapsed_seconds": round(time.monotonic() - started, 3),
              "without_cyrillic": [s for s in sources if s in translated and not re.search("[А-Яа-яЁё]", translated[s])],
              "unchanged": [s for s in sources if translated.get(s) == s],
              "output_sha256": sha256(args.output)}
    save_json(report_path, report)
    log("complete", new_strings=completed, covered_strings=covered, missing_strings=len(sources) - covered,
        elapsed_seconds=report["elapsed_seconds"], without_cyrillic=len(report["without_cyrillic"]),
        report=str(report_path))
    return 0


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
