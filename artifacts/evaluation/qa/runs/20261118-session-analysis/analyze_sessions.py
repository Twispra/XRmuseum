#!/usr/bin/env python3
"""把会话遥测 CSV 变成可复核的指标、指标候选与优先级 issue log。

设计原则
--------
1. **只做能自动判定的部分。** 行为层能算出"指了几次、按了几次、在哪两个目标之间来回"，
   算不出"为什么"。所以本工具产出的是**行为指标（B1–B6）**，不是定性编码（RD/ST/TM/LG/RS/NV）。
   定性编码由主持人按 `docs/evaluation/observation-and-coding.zh-CN.md` 手工填写，
   通过 `--coded` 传入本工具合并。两者在输出里用 `source` 列区分，不混为一谈。
2. **每条结论都必须带证据。** 每个指标都写明来自哪几场会话、哪个目标、多少次。
3. **不编造未观测到的东西。** 没有数据的维度直接不出现，不做"默认 0"式的假装测量。

用法
----
    python analyze_sessions.py --telemetry <会话CSV目录> --out <输出目录>
    python analyze_sessions.py --telemetry ... --coded coded-observations.csv --out ...
    python analyze_sessions.py --self-test

纯标准库，无第三方依赖。退出码 0 表示成功。
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import statistics
import sys
import tempfile
from pathlib import Path

# --------------------------------------------------------------------------------------
# 常量：与 docs/evaluation 的定义保持一致，改这里就要同时改文档。
# --------------------------------------------------------------------------------------

CODE_LABELS = {
    "RD": "可读性",
    "ST": "状态可辨",
    "TM": "时间压力",
    "LG": "语言与术语",
    "RS": "复位与收尾",
    "NV": "导航与朝向",
}

INDICATOR_DEFS = {
    "B1": {
        "title": "整场没有任何交互",
        "rule": "会话内 hover / activate / select 全部为 0",
        "severity": 3,
        "cost": "S",
        "requirement": "EQ4 可发现性",
        "note": "可能是不知道从哪开始，也可能是测试环境问题；必须回看录像或访谈再定性。",
    },
    "B2": {
        "title": "指得到但按不下去",
        "rule": "同一目标被 hover ≥3 次，且从未被 activate 或 select",
        "severity": 3,
        "cost": "S",
        "requirement": "EQ2 状态可辨 / EQ4 可发现性",
        "note": "行为层只能证明“指了没按”，原因（不知道要按、按错键、以为悬停即选中）需人工编码。",
    },
    "B3": {
        "title": "在目标之间反复折返",
        "rule": "交互目标序列中出现 ≥2 次 A→B→A 的折返（A、B 为任意两个目标）",
        "severity": 2,
        "cost": "S",
        "requirement": "EQ5 流程可理解",
        "note": "折返本身不是缺陷，可能是在比较；但它标记了“决策成本最高的位置”。",
    },
    "B4": {
        "title": "求助次数偏高",
        "rule": "面板求助计数 hints ≥2",
        "severity": 2,
        "cost": "M",
        "requirement": "EQ5 流程可理解",
        "note": "仅对走探索流程的会话有意义；未进入该流程的会话不参与判定。",
    },
    "B5": {
        "title": "面板按钮按了但流程没推进",
        "rule": "对 *Button 目标有 activate/select ≥1 次，但结束页面与开始页面相同",
        "severity": 4,
        "cost": "M",
        "requirement": "EQ5 流程可理解",
        "note": "属于阻断核心流程的强信号；也可能按钮在别处生效但面板未刷新，需人工确认。",
    },
    "B6": {
        "title": "首次交互明显滞后",
        "rule": "从进入场景到第一次 hover/activate/select 超过 20 秒",
        "severity": 1,
        "cost": "S",
        "requirement": "EQ4 可发现性",
        "note": "受读题与主持人指令影响很大，只在同一脚本下横向比较有意义。",
    },
}

COST_WEIGHT = {"S": 1.0, "M": 2.0, "L": 3.0}
FIRST_INTERACTION_LIMIT = 20.0
HOVER_STUCK_MIN = 3
OSCILLATION_MIN = 2
HINTS_HIGH = 2

SESSION_COLUMNS = [
    "t_seconds", "event", "target", "detail", "page", "clue", "hints",
    "air_seen", "voice_seen", "era_index", "era_name", "language",
    "cutaway", "floating", "window_open", "resets",
]


# --------------------------------------------------------------------------------------
# 读取
# --------------------------------------------------------------------------------------

def _to_int(value, default=0):
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return default


def _to_float(value, default=0.0):
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def read_session(path: Path) -> dict:
    """读一个遥测 CSV。缺列、缺 session_end 都不应导致崩溃。"""
    events = []
    last = {}
    with path.open("r", encoding="utf-8-sig", newline="") as fh:
        reader = csv.DictReader(fh)
        if reader.fieldnames is None:
            raise ValueError(f"{path.name}: 空文件")
        missing = [c for c in ("t_seconds", "event") if c not in reader.fieldnames]
        if missing:
            raise ValueError(f"{path.name}: 缺少必需列 {missing}")

        for row in reader:
            event = (row.get("event") or "").strip()
            target = (row.get("target") or "").strip()
            events.append({
                "t": _to_float(row.get("t_seconds")),
                "event": event,
                "target": target,
                "detail": (row.get("detail") or "").strip(),
                "page": (row.get("page") or "").strip(),
                "hints": _to_int(row.get("hints")),
            })
            last = row

    duration = 0.0
    for e in events:
        if e["event"] == "session_end":
            for part in e["detail"].split(";"):
                if part.startswith("duration="):
                    duration = _to_float(part.split("=", 1)[1])
            break
    if duration == 0.0 and events:
        duration = events[-1]["t"]

    return {
        "file": path.name,
        "path": str(path),
        "events": events,
        "duration": duration,
        "start_page": events[0]["page"] if events else "",
        "end_page": events[-1]["page"] if events else "",
        "hints_max": max((e["hints"] for e in events), default=0),
        "interactables": _extract_kv(events, "interactables"),
        "scene": _extract_kv(events, "scene"),
    }


def _extract_kv(events, key):
    for e in events:
        if e["event"] == "session_start":
            for part in e["detail"].split(";"):
                if part.startswith(key + "="):
                    return part.split("=", 1)[1]
    return ""


def target_kind(target: str) -> str:
    """按路径猜目标类型。只用于分组统计，不作为结论依据。"""
    lowered = target.lower()
    if "button" in lowered:
        return "button"
    if "hotspot" in lowered:
        return "hotspot"
    return "other"


# --------------------------------------------------------------------------------------
# 指标
# --------------------------------------------------------------------------------------

def session_metrics(session: dict) -> dict:
    hovers = [e for e in session["events"] if e["event"] == "hover"]
    activates = [e for e in session["events"] if e["event"] == "activate"]
    selects = [e for e in session["events"] if e["event"] == "select"]
    interactions = sorted(hovers + activates + selects, key=lambda e: e["t"])

    per_target = {}
    for e in hovers:
        per_target.setdefault(e["target"], {"hover": 0, "activate": 0, "select": 0})["hover"] += 1
    for e in activates:
        per_target.setdefault(e["target"], {"hover": 0, "activate": 0, "select": 0})["activate"] += 1
    for e in selects:
        per_target.setdefault(e["target"], {"hover": 0, "activate": 0, "select": 0})["select"] += 1

    return {
        "file": session["file"],
        "scene": session["scene"],
        "duration": round(session["duration"], 3),
        "interactables": session["interactables"],
        "hovers": len(hovers),
        "activates": len(activates),
        "selects": len(selects),
        "first_interaction_t": round(interactions[0]["t"], 3) if interactions else None,
        "unique_targets": len(per_target),
        "per_target": per_target,
        "start_page": session["start_page"],
        "end_page": session["end_page"],
        "hints_max": session["hints_max"],
        "button_actions": sum(1 for e in activates + selects if target_kind(e["target"]) == "button"),
        "sequence": [e["target"] for e in interactions if e["target"]],
    }


def oscillation_count(sequence):
    """统计 A→B→A 的折返次数（相隔一个目标回到原目标）。"""
    count = 0
    for i in range(2, len(sequence)):
        if sequence[i] == sequence[i - 2] and sequence[i] != sequence[i - 1]:
            count += 1
    return count


def detect_indicators(sessions, metrics):
    """返回 [{id, code, indicator, severity, cost, sessions:[], evidence}]。

    会话级指标每条会话最多命中一次；目标级指标按目标聚合。
    """
    total = len(metrics) or 1
    findings = []

    # ---- 会话级 ----
    def collect(indicator_id, predicate, evidence_fn):
        hits, items = [], []
        for s, m in zip(sessions, metrics):
            if not predicate(m):
                continue
            hits.append(m["file"])
            items.append(evidence_fn(m))
        if not hits:
            return
        definition = INDICATOR_DEFS[indicator_id]
        findings.append({
            "id": indicator_id,
            "code": "",
            "title": definition["title"],
            "source": "telemetry",
            "severity": definition["severity"],
            "cost": definition["cost"],
            "requirement": definition["requirement"],
            "affected_sessions": len(hits),
            "affected_ratio": round(len(hits) / total, 3),
            "sessions": hits,
            "evidence": items[:8],
            "note": definition["note"],
        })

    collect("B1", lambda m: m["hovers"] + m["activates"] + m["selects"] == 0,
            lambda m: f"{m['file']}: 时长 {m['duration']}s，无任何交互事件")

    collect("B6", lambda m: m["first_interaction_t"] is not None and m["first_interaction_t"] > FIRST_INTERACTION_LIMIT,
            lambda m: f"{m['file']}: 首次交互在 {m['first_interaction_t']}s")

    collect("B5", lambda m: m["button_actions"] >= 1 and m["end_page"] == m["start_page"] and m["start_page"],
            lambda m: f"{m['file']}: 按钮动作 {m['button_actions']} 次，页面仍为 {m['end_page']}")

    collect("B4", lambda m: m["end_page"] not in ("", "Welcome") and m["hints_max"] >= HINTS_HIGH,
            lambda m: f"{m['file']}: 求助 {m['hints_max']} 次")

    collect("B3", lambda m: oscillation_count(m["sequence"]) >= OSCILLATION_MIN,
            lambda m: f"{m['file']}: 折返 {oscillation_count(m['sequence'])} 次，序列 {m['sequence'][:8]}")

    # ---- 目标级：B2 ----
    stuck = {}
    for s, m in zip(sessions, metrics):
        for target, stat in m["per_target"].items():
            if stat["hover"] >= HOVER_STUCK_MIN and stat["activate"] == 0 and stat["select"] == 0:
                entry = stuck.setdefault(target, {"sessions": [], "hovers": 0, "max_in_session": 0})
                entry["sessions"].append(m["file"])
                entry["hovers"] += stat["hover"]
                entry["max_in_session"] = max(entry["max_in_session"], stat["hover"])
    if stuck:
        definition = INDICATOR_DEFS["B2"]
        ordered = sorted(stuck.items(), key=lambda kv: -kv[1]["hovers"])
        affected = set()
        for _, entry in ordered:
            affected.update(entry["sessions"])
        findings.append({
            "id": "B2",
            "code": "",
            "title": definition["title"],
            "source": "telemetry",
            "severity": definition["severity"],
            "cost": definition["cost"],
            "requirement": definition["requirement"],
            "affected_sessions": len(affected),
            "affected_ratio": round(len(affected) / total, 3),
            "sessions": sorted(affected),
            "evidence": [
                f"{target}：{len(entry['sessions'])} 场会话命中，单场最高 hover {entry['max_in_session']} 次，"
                f"累计 {entry['hovers']} 次，从未按下"
                for target, entry in ordered[:8]
            ],
            "note": definition["note"],
        })

    return findings


def aggregate(metrics):
    if not metrics:
        return {}
    durations = [m["duration"] for m in metrics]
    activations = sum(m["activates"] for m in metrics)
    hovers = sum(m["hovers"] for m in metrics)
    started = sum(1 for m in metrics if (m["hovers"] + m["activates"] + m["selects"]) > 0)
    first = [m["first_interaction_t"] for m in metrics if m["first_interaction_t"] is not None]
    return {
        "sessions": len(metrics),
        "total_duration_s": round(sum(durations), 3),
        "median_duration_s": round(statistics.median(durations), 3),
        "sessions_with_interaction": started,
        "hover_total": hovers,
        "activate_total": activations,
        "select_total": sum(m["selects"] for m in metrics),
        "activate_per_hover": round(activations / hovers, 3) if hovers else None,
        "median_first_interaction_s": round(statistics.median(first), 3) if first else None,
        "sessions_reaching_non_welcome": sum(1 for m in metrics if m["end_page"] not in ("", "Welcome")),
    }


# --------------------------------------------------------------------------------------
# 人工编码（可选输入）
# --------------------------------------------------------------------------------------

CODED_COLUMNS = ["session_id", "participant", "code", "severity", "cost", "excerpt", "requirement", "decision"]


def load_coded(path: Path):
    rows = []
    with path.open("r", encoding="utf-8-sig", newline="") as fh:
        reader = csv.DictReader(fh)
        missing = [c for c in ("session_id", "code", "severity") if c not in (reader.fieldnames or [])]
        if missing:
            raise ValueError(f"{path.name}: 缺少必需列 {missing}（模板列：{CODED_COLUMNS}）")
        for row in reader:
            code = (row.get("code") or "").strip().upper()
            if not code:
                continue
            if code not in CODE_LABELS and not code.startswith("X"):
                raise ValueError(f"{path.name}: 未知编码 {code}；预设码 {sorted(CODE_LABELS)}，归纳码用 X1/X2…")
            rows.append({
                "session_id": (row.get("session_id") or "").strip(),
                "participant": (row.get("participant") or "").strip(),
                "code": code,
                "severity": _to_int(row.get("severity")),
                "cost": (row.get("cost") or "M").strip().upper() or "M",
                "excerpt": (row.get("excerpt") or "").strip(),
                "requirement": (row.get("requirement") or "").strip(),
                "decision": (row.get("decision") or "").strip(),
            })
    return rows


def coded_findings(coded, total_sessions):
    grouped = {}
    for row in coded:
        grouped.setdefault(row["code"], []).append(row)

    out = []
    for code, rows in grouped.items():
        sessions = sorted({r["session_id"] for r in rows if r["session_id"]})
        severity = max(r["severity"] for r in rows)
        cost = rows[0]["cost"] if rows[0]["cost"] in COST_WEIGHT else "M"
        label = CODE_LABELS.get(code, f"归纳码 {code}")
        out.append({
            "id": code,
            "code": code,
            "title": f"{label}（人工编码）",
            "source": "coded",
            "severity": severity,
            "cost": cost,
            "requirement": rows[0]["requirement"] or "-",
            "affected_sessions": len(sessions),
            "affected_ratio": round(len(sessions) / (total_sessions or 1), 3),
            "sessions": sessions,
            "evidence": [f"{r['session_id'] or '?'}: {r['excerpt']}" for r in rows[:8]],
            "note": "来自主持人手工编码。",
            "decision": rows[0]["decision"],
        })
    return out


# --------------------------------------------------------------------------------------
# 优先级与输出
# --------------------------------------------------------------------------------------

def priority_of(finding):
    cost = COST_WEIGHT.get(finding["cost"], 2.0)
    return round(finding["severity"] * finding["affected_ratio"] / cost, 3)


def finalize(findings):
    for f in findings:
        f["priority"] = priority_of(f)
        f.setdefault("decision", "")
    findings.sort(key=lambda f: (-f["priority"], -f["severity"], f["id"]))
    return findings


def write_outputs(out_dir: Path, report: dict):
    out_dir.mkdir(parents=True, exist_ok=True)

    (out_dir / "report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")

    log_path = out_dir / "issue-log.csv"
    with log_path.open("w", encoding="utf-8", newline="") as fh:
        writer = csv.writer(fh, lineterminator="\n")
        writer.writerow(["id", "title", "source", "code", "severity", "cost",
                         "affected_sessions", "affected_ratio", "priority",
                         "requirement", "decision", "evidence"])
        for f in report["findings"]:
            writer.writerow([
                f["id"], f["title"], f["source"], f["code"] or "-", f["severity"], f["cost"],
                f["affected_sessions"], f["affected_ratio"], f["priority"],
                f["requirement"], f["decision"] or "（待定）",
                " | ".join(f["evidence"]),
            ])

    (out_dir / "summary.md").write_text(render_summary(report), encoding="utf-8", newline="\n")


def render_summary(report: dict) -> str:
    agg = report["aggregate"]
    lines = []
    lines.append("# 会话指标与优先级（自动生成）\n")
    lines.append(f"> 生成时间：{report['generated_at']}　输入：`{report['input_dir']}`　会话数：**{agg.get('sessions', 0)}**\n")
    lines.append(f"> {report['scope_note']}\n")
    lines.append("> 复跑：`python analyze_sessions.py --telemetry <目录> --out <目录>`"
                 "（加 `--self-test` 可先用合成夹具自检）。\n")

    section = iter(range(1, 10))

    lines.append(f"## {next(section)}. 聚合指标\n")
    lines.append("| 指标 | 值 |")
    lines.append("| --- | --- |")
    for key, label in [
        ("sessions", "会话数"),
        ("total_duration_s", "总时长（秒）"),
        ("median_duration_s", "时长中位数（秒）"),
        ("sessions_with_interaction", "有交互的会话数"),
        ("hover_total", "hover 总数"),
        ("activate_total", "activate（扳机）总数"),
        ("select_total", "select（抓握）总数"),
        ("activate_per_hover", "每次 hover 对应的 activate 数"),
        ("median_first_interaction_s", "首次交互中位数（秒）"),
        ("sessions_reaching_non_welcome", "推进到 Welcome 以外页面的会话数"),
    ]:
        lines.append(f"| {label} | {agg.get(key)} |")

    lines.append(f"\n## {next(section)}. 逐场会话\n")
    lines.append("| 文件 | 时长(s) | hover | activate | select | 首交互(s) | 唯一目标 | 结束页 | hints |")
    lines.append("| --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for s in report["sessions"]:
        first = "-" if s["first_interaction_t"] is None else s["first_interaction_t"]
        lines.append(f"| `{s['file']}` | {s['duration']} | {s['hovers']} | {s['activates']} | "
                     f"{s['selects']} | {first} | {s['unique_targets']} | {s['end_page'] or '-'} | {s['hints_max']} |")

    lines.append(f"\n## {next(section)}. 指标与优先级\n")
    lines.append("优先级 = 严重度 × 影响会话占比 ÷ 成本（S=1，M=2，L=3）。")
    lines.append("严重度为 Nielsen 0–4，与 `docs/evaluation/observation-and-coding.zh-CN.md` 一致。\n")
    lines.append("| 编号 | 指标 | 来源 | 严重度 | 成本 | 影响会话 | 占比 | 优先级 |")
    lines.append("| --- | --- | --- | --- | --- | --- | --- | --- |")
    for f in report["findings"]:
        lines.append(f"| {f['id']} | {f['title']} | {f['source']} | {f['severity']} | {f['cost']} | "
                     f"{f['affected_sessions']} | {f['affected_ratio']} | {f['priority']} |")

    if report["findings"]:
        lines.append("\n### 证据\n")
        for f in report["findings"]:
            lines.append(f"**{f['id']}　{f['title']}**（关联 `{f['requirement']}`）")
            for item in f["evidence"]:
                lines.append(f"- {item}")
            lines.append(f"- 备注：{f['note']}")
            lines.append("")

    if report.get("coded_observations"):
        lines.append(f"## {next(section)}. 人工编码观察\n")
        lines.append("| 会话 | 参与者 | 编码 | 严重度 | 摘录 |")
        lines.append("| --- | --- | --- | --- | --- |")
        for r in report["coded_observations"]:
            lines.append(f"| {r['session_id'] or '-'} | {r['participant'] or '-'} | {r['code']} | "
                         f"{r['severity']} | {r['excerpt'][:60]} |")
        lines.append("")

    lines.append(f"## {next(section)}. 本工具不做的事\n")
    lines.append("- 不做定性归因：行为层只能证明“指了几次、按了几次”，不能证明“为什么”。")
    lines.append("- 不替代主持人编码：预设码 RD/ST/TM/LG/RS/NV 需人工判读后通过 `--coded` 传入。")
    lines.append("- 不外推：样本量与抽样方式决定结论只能用于本轮改进优先级。")
    return "\n".join(lines) + "\n"


# --------------------------------------------------------------------------------------
# 自测
# --------------------------------------------------------------------------------------

SELF_TEST_SESSIONS = {
    "a.csv": [
        "0.0,session_start,MuseumVR,scene=Test;interactables=5,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "3.0,hover,Exhibit/Hotspot A,n=1,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "5.0,hover,Exhibit/Hotspot B,n=2,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "7.0,hover,Exhibit/Hotspot A,n=3,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "9.0,hover,Exhibit/Hotspot B,n=4,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "11.0,session_end,,duration=11.0;selects=0;activates=0;hovers=4,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
    ],
    "b.csv": [
        "0.0,session_start,MuseumVR,scene=Test;interactables=5,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "4.0,hover,Exhibit/Hotspot A,n=1,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "6.0,activate,Exhibit/Hotspot A,first;n=1,Air,-,0,1,0,1,Era,English,0,0,0,0",
        "9.0,session_end,,duration=9.0;selects=0;activates=1;hovers=1,Air,-,0,1,0,1,Era,English,0,0,0,0",
    ],
    "c.csv": [
        "0.0,session_start,MuseumVR,scene=Test;interactables=5,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "30.0,activate,Panel/Inspect Button,first;n=1,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "33.0,session_end,,duration=33.0;selects=0;activates=1;hovers=0,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
    ],
}


def run_self_test() -> int:
    header = "t_seconds,event,target,detail,page,clue,hints,air_seen,voice_seen,era_index,era_name,language,cutaway,floating,window_open,resets"
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        src = root / "in"
        src.mkdir()
        for name, rows in SELF_TEST_SESSIONS.items():
            (src / name).write_text(header + "\n" + "\n".join(rows) + "\n", encoding="utf-8")

        sessions = [read_session(p) for p in sorted(src.glob("*.csv"))]
        metrics = [session_metrics(s) for s in sessions]
        findings = finalize(detect_indicators(sessions, metrics))
        found = {f["id"] for f in findings}

        checks = []

        def check(name, cond):
            checks.append((name, bool(cond)))

        # a.csv: 4 次 hover、0 次按下、两个目标各 2 次 → B3 命中（≥2 折返），B2 不命中（hover 均 <3）
        ma = metrics[0]
        check("a: hover=4 activate=0", ma["hovers"] == 4 and ma["activates"] == 0)
        check("a: 折返计数=2", oscillation_count(ma["sequence"]) == 2)
        check("a: B3 命中", "B3" in found)
        check("a: B2 不应命中（单目标 hover<3）", "B2" not in found)
        # b.csv: 正常推进 → 不应被 B5 波及（B5 只应命中 c.csv）
        mb = metrics[1]
        check("b: 结束页=Air", mb["end_page"] == "Air")
        check("b: 未进入 B5 名单",
              "b.csv" not in next((f["sessions"] for f in findings if f["id"] == "B5"), []))
        # c.csv: 按钮按下但页面未变 → B5；首交互 30s → B6
        check("c: B5 命中", "B5" in found)
        check("c: B5 只命中 c.csv",
              next(f["sessions"] for f in findings if f["id"] == "B5") == ["c.csv"])
        check("c: B6 命中", "B6" in found)
        check("c: B5 严重度=4", next(f["severity"] for f in findings if f["id"] == "B5") == 4)

        # 输出可写出且可解析
        out = root / "out"
        report = {
            "generated_at": "self-test",
            "input_dir": str(src),
            "scope_note": "self-test",
            "aggregate": aggregate(metrics),
            "sessions": metrics,
            "findings": findings,
            "coded_observations": [],
        }
        write_outputs(out, report)
        check("输出 report.json 可解析",
              json.loads((out / "report.json").read_text(encoding="utf-8"))["aggregate"]["sessions"] == 3)
        check("输出 issue-log.csv 行数", (out / "issue-log.csv").read_text(encoding="utf-8").count("\n") == len(findings) + 1)
        check("输出 summary.md 非空", len((out / "summary.md").read_text(encoding="utf-8")) > 400)

    passed = sum(1 for _, ok in checks if ok)
    for name, ok in checks:
        print(("  PASS  " if ok else "  FAIL  ") + name)
    print(f"\n自测：{passed}/{len(checks)} 通过")
    return 0 if passed == len(checks) else 1


# --------------------------------------------------------------------------------------
# 入口
# --------------------------------------------------------------------------------------

def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="会话遥测 → 指标 → 优先级 issue log")
    parser.add_argument("--telemetry", help="存放会话 CSV 的目录")
    parser.add_argument("--coded", help="主持人手工编码 CSV（可选）")
    parser.add_argument("--out", help="输出目录")
    parser.add_argument("--scope-note", default="**本文件为组员自测数据，不是评价参与者数据，不得作为评价结论引用。**",
                        help="写进 summary.md 的范围声明")
    parser.add_argument("--self-test", action="store_true", help="用合成夹具自检，不读外部数据")
    args = parser.parse_args(argv)

    if args.self_test:
        return run_self_test()

    if not args.telemetry or not args.out:
        parser.error("需要 --telemetry 与 --out（或用 --self-test）")

    src = Path(args.telemetry)
    if not src.is_dir():
        print(f"目录不存在：{src}", file=sys.stderr)
        return 2

    files = sorted(src.glob("*.csv"))
    if not files:
        print(f"{src} 里没有 CSV", file=sys.stderr)
        return 2

    sessions, metrics = [], []
    for path in files:
        try:
            session = read_session(path)
        except ValueError as exc:
            print(f"跳过 {path.name}: {exc}", file=sys.stderr)
            continue
        sessions.append(session)
        metrics.append(session_metrics(session))

    findings = detect_indicators(sessions, metrics)
    coded = []
    if args.coded:
        coded = load_coded(Path(args.coded))
    findings += coded_findings(coded, len(metrics))
    findings = finalize(findings)

    report = {
        "generated_at": __import__("datetime").datetime.now().astimezone().isoformat(timespec="seconds"),
        "input_dir": str(src),
        "scope_note": args.scope_note,
        "aggregate": aggregate(metrics),
        "sessions": metrics,
        "findings": findings,
        "coded_observations": coded,
    }
    write_outputs(Path(args.out), report)

    agg = report["aggregate"]
    print(f"会话 {agg['sessions']} 场｜hover {agg['hover_total']}｜activate {agg['activate_total']}｜"
          f"指标 {len(findings)} 条")
    for f in findings[:5]:
        print(f"  {f['id']:<3} sev={f['severity']} 优先级={f['priority']:<5} {f['title']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
