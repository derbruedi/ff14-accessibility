import gzip
import json
from pathlib import Path
import tempfile
import unittest

import generate
import generate_names


class TranslationTests(unittest.TestCase):
    def test_direction_strength_and_clause_order_are_preserved(self):
        vocabulary = json.loads((generate.HERE / "shape-vocabulary.json").read_text(encoding="utf-8"))
        self.assertEqual("заметно шире, немного короче, смещены к центру",
                         generate.translate_shape("markedly wider, slightly shorter, shifted inward", vocabulary))
        self.assertEqual("глубже посажены, дальше назад",
                         generate.translate_shape("deeper set, further back", vocabulary))

    def test_unknown_phrase_is_not_silently_removed(self):
        with self.assertRaisesRegex(ValueError, "Missing shape phrase"):
            generate.translate_shape("wider, new upstream detail", {"wider": "шире"})

    def test_catalog_is_reproducible_and_reports_actual_gaps(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            generate.generate(root / "a.gz", root / "a.json")
            generate.generate(root / "b.gz", root / "b.json")
            self.assertEqual((root / "a.gz").read_bytes(), (root / "b.gz").read_bytes())
            catalog = json.loads(gzip.decompress((root / "a.gz").read_bytes()))
            report = json.loads((root / "a.json").read_text(encoding="utf-8"))
            self.assertEqual(len(catalog), report["translated_phrases"])
            self.assertEqual([], report["tables"]["Shape"]["remaining"])
            self.assertEqual([], report["tables"]["Icon"]["remaining"])
            all_sources = set()
            for kind, stats in report["tables"].items():
                sources = generate.phrases(generate.ROOT / "FF14Accessibility/Services" / f"CharaMake{kind}Text.cs")
                all_sources.update(sources)
                self.assertEqual(sorted(sources - catalog.keys()), stats["remaining"])
                self.assertEqual(len(sources & catalog.keys()), stats["translated"])
            self.assertEqual(len(all_sources), report["unique_source_phrases"])

    def test_extractor_handles_quotes_and_short_full_pairs(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "text.cs"
            source.write_text('F("de", "a \\"quote\\"", 123); S("kurz", "short", "lang", "long", 456);'.replace('\\\\', '\\'), encoding="utf-8")
            self.assertEqual({'a "quote"', 'short', 'long'}, generate.phrases(source))

    def test_name_catalog_requires_every_saved_row_and_does_not_cross_tables(self):
        sources = {"CraftActionName": {"123": "Basic Synthesis"}, "ActionName": {"123": "Veneration"}}
        glossary = {"Basic Synthesis": "Базовый синтез", "Veneration": "Почитание"}
        result = generate_names.build(sources, glossary)
        self.assertNotEqual(result["CraftActionName"]["123"], result["ActionName"]["123"])
        del glossary["Veneration"]
        with self.assertRaisesRegex(ValueError, "Missing name: ActionName/123"):
            generate_names.build(sources, glossary)


if __name__ == "__main__":
    unittest.main()
