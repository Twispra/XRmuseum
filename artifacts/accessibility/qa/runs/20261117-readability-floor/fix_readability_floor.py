#!/usr/bin/env python3
"""可读性下限修正器（Readability floor fixer）。

规则
----
对每个 TMP 文本组件，令其自动缩放的下限不再低于设计尺寸的固定比例：

    m_fontSizeMin := max(原值, RATIO × m_fontSizeMax)        RATIO = 0.35

理由：`m_fontSizeMin` 决定自动缩放能把字压到多小。设成 0.02 等于"允许压到几乎没有"，
实测最坏压缩比 72.5×（0.02 → 1.45）。按 35% 封顶，最坏压缩比收敛到 1/0.35 ≈ 2.86×。

为什么用相对比例而不是绝对字号
------------------------------
场景里的 TMP 处在不同缩放空间（同一个 0.02 在不同父级 scale 下对应的物理尺寸完全不同），
用绝对门槛需要先把单位换算统一；而"相对自身设计尺寸"与单位无关，可直接比较、可数值复核。

范围
----
只处理显式列出的场景文件。**主场景 `MuseumStart.unity` 不在默认范围内**——
它属共享文件，改动需按 `AGENTS.md` 先协调单一写入者。

用法
----
    python fix_readability_floor.py                 # 干跑，只打印将要发生的改动
    python fix_readability_floor.py --apply         # 真正写入
    python fix_readability_floor.py --ratio 0.5     # 换一个比例（默认 0.35）

退出码：0 = 正常；1 = 输入缺失或格式异常。
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

DEFAULT_REPO = Path(__file__).resolve().parents[5]
RATIO = 0.35

TARGETS = [
    "MuseumVR/Assets/Museum/Features/HelmetInspection/Scenes/HelmetInspectionDemo.unity",
    "MuseumVR/Assets/Museum/Features/PhysicalHands/Scenes/PhysicalHandsLab.unity",
]

DOC_SPLIT = re.compile(r"(?m)^--- !u!")
MIN_RE = re.compile(r"(?m)^(  m_fontSizeMin: )(-?[\d.]+)[ \t]*$")
MAX_RE = re.compile(r"(?m)^  m_fontSizeMax: (-?[\d.]+)[ \t]*$")
NAME_RE = re.compile(r"(?m)^  m_Name:[ \t]*(.*)$")
FID_RE = re.compile(r"&(-?\d+)")
GO_RE = re.compile(r"(?m)^  m_GameObject:\s*\{fileID:\s*(-?\d+)")


def fmt(v: float) -> str:
    """按 Unity YAML 的习惯输出浮点：紧凑、不带多余零、固定用点号。"""
    s = f"{v:.6f}".rstrip("0").rstrip(".")
    return s if s else "0"


def process(path: Path, ratio: float, apply: bool):
    text = path.read_text(encoding="utf-8")
    parts = DOC_SPLIT.split(text)
    changes = []

    # GameObject 名映射（TMP 组件本身没有 m_Name，要经 m_GameObject 反查）
    names = {}
    for body in parts[1:]:
        head = body.split("\n", 1)[0]
        m = None
        for cand in FID_RE.finditer(head):
            m = cand
        if not m:
            continue
        nm = NAME_RE.search(body.split("\n", 1)[1] if "\n" in body else "")
        if nm and body.split("\n", 1)[1].lstrip().startswith("GameObject:"):
            names[m.group(1)] = nm.group(1).strip()

    def transform(body: str) -> str:
        m_min = MIN_RE.search(body)
        m_max = MAX_RE.search(body)
        if not m_min or not m_max:
            return body
        old = float(m_min.group(2))
        top = float(m_max.group(1))
        if top <= 0:
            return body
        floor = ratio * top
        if floor <= old + 1e-9:
            return body
        new = fmt(floor)
        body2 = MIN_RE.sub(lambda mm: mm.group(1) + new, body, count=1)
        go = GO_RE.search(body)
        label = names.get(go.group(1)) if go else None
        changes.append(
            {
                "file": str(path).replace("\\", "/").split("MuseumVR/")[-1],
                "object": label,
                "fontSizeMax": top,
                "fontSizeMin_before": old,
                "fontSizeMin_after": float(new),
                "shrinkRatio_before": round(top / old, 2) if old > 0 else None,
                "shrinkRatio_after": round(top / float(new), 2),
            }
        )
        return body2

    new_parts = [parts[0]] + [transform(p) for p in parts[1:]]
    new_text = parts[0] + "".join("--- !u!" + p for p in new_parts[1:])
    if apply and changes:
        path.write_text(new_text, encoding="utf-8", newline="\n")
    return changes


def main() -> int:
    ap = argparse.ArgumentParser(description="修正 TMP 自动缩放下限")
    ap.add_argument("--repo", default=str(DEFAULT_REPO))
    ap.add_argument("--ratio", type=float, default=RATIO)
    ap.add_argument("--apply", action="store_true", help="真正写入（默认只干跑）")
    args = ap.parse_args()

    repo = Path(args.repo)
    all_changes = []
    for rel in TARGETS:
        p = repo / rel
        if not p.exists():
            print(f"[缺失] {rel}", file=sys.stderr)
            return 1
        ch = process(p, args.ratio, args.apply)
        print(f"\n=== {rel}  改动 {len(ch)} 处 ===")
        for c in ch:
            print(
                f"  {str(c['object'])[:34]:<36} min {c['fontSizeMin_before']:>6} -> "
                f"{c['fontSizeMin_after']:<8} max {c['fontSizeMax']:<8} "
                f"压缩比 {c['shrinkRatio_before']} -> {c['shrinkRatio_after']}"
            )
        all_changes.extend(ch)

    out = Path(__file__).resolve().parent / ("changes-applied.json" if args.apply else "changes-dryrun.json")
    out.write_text(
        json.dumps(
            {"ratio": args.ratio, "applied": args.apply, "total": len(all_changes), "changes": all_changes},
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
        newline="\n",
    )
    mode = "已写入" if args.apply else "干跑（未写入）"
    print(f"\n[{mode}] 共 {len(all_changes)} 处；明细 -> {out.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
