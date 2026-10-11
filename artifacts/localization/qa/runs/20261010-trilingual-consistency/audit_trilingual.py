#!/usr/bin/env python3
"""三语文案一致性核对（可复现）

比对 MuseumTranslations.json（作者源）与 LocalizationTable.asset（Unity 保存表）的全部字段，
并核对文案用到的字符是否都已烘焙进 TMP SDF 字形资产。

纯标准库实现，不需要 Unity、不需要 fontTools。

用法（仓库根目录或任意位置）:
    python audit_trilingual.py
    python audit_trilingual.py --repo <仓库根目录>
    python audit_trilingual.py --json <输出路径>

退出码: 0 = 全部一致；1 = 发现不一致（详见 stderr）。
"""
import argparse
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", ".."))
REL = os.path.join("MuseumVR", "Assets", "Museum", "Features", "Localization")


# ---------------- Unity YAML 标量还原 ----------------
def fold_scalar(raw_lines, quote):
    """还原 YAML flow scalar 的折行：N 个换行 -> N-1 个换行；1 个换行 -> 空格。

    Unity 写 LocalizationTable.asset 时: 中文/繁体用双引号 + \\uXXXX 转义；
    英文可能用单引号、无引号或双引号，并在 80 列附近自动折行。
    因此必须先按 YAML 规则折行还原，再解码转义，否则会把折行误判成漏译。
    """
    text = "\n".join(raw_lines).strip()
    if quote:
        if len(text) >= 2 and text[0] == quote and text[-1] == quote:
            text = text[1:-1]
        elif text[:1] == quote:
            text = text[1:]
        elif text[-1:] == quote:
            text = text[:-1]
    if quote == "'":
        text = text.replace("''", "\x00")

    lines = text.split("\n")
    out = lines[0].strip()
    pending_breaks = 0
    for ln in lines[1:]:
        if ln.strip() == "":
            pending_breaks += 1
            continue
        breaks = 1 + pending_breaks
        out += (" " if breaks == 1 else "\n" * (breaks - 1)) + ln.strip()
        pending_breaks = 0

    if quote == '"':
        out = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), out)
        for a, b in (("\\n", "\n"), ("\\t", "\t"), ('\\"', '"'), ("\\\\", "\\")):
            out = out.replace(a, b)
    elif quote == "'":
        out = out.replace("\x00", "'")
    return out


def parse_asset(path):
    entries, cur, field, buf, quote = [], None, None, [], None

    def flush():
        if cur is None or field is None or field == "key":
            return
        cur[field] = fold_scalar(buf, quote)

    for ln in open(path, encoding="utf-8").read().splitlines():
        m = re.match(r"^  - key:\s*(.+)$", ln)
        if m:
            flush()
            cur = {"key": m.group(1).strip()}
            entries.append(cur)
            field, buf, quote = None, [], None
            continue
        if cur is None:
            continue
        m = re.match(r"^    (key|chinese|english|traditionalChinese|note):\s*(.*)$", ln)
        if m:
            flush()
            field, buf = m.group(1), [m.group(2)]
            v = m.group(2)
            quote = v[0] if v[:1] in ("'", '"') else None
            if quote and len(v) > 1 and v.rstrip().endswith(quote):
                flush()
                field, buf, quote = None, [], None
            continue
        # 折行的续行（缩进 >= 6）与空行
        if field and (ln.startswith("      ") or ln.strip() == ""):
            buf.append(ln)
    flush()
    return entries


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default=DEFAULT_REPO)
    ap.add_argument("--json", default=os.path.join(HERE, "report.generated.json"))
    args = ap.parse_args()

    base = os.path.join(args.repo, REL)
    json_path = os.path.join(base, "Tables", "MuseumTranslations.json")
    asset_path = os.path.join(base, "Tables", "LocalizationTable.asset")
    sdf_path = os.path.join(base, "Fonts", "NotoSansSC-MuseumSubset SDF.asset")
    req_path = os.path.join(base, "Fonts", "characters-required.txt")
    for p in (json_path, asset_path, sdf_path, req_path):
        if not os.path.isfile(p):
            sys.exit(f"输入不存在: {p}")

    src = json.load(open(json_path, encoding="utf-8"))["entries"]
    tbl = parse_asset(asset_path)
    table = {e["key"]: e for e in tbl}
    source = {e["key"]: e for e in src}

    problems = []
    only_source = sorted(set(source) - set(table))
    only_table = sorted(set(table) - set(source))
    if only_source:
        problems.append(f"只在 JSON: {only_source}")
    if only_table:
        problems.append(f"只在保存表: {only_table}")

    fields = ("chinese", "traditionalChinese", "english")
    compared = 0
    for k in sorted(set(source) & set(table)):
        for f in fields:
            compared += 1
            if (source[k].get(f) or "").strip() != (table[k].get(f) or "").strip():
                problems.append(f"不一致 [{k}].{f}")

    uni = {int(m.group(1)) for m in re.finditer(r"m_Unicode:\s*(\d+)",
                                                open(sdf_path, encoding="utf-8", errors="replace").read())}
    baked = {chr(u) for u in uni}
    used = {}
    for e in src:
        for f in fields:
            for c in (e.get(f) or ""):
                if c not in "\n\r\t ":
                    used.setdefault(c, 0)
                    used[c] += 1
    missing_used = sorted(c for c in used if c not in baked)

    required = {c for c in open(req_path, encoding="utf-8").read() if c not in "\n\r\t "}
    missing_required = sorted(c for c in required if c not in baked)

    result = {
        "jsonEntries": len(src),
        "assetEntries": len(tbl),
        "fieldsCompared": compared,
        "mismatches": len([p for p in problems if p.startswith("不一致")]) + len(only_source) + len(only_table),
        "bakedGlyphs": len(baked),
        "distinctCharsUsedInCopy": len(used),
        "missingGlyphsInCopy": len(missing_used),
        "listedChars": len(required),
        "missingFromList": len(missing_required),
        "problems": problems[:50],
        "missingUsedChars": [f"{c} U+{ord(c):04X}" for c in missing_used[:50]],
        "missingRequiredChars": [f"{c} U+{ord(c):04X}" for c in missing_required[:50]],
    }
    with open(args.json, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    print(json.dumps({k: v for k, v in result.items() if not k.startswith("missing")},
                     ensure_ascii=False, indent=2))
    if result["mismatches"] or result["missingGlyphsInCopy"] or result["missingFromList"]:
        print("发现不一致，详见", args.json, file=sys.stderr)
        return 1
    print("✅ 保存表与 JSON 一致；文案字符全部已烘焙；required 清单全部已烘焙")
    return 0


if __name__ == "__main__":
    sys.exit(main())
