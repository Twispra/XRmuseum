#!/usr/bin/env python3
"""针对 PR #41 修复的独立读回检查；输出可另指定，拒绝覆盖历史验证文件。"""
import argparse
import ast
import hashlib
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path


def main():
    here = Path(__file__).resolve().parent
    repo = Path(__file__).resolve().parents[5]
    ap = argparse.ArgumentParser()
    ap.add_argument("--report", default=str(here / "report.json"))
    ap.add_argument("--out", default=str(here / "verification.json"))
    args = ap.parse_args()
    result = {"checks": []}

    def check(name, condition):
        result["checks"].append({"check": name, "passed": bool(condition)})

    def git(*parts):
        return subprocess.run(["git", "-C", str(repo), *parts], capture_output=True, check=True).stdout

    report = json.loads(Path(args.report).read_text(encoding="utf-8"))
    historic_path = repo / "artifacts/accessibility/qa/runs/20261010-accessibility-audit/report.json"
    old_bytes = historic_path.read_bytes()
    old = json.loads(old_bytes)
    obs = {o["item"]: o["value"] for o in report["observations"]}
    old_obs = {o["item"]: o["value"] for o in old["observations"]}
    check("20 observations retained", len(report["observations"]) == 20 and set(obs) == set(old_obs))
    changed = {
        "热点说明面板背景（HotspotPanel.mat）", "语言选择板背景（Language-Board.mat）",
        "热点面板文字 / 面板背景 的对比度", "语言选择板文字 / 板面背景 的对比度",
        "说明面板自动淡出的默认时长", "交互输入开关", "docs/ 中提到可访问性的文件",
    }
    for item in sorted(set(obs) - changed):
        check(f"unchanged asset values: {item}", obs[item] == old_obs[item])
    panel = obs["热点说明面板背景（HotspotPanel.mat）"]
    board = obs["语言选择板背景（Language-Board.mat）"]
    check("panel alpha/blend/baseMap", panel["baseAlpha"] == 1 and panel["baseMapTexture"] == "{fileID: 0}"
          and panel["blendState"]["_SrcBlend"] == 5 and panel["blendState"]["_DstBlend"] == 10)
    check("URP Unlit package evidence", panel["shaderGuid"] == "650dd9526735d5b46b79224bc6e94025"
          and panel["shaderEvidence"]["name"] == "Universal Render Pipeline/Unlit"
          and panel["shaderEvidence"]["baseMapDefaultWhite"]
          and panel["shaderEvidence"]["baseColorIsColorProperty"]
          and panel["shaderEvidence"]["fragmentSource"]["alphaExpression"] == "texColor.a * _BaseColor.a")
    check("URP Lit board evidence", board["shaderGuid"] == "933532a4fcc9baf4fa0491de14d08ed7"
          and board["shaderEvidence"]["name"] == "Universal Render Pipeline/Lit"
          and board["shaderEvidence"]["baseColorIsColorProperty"])

    # Independent computation from serialized float inputs; do not reuse the audit's luminance function.
    def palette_ratio(rgb, bg):
        def luminance(color):
            return sum(weight * (value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4)
                       for weight, value in zip((0.2126, 0.7152, 0.0722), color))
        a, b = luminance(rgb), luminance(bg)
        return round((max(a, b) + 0.05) / (min(a, b) + 0.05), 2)

    expected = {
        "#FFFFFF": palette_ratio((1, 1, 1), (0.1, 0.12, 0.15)),
        "#EBF5F2": palette_ratio((0.92, 0.96, 0.95), (0.015, 0.045, 0.055)),
        "#4CFF8C": palette_ratio((0.3, 1, 0.55), (0.015, 0.045, 0.055)),
    }
    for item in ("热点面板文字 / 面板背景 的对比度", "语言选择板文字 / 板面背景 的对比度"):
        for pair in obs[item]:
            check(f"conditional palette / no rendered pass: {pair['textHex']}",
                  pair["conditionalSrgbPaletteRatio"] == expected[pair["textHex"]]
                  and pair["renderedContrastVerified"] is False)
    inputs = obs["交互输入开关"]
    check("Activate still requires input / hover only colors", inputs["allowHoveredActivate"] == "true"
          and "activateInteractionState.activatedThisFrame" in inputs["activateInputSource"]["shouldActivateCondition"]
          and inputs["hoverHandler"] == "ApplyColor(hoverColor);")
    timing = obs["说明面板自动淡出的默认时长"]
    check("30s hold / 1s fade / recall", timing["holdSeconds"] == 30 and timing["fadeSeconds"] == 1
          and timing["recallPathExists"] and timing["fadeScalesLabelAlpha"]
          and timing["fadeScalesBackingAlpha"] and timing["restartRestoresFullOpacity"])
    check("historic report bytes preserved", old_bytes == git("show", "5fd765d:artifacts/accessibility/qa/runs/20261010-accessibility-audit/report.json"))
    check("Unity project equals merged main", git("rev-parse", "HEAD:MuseumVR").strip()
          == git("rev-parse", "a8da4e8:MuseumVR").strip()
          and not git("diff", "HEAD", "--", "MuseumVR").strip())

    script = repo / "artifacts/accessibility/qa/runs/20261010-accessibility-audit/audit_accessibility.py"
    ast.parse(script.read_text(encoding="utf-8"))
    check("audit Python syntax", True)
    command = [sys.executable, str(script), "--repo", str(repo)]
    existing = subprocess.run(command + ["--out", str(historic_path)], capture_output=True)
    check("existing report rejected unchanged", existing.returncode == 1 and historic_path.read_bytes() == old_bytes)
    missing = subprocess.run(command, capture_output=True)
    check("explicit --out required", missing.returncode == 2)
    with tempfile.TemporaryDirectory(prefix="pr41-review-") as temp_dir:
        scratch = Path(temp_dir)
        no_cache_out = scratch / "no-package-cache.json"
        no_cache = subprocess.run(command + ["--package-cache", str(scratch / "absent-cache"),
                                            "--out", str(no_cache_out)], capture_output=True)
        check("audit works without package cache", no_cache.returncode == 0)
        no_cache_data = json.loads(no_cache_out.read_text(encoding="utf-8"))
        no_cache_obs = {o["item"]: o["value"] for o in no_cache_data["observations"]}
        check("missing package sources explicitly unverified", len(no_cache_data["observations"]) == 20
              and no_cache_obs["热点说明面板背景（HotspotPanel.mat）"]["shaderEvidence"]["available"] is False
              and no_cache_obs["交互输入开关"]["activateInputSource"]["available"] is False)

    doc = (repo / "docs/accessibility.zh-CN.md").read_text(encoding="utf-8")
    check("current doc drops obsolete assertions", "Unity 忠实" not in doc and "悬停即可激活" not in doc
          and "不必按扳机" not in doc and "在资产层面已经成立" not in doc
          and all(f"{ratio:.2f}:1" in doc for ratio in expected.values()))
    missing_links = []
    for label, target in re.findall(r"\[([^\]]+)\]\(([^)]+)\)", doc):
        if not target.startswith(("https://", "http://", "#")):
            path = target.split("#", 1)[0]
            if not (repo / "docs" / path).resolve().exists():
                missing_links.append(target)
    check("current doc local links resolve", not missing_links)
    result.update(
        gitHead=git("rev-parse", "HEAD").decode().strip(),
        reportSha256=hashlib.sha256(Path(args.report).read_bytes()).hexdigest(),
        historicReportSha256=hashlib.sha256(old_bytes).hexdigest(),
        scriptSha256=hashlib.sha256(script.read_bytes()).hexdigest(),
        python=sys.version.split()[0],
        passed=all(check["passed"] for check in result["checks"]),
    )
    with Path(args.out).open("x", encoding="utf-8", newline="\n") as stream:
        stream.write(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(f"Checks: {len(result['checks'])}; passed: {result['passed']}; output: {args.out}")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(errors="backslashreplace")
    raise SystemExit(main())
