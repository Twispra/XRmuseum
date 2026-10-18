"""Read-only old cmap/metrics/outlines audit; requires fontTools 4.60.1.

Run from any folder: python audit-source-font.py <optional-fontTools-directory>
The output is written beside this script, outside Unity Assets.
"""
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys

if len(sys.argv) > 1:
    sys.path.insert(0, sys.argv[1])
import fontTools
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen

folder = Path(__file__).resolve().parent
root = folder.parents[4]
asset = "MuseumVR/Assets/Museum/Features/Localization/Fonts/NotoSansSC-MuseumSubset.ttf"
before_ref = "83d64397d99f6d3b7dc99d85fe62bd23d5fff979"
old_bytes = subprocess.check_output(["git", "-C", str(root), "show", before_ref + ":" + asset])
new_bytes = (root / asset).read_bytes()
old, new = TTFont(io.BytesIO(old_bytes)), TTFont(io.BytesIO(new_bytes))
a, b = old.getBestCmap(), new.getBestCmap()
removed = sorted(set(a) - set(b))
assert not removed, "Old source codepoints were removed."
metrics = [c for c in a if old["hmtx"][a[c]] != new["hmtx"][b[c]]]
glyphsets = old.getGlyphSet(), new.getGlyphSet()
outlines = []
for codepoint in a:
    pens = [DecomposingRecordingPen(glyphset) for glyphset in glyphsets]
    glyphsets[0][a[codepoint]].draw(pens[0])
    glyphsets[1][b[codepoint]].draw(pens[1])
    if pens[0].value != pens[1].value:
        outlines.append(codepoint)
report = {
    "beforeCommit": before_ref,
    "beforeCodepoints": len(a), "afterCodepoints": len(b),
    "removedCodepoints": removed,
    "addedCodepoints": [f"U+{c:04X} {chr(c)}" for c in sorted(set(b) - set(a))],
    "changedOldHorizontalMetrics": metrics,
    "changedOldDecomposedOutlines": outlines,
    "beforeFamily": old["name"].getDebugName(1), "afterFamily": new["name"].getDebugName(1),
    "beforeStyle": old["name"].getDebugName(2), "afterStyle": new["name"].getDebugName(2),
    "beforeUnitsPerEm": old["head"].unitsPerEm, "afterUnitsPerEm": new["head"].unitsPerEm,
    "beforeSha256": hashlib.sha256(old_bytes).hexdigest(),
    "afterSha256": hashlib.sha256(new_bytes).hexdigest(),
    "fontTools": fontTools.__version__,
    "limits": "Source TTF audit only; saved SDF pixels, GUI layout and XR behavior require separate evidence.",
}
assert not metrics and not outlines, "Existing glyph metrics/outlines changed."
(folder / "source-font-preservation-audit.json").write_text(
    json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"old={len(a)} new={len(b)} metrics={len(metrics)} outlines={len(outlines)}")
