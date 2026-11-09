from copy import deepcopy
from pathlib import Path
from zipfile import ZipFile
from lxml import etree
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[3]
WORK = Path(__file__).resolve().parent
REFERENCE = ROOT / "phase2小组和个人作业/COMP5424-Group-Project-Phase2-ActivityRecord-template.docx"
OUTPUT = ROOT / "phase2小组和个人作业/COMP5424-Group2-Phase2-ActivityRecord.docx"
W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PKG = "http://schemas.openxmlformats.org/package/2006/relationships"
NS = {"w": W, "r": R}
def q(name): return "{" + W + "}" + name

MEMBERS = [
    ["赵基蘅\nTwispra", "• Foundation, integration and XR repairs.", "22 Sep - 18 Oct: #1, #2, #24, #49, #52, #58, #67; follow-up repairs."],
    ["叶福涛\nFttt1003", "• Gallery, Mark V, demos and trilingual single-hand controls.", "29 Sep - 17 Oct: #3, #5, #18, #35, #59; fixed-centre mode."],
    ["张非凡\nnikoyeah830-wq", "• Eras/localisation, audits and operator panel.", "3 - 18 Oct: #10, #13, #27, #40, #65. Panel repaired by Twispra."],
    ["何梓绎\nAragorn-cyber /\nheziyi-aragon", "• Hotspots, visibility and accessible states.", "6 - 16 Oct: d84c93f; #36, #46, #55; followed by Twispra repairs."],
    ["赵祥睿\nEreshkigal1224", "• SuperLite model and initial Unity integration.", "#12, #37, #43 (11 Oct), #44 (12 Oct); seven Twispra follow-ups."],
    ["蔡子申\nr2kh8sck4y-bot", "• Recovery/Help, early model and photo study.", "13 - 18 Oct: #51, #53, #66, #69; #67 merged after Twispra repairs."],
]
DECISIONS = [
    ["Three eras, shared exhibit", "Retain common controls", "Central #35/#44/#69; #67 adds a separate left study; full #64 layout open."],
    ["Fixed-centre rotation", "Predictable inspection", "#59 replaces default free/two-hand grabbing (#52)."],
    ["Historical caveats", "Unmeasured details", "1839 source 1881-47; virtual windows/routes are teaching aids."],
]
ISSUES = [
    ["PQ604 / complete VR walkthrough", "18 Oct: 6088c73 build/ZIP verified; complete the headset journey on PQ604.", "Proposed: 赵基蘅; tester to confirm."],
    ["Usability, language and learning", "Phase 3: test seated/single-hand use, text and learning with headset users (#50).", "Proposed: 叶福涛 / 赵基蘅; team evaluation."],
    ["Full display layout / historical limits", "#67 merged; review the wider #64 layout and uncertain historical details.", "Proposed: 蔡子申; group review."],
]
COMMIT_URLS = {
    "6088c73": "6088c736e89d05101e09645c1796556012a6fd5d",
    "d84c93f": "d84c93f352541949db246440adb857361ec80ecd",
}
ISSUE_NUMBERS = {50, 64}
BASE = "https://github.com/Twispra/XRmuseum"

inventory = json.loads((WORK / "template-package-inventory.json").read_text(encoding="utf-8"))
assert hashlib.sha256(REFERENCE.read_bytes()).hexdigest() == inventory["sha256"]
with ZipFile(REFERENCE) as source:
    entries = source.infolist()
    parts = {entry.filename: source.read(entry.filename) for entry in entries}
document = etree.fromstring(parts["word/document.xml"])
rels = etree.fromstring(parts["word/_rels/document.xml.rels"])
body = document.find("w:body", NS)
original_section = etree.tostring(body.find("w:sectPr", NS))
original_relationships = {e.get("Id"): etree.tostring(e) for e in rels}
href_ids = {}

def relationship(href):
    if href in href_ids: return href_ids[href]
    ident = "activityEvidence" + str(len(href_ids) + 1)
    assert ident not in original_relationships
    el = etree.SubElement(rels, "{" + PKG + "}Relationship")
    el.set("Id", ident)
    el.set("Type", R + "/hyperlink")
    el.set("Target", href)
    el.set("TargetMode", "External")
    href_ids[href] = ident
    return ident

def make_run(text, base_properties=None, link=False):
    run = etree.Element(q("r"))
    prop = deepcopy(base_properties) if base_properties is not None else etree.Element(q("rPr"))
    fonts = prop.find("w:rFonts", NS)
    if fonts is None: fonts = etree.SubElement(prop, q("rFonts"))
    for attr in ("ascii", "hAnsi", "cs"): fonts.set(q(attr), "Times New Roman")
    fonts.set(q("eastAsia"), "Microsoft YaHei")
    if link:
        under = etree.SubElement(prop, q("u")); under.set(q("val"), "single")
        color = etree.SubElement(prop, q("color")); color.set(q("val"), "000000")
    run.append(prop)
    for index, line in enumerate(text.split("\n")):
        if index: etree.SubElement(run, q("br"))
        t = etree.SubElement(run, q("t"))
        t.set("{http://www.w3.org/XML/1998/namespace}space", "preserve")
        t.text = line
    return run

