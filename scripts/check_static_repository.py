#!/usr/bin/env python3
"""Fast, read-only checks that do not require Unity or Git LFS downloads."""

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[1]
TRANSLATIONS = Path("MuseumVR/Assets/Museum/Features/Localization/Tables/MuseumTranslations.json")
ASSETS = Path("MuseumVR/Assets/Museum")
LINK_RE = re.compile(r"!?\[[^\]]+\]\((<[^>]+>|[^\s)]+)(?:\s+[^)]*)?\)")
REFERENCE_RE = re.compile(r"^\s*\[[^\]]+\]:\s*(<[^>]+>|\S+)", re.MULTILINE)


def tracked_files(root):
    result = subprocess.run(
        ["git", "ls-files", "-z"], cwd=root, check=True, capture_output=True
    )
    return {Path(p.decode("utf-8")) for p in result.stdout.split(b"\0") if p}


def check_translations(root):
    errors = []
    path = root / TRANSLATIONS
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        return [f"{TRANSLATIONS}: cannot read JSON: {exc}"]
    entries = data.get("entries") if isinstance(data, dict) else None
    if not isinstance(entries, list) or not entries:
        return [f"{TRANSLATIONS}: entries must be a non-empty list"]

    seen_keys = set()
    english_translations = {}
    for index, entry in enumerate(entries, 1):
        label = f"{TRANSLATIONS}: entry {index}"
        if not isinstance(entry, dict):
            errors.append(f"{label} must be an object")
            continue
        for field in ("key", "english", "chinese", "traditionalChinese", "note"):
            if not isinstance(entry.get(field), str) or not entry[field].strip():
                errors.append(f"{label} has an empty or non-string {field}")
        key = entry.get("key")
        if isinstance(key, str):
            if key in seen_keys:
                errors.append(f"{label} duplicates key {key!r}")
            seen_keys.add(key)
        english = entry.get("english")
        if all(isinstance(entry.get(f), str) and entry[f].strip()
               for f in ("english", "chinese", "traditionalChinese")):
            pair = (entry["chinese"], entry["traditionalChinese"])
            previous = english_translations.setdefault(english, pair)
            if previous != pair:
                errors.append(f"{label} translates English source {english!r} inconsistently")
    return errors


def check_meta_pairs(files):
    errors = []
    assets = {p for p in files if p == ASSETS or ASSETS in p.parents}
    targets = {p for p in assets if p.suffix != ".meta"}
    directories = {ASSETS}
    for target in targets:
        directories.update(parent for parent in target.parents if parent == ASSETS or ASSETS in parent.parents)
        # .gitkeep is a Git placeholder, not a Unity asset, though some
        # existing placeholders do have a .meta file.
        if target.name != ".gitkeep":
            meta = Path(str(target) + ".meta")
            if meta not in assets:
                errors.append(f"{target}: missing tracked {meta.name}")
    for directory in directories:
        meta = Path(str(directory) + ".meta")
        if meta not in files:
            errors.append(f"{directory}: missing tracked directory .meta")
    for meta in (p for p in assets if p.suffix == ".meta"):
        target = Path(str(meta)[:-5])
        if target not in targets and target not in directories:
            errors.append(f"{meta}: orphan .meta (target is not tracked)")
    return errors


def local_links(text):
    outside_fences = []
    fenced = False
    for line in text.splitlines():
        if re.match(r"^\s*(```|~~~)", line):
            fenced = not fenced
        elif not fenced:
            outside_fences.append(line)
    content = "\n".join(outside_fences)
    return [match.group(1) for pattern in (LINK_RE, REFERENCE_RE)
            for match in pattern.finditer(content)]


def check_document_links(root, files):
    root = root.resolve()
    errors = []
    docs = sorted(p for p in files if p.suffix.lower() == ".md" and
                  (p == Path("README.md") or p.parts[0] in ("docs", "artifacts")))
    for doc in docs:
        try:
            text = (root / doc).read_text(encoding="utf-8")
        except (OSError, UnicodeError) as exc:
            errors.append(f"{doc}: cannot read: {exc}")
            continue
        for raw in local_links(text):
            destination = raw[1:-1] if raw.startswith("<") and raw.endswith(">") else raw
            if destination.startswith("#") or destination.startswith("/"):
                continue
            parsed = urlsplit(destination)
            if parsed.scheme or parsed.netloc:
                continue
            target = (root / doc.parent / unquote(parsed.path)).resolve()
            if not target.is_relative_to(root) or not target.exists():
                errors.append(f"{doc}: broken local link {destination}")
    return errors, len(docs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT, help="repository root")
    args = parser.parse_args()
    root = args.root.resolve()
    try:
        files = tracked_files(root)
    except (OSError, subprocess.CalledProcessError) as exc:
        print(f"Cannot list tracked files: {exc}", file=sys.stderr)
        return 2
    errors = check_translations(root)
    errors.extend(check_meta_pairs(files))
    link_errors, document_count = check_document_links(root, files)
    errors.extend(link_errors)
    for error in sorted(errors):
        print(f"ERROR: {error}", file=sys.stderr)
    print(f"Checked translations, Unity .meta pairs, and links in {document_count} documents: "
          f"{len(errors)} error(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
