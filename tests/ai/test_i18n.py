"""Every UI text in the C# code and the data files has its translations (scripts/i18n.py, Assets/i18n/*.json)."""
from pathlib import Path
import re
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
import i18n  # noqa: E402

LANGUAGES = ("ja",)


class TranslationTests(unittest.TestCase):
    def test_reader_finds_plain_joined_and_interpolated_text(self):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder) / "Sample.cs"
            source.write_text('L("Một", "One"); pet.L("Hai " + "dòng", "Two " + "lines");\n'
                              'LF($"Đợt {w}", $"Wave {w} · {(n == 1 ? "" : "s")} {t:00}"); L(x.Vi, x.En);\n', encoding="utf-8")
            keys, dynamic = i18n.extract([source])
        self.assertEqual(list(keys), ["One", "Two lines", "Wave {0} · {1} {2:00}"])
        self.assertEqual(len(dynamic), 1)

    def test_every_code_text_is_translated(self):
        keys, _ = i18n.code_keys()
        self.assertGreater(len(keys), 500)
        for language in LANGUAGES:
            table = i18n.table(language)
            missing = [k for k in keys if k not in table]
            self.assertEqual(missing, [], f"{language}: run scripts/i18n.py {language}")

    def test_every_data_text_is_translated(self):
        keys = i18n.data_keys()
        self.assertIn("Slime King", keys)
        self.assertEqual(keys.get("Calico"), "Tam thể")
        for language in LANGUAGES:
            table = i18n.table(language)
            missing = [k for k in keys if k not in table]
            self.assertEqual(missing, [], f"{language}: run scripts/i18n.py {language}")

    def test_data_translations_keep_their_numbers_and_fit(self):
        # A skill's {key} is filled with a battle.json number; data text has length limits.
        named = re.compile(r"\{[A-Za-z]+\}")
        keys = i18n.data_keys()
        for language in LANGUAGES:
            table = i18n.table(language)
            for key in keys:
                text = table.get(key, key)
                self.assertEqual(sorted(named.findall(text)), sorted(named.findall(key)), (language, key, text))
                self.assertLessEqual(len(text), max(len(key), 8), (language, key, text))

    def test_translations_use_only_their_keys_holes(self):
        for language in LANGUAGES:
            for key, text in i18n.table(language).items():
                self.assertLessEqual(i18n.holes(text), i18n.holes(key), (language, key, text))


if __name__ == "__main__":
    unittest.main()