def fill_paragraph(paragraph, text):
    first = paragraph.find("w:r/w:rPr", NS)
    base_prop = deepcopy(first) if first is not None else None
    for child in list(paragraph):
        if child.tag not in {q("pPr"), q("commentRangeStart"), q("commentRangeEnd")}:
            # Template comment reference runs occur outside editable table data.
            if child.find("w:commentReference", NS) is None: paragraph.remove(child)
    pat = re.compile(r"#\d+|\b(?:6088c73|d84c93f)\b")
    cursor = 0
    for match in pat.finditer(text):
        if match.start() > cursor: paragraph.append(make_run(text[cursor:match.start()], base_prop))
        token = match.group()
        if token.startswith("#"):
            number = int(token[1:]); kind = "issues" if number in ISSUE_NUMBERS else "pull"
            href = BASE + "/" + kind + "/" + str(number)
        else: href = BASE + "/commit/" + COMMIT_URLS[token]
        link = etree.SubElement(paragraph, q("hyperlink")); link.set("{" + R + "}id", relationship(href))
        link.append(make_run(token, base_prop, link=True))
        cursor = match.end()
    if cursor < len(text) or not text: paragraph.append(make_run(text[cursor:], base_prop))

def fill_table(table, rows):
    source_rows = table.findall("w:tr", NS)
    header = source_rows[0]
    prototype = deepcopy(source_rows[1])
    assert not prototype.findall(".//w:commentRangeStart", NS)
    for row in source_rows[1:]: table.remove(row)
    for values in rows:
        row = deepcopy(prototype)
        # New identity values must not duplicate template paragraph identifiers.
        for node in row.iter():
            for attr in list(node.attrib):
                if etree.QName(attr).localname in {"paraId", "textId"}: del node.attrib[attr]
        for cell, value in zip(row.findall("w:tc", NS), values):
            paragraphs = cell.findall("w:p", NS)
            for extra in paragraphs[1:]: cell.remove(extra)
            fill_paragraph(paragraphs[0], value)
        table.append(row)

paragraphs = body.findall("w:p", NS)
fill_paragraph(paragraphs[1], "Team: 2   |   Period: 22 Sep - 18 Oct 2026   |   Prototype: 6088c73")
fill_paragraph(paragraphs[2], "Prepared by: 赵基蘅 (Twispra), with Codex assistance")
for table, rows in zip(body.findall("w:tbl", NS), (MEMBERS, DECISIONS, ISSUES)):
    fill_table(table, rows)

# Word requires a terminal paragraph after the last table. Keep this empty
# paragraph, but avoid exporting an otherwise empty second page for its spacing.
terminal = paragraphs[-1]
assert not terminal.xpath('.//w:t/text()', namespaces=NS)
pp = terminal.find('w:pPr', NS)
if pp is None: pp = etree.SubElement(terminal, q('pPr'))
spacing = pp.find('w:spacing', NS)
if spacing is None: spacing = etree.SubElement(pp, q('spacing'))
for key, value in {'before':'0', 'after':'0', 'line':'20', 'lineRule':'exact'}.items():
    spacing.set(q(key), value)
rp = pp.find('w:rPr', NS)
if rp is None: rp = etree.SubElement(pp, q('rPr'))
sz = rp.find('w:sz', NS)
if sz is None: sz = etree.SubElement(rp, q('sz'))
sz.set(q('val'), '2')

assert etree.tostring(body.find("w:sectPr", NS)) == original_section
for ident, xml in original_relationships.items():
    assert etree.tostring(next(e for e in rels if e.get("Id") == ident)) == xml
parts["word/document.xml"] = etree.tostring(document, xml_declaration=True, encoding="UTF-8", standalone=True)
parts["word/_rels/document.xml.rels"] = etree.tostring(rels, xml_declaration=True, encoding="UTF-8", standalone=True)
with ZipFile(OUTPUT, "w") as target:
    for entry in entries: target.writestr(entry, parts[entry.filename])
assert hashlib.sha256(REFERENCE.read_bytes()).hexdigest() == inventory["sha256"]
preserved = []
with ZipFile(OUTPUT) as final:
    for name, info in inventory["parts"].items():
        if name in {"word/document.xml", "word/_rels/document.xml.rels"}: continue
        assert hashlib.sha256(final.read(name)).hexdigest() == info["sha256"], name
        preserved.append(name)
visible = " ".join(document.xpath("//w:body//w:t/text()", namespaces=NS))
assert not re.search(r"\[Team number\]|\[Name\]|\[Issue|\[Next|…", visible)
report = {
    "reference": str(REFERENCE), "reference_sha256": inventory["sha256"],
    "output": str(OUTPUT), "output_sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
    "visible_words": len(visible.split()), "evidence_links": sorted(href_ids),
    "preserved_parts": preserved, "row_counts": [7, 4, 4],
    "body_text": visible,
}
(WORK / "authoring-audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({k: v for k, v in report.items() if k not in {"body_text", "evidence_links", "preserved_parts"}}, ensure_ascii=False, indent=2))
