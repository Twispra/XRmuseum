"""Focused regression tests for the repository's no-Unity checks."""

import json
import tempfile
import unittest
from pathlib import Path

from scripts.check_static_repository import (
    ASSETS,
    TRANSLATIONS,
    check_document_links,
    check_meta_pairs,
    check_translations,
)


class StaticRepositoryTests(unittest.TestCase):
    def test_translation_rejects_duplicate_key(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            path = root / TRANSLATIONS
            path.parent.mkdir(parents=True)
            entry = {"key": "same", "english": "Hello", "chinese": "你好",
                     "traditionalChinese": "你好"}
            path.write_text(json.dumps({"entries": [entry, entry]}), encoding="utf-8")
            self.assertTrue(any("duplicates key" in error
                                for error in check_translations(root)))

    def test_translation_rejects_conflicting_same_english(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            path = root / TRANSLATIONS
            path.parent.mkdir(parents=True)
            entries = [
                {"key": "a", "english": "Open", "chinese": "打开",
                 "traditionalChinese": "打開", "note": "button"},
                {"key": "b", "english": "Open", "chinese": "开启",
                 "traditionalChinese": "開啟", "note": "button"},
            ]
            path.write_text(json.dumps({"entries": entries}), encoding="utf-8")
            self.assertTrue(any("inconsistently" in error
                                for error in check_translations(root)))

    def test_meta_pairs_ignore_gitkeep_but_reject_missing_asset_meta(self):
        folder = ASSETS / "Models"
        files = {Path(str(ASSETS) + ".meta"), folder,
                 Path(str(folder) + ".meta"), folder / ".gitkeep",
                 folder / "helmet.fbx"}
        errors = check_meta_pairs(files)
        self.assertEqual(1, len(errors))
        self.assertIn("helmet.fbx", errors[0])

    def test_document_link_rejects_missing_relative_target(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            doc = Path("docs/example.md")
            (root / doc).parent.mkdir()
            (root / doc).write_text("[missing](other.md)", encoding="utf-8")
            errors, count = check_document_links(root, {doc})
            self.assertEqual(1, count)
            self.assertEqual(1, len(errors))

    def test_document_link_accepts_existing_target_and_ignores_external(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            doc = Path("docs/example.md")
            target = root / "README.md"
            (root / doc).parent.mkdir()
            target.write_text("Present", encoding="utf-8")
            (root / doc).write_text(
                "[local](../README.md#start) [remote](https://example.org/page)",
                encoding="utf-8",
            )
            errors, count = check_document_links(root, {doc})
            self.assertEqual(1, count)
            self.assertEqual([], errors)


if __name__ == "__main__":
    unittest.main()
