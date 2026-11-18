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
import io
import json
import math
import statistics
import sys
import tempfile
from contextlib import redirect_stderr
from itertools import groupby
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
        "title": "会话未记录交互事件",
        "rule": "会话内 hover / activate / select 全部为 0",
        "severity": 3,
        "cost": "S",
        "requirement": "EQ4 可发现性",
        "note": "可能是不知道从哪开始，也可能是测试环境问题；必须回看录像或访谈再定性。",
    },
    "B2": {
        "title": "反复悬停且未记录选择或激活",
        "rule": "同一目标被 hover ≥3 次，且从未被 activate 或 select",
        "severity": 3,
        "cost": "S",
        "requirement": "EQ2 状态可辨 / EQ4 可发现性",
        "note": "请结合观察记录确认是否尝试操作，以及使用的输入是否触发了 XRI 事件。",
    },
    "B3": {
        "title": "在目标之间反复折返",
        "rule": "合并相邻同目标事件后，目标序列中出现 ≥2 次 A→B→A 的折返",
        "severity": 2,
        "cost": "S",
        "requirement": "EQ5 流程可理解",
        "note": "回看场次以区分正常比较与操作困难。",
    },
    "B4": {
        "title": "求助次数偏高",
        "rule": "会话曾记录到有效的非 Welcome 页面，且求助计数 hints ≥2",
        "severity": 2,
        "cost": "M",
        "requirement": "EQ5 流程可理解",
        "note": "仅对走探索流程的会话有意义；未进入该流程的会话不参与判定。",
    },
    "B5": {
        "title": "选择探索按钮后未观测到预期换页",
        "rule": "选择明确换页的探索按钮后，在下一次按键或会话边界前至少观测 1 秒，页面仍未改变",
        "severity": 4,
        "cost": "M",
        "requirement": "EQ5 流程可理解",
        "note": "按潜在阻断优先核查，须回看场次确认动作是否执行及任务能否完成。",
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
PAGE_OBSERVATION_SECONDS = 1.0

# Match the saved HelmetExplorationButton names and the station's runtime controls.
EXPLORATION_BUTTON_PAGES = {
    "Exploration AIR": "Air",
    "Exploration VOICE": "Voice",
    "Exploration HINT": "Hint",
    "Exploration REVIEW": "Review",
}
PHYSICAL_CONTROL_PAGES = {
    "Exhibit operation markers/Physical UI - Air": "Air",
    "Exhibit operation markers/Physical UI - Voice": "Voice",
}
EXPLORATION_PAGES = {"Welcome", "Air", "Voice", "Hint", "Review"}

SESSION_COLUMNS = [
    "t_seconds", "event", "target", "detail", "page", "clue", "hints",
    "air_seen", "voice_seen", "era_index", "era_name", "language",
    "cutaway", "floating", "window_open", "resets",
]


# --------------------------------------------------------------------------------------
# 读取
# --------------------------------------------------------------------------------------

def _to_float(value, default=0.0):
    try:
        number = float(value)
    except (TypeError, ValueError):
        return default
    return number if math.isfinite(number) else default


def read_session(path: Path) -> dict:
    """读一个遥测 CSV。缺列、缺 session_end 都不应导致崩溃。"""
    events = []
    with path.open("r", encoding="utf-8-sig", newline="") as fh:
        reader = csv.DictReader(fh)
        if reader.fieldnames is None:
            raise ValueError(f"{path.name}: 空文件")
        missing = [c for c in ("t_seconds", "event") if c not in reader.fieldnames]
        if missing:
            raise ValueError(f"{path.name}: 缺少必需列 {missing}")

        for row in reader:
            event = (row.get("event") or "").strip()
            time = _to_float(row.get("t_seconds"), None)
            if not event or time is None or time < 0:
                raise ValueError(f"第 {reader.line_num} 行：事件名或时间无效")
            page = (row.get("page") or "").strip()
            events.append({
                "t": time,
                "event": event,
                "target": (row.get("target") or "").strip(),
                "detail": (row.get("detail") or "").strip(),
                "page": page if page in EXPLORATION_PAGES else "",
                "hints": max(0, int(_to_float(row.get("hints")))),
            })
    if not events:
        raise ValueError("没有事件记录")

    duration = 0.0
    for e in events:
        if e["event"] == "session_end":
            for part in e["detail"].split(";"):
                if part.startswith("duration="):
                    duration = max(0.0, _to_float(part.split("=", 1)[1]))
            break
    if duration == 0.0:
        duration = events[-1]["t"]

    return {
        "file": path.name,
        "path": str(path),
        "events": events,
        "duration": duration,
        "start_page": events[0]["page"],
        "end_page": events[-1]["page"],
        "hints_max": max(e["hints"] for e in events),
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


def observed_page_actions(events):
    """用同一次操作后的有效快照，检查已知控件的目标页面。"""
    attempts = []
    pending = None
    for event in events:
        kind, page, time = event["event"], event["page"], event["t"]
        if pending:
            before = pending["from_page"]
            boundary = kind in ("select", "activate", "session_start", "scene_loaded")
            if boundary or not page or time < pending["t"]:
                pending = None
            elif page != before:
                pending["outcome"] = "destination_observed" if page == pending["expected_page"] else "other_page_observed"
                pending["observed_at"] = time
                pending["observed_page"] = page
                pending = None
            else:
                pending["observed_at"] = time
                pending["observed_page"] = page
                if kind == "session_end":
                    pending = None

        if kind != "select":
            continue
        target = event["target"]
        target_parts = target.split("/")
        expected = EXPLORATION_BUTTON_PAGES.get(target_parts[-1])
        physical = expected is None
        if physical:
            expected = PHYSICAL_CONTROL_PAGES.get("/".join(target_parts[-2:]))
        if not expected or page == expected:
            continue

        attempt = {
            "target": target, "t": time, "from_page": page, "expected_page": expected,
            "physical": physical, "outcome": "inconclusive", "observed_at": None, "observed_page": "",
        }
        attempts.append(attempt)
        if page:
            pending = attempt

    for attempt in attempts:
        # Physical inputs confirm on release, and a matching visible demo may toggle off.
        # Select-enter alone proves neither a command nor a failure.
        time = attempt["observed_at"]
        if (not attempt["physical"] and attempt["outcome"] == "inconclusive"
                and time is not None and time - attempt["t"] >= PAGE_OBSERVATION_SECONDS):
            attempt["outcome"] = "unchanged"
    return attempts


# --------------------------------------------------------------------------------------
# 指标
# --------------------------------------------------------------------------------------

def session_metrics(session: dict) -> dict:
    interaction_types = ("hover", "activate", "select")
    counts = dict.fromkeys(interaction_types, 0)
    per_target = {}
    interactions = sorted((e for e in session["events"] if e["event"] in counts), key=lambda e: e["t"])
    for event in interactions:
        kind = event["event"]
        counts[kind] += 1
        if event["target"]:
            per_target.setdefault(event["target"], dict.fromkeys(interaction_types, 0))[kind] += 1
    sequence = [event["target"] for event in interactions if event["target"]]
    page_history = [page for page, _ in groupby(e["page"] for e in session["events"] if e["page"])]

    return {
        "file": session["file"],
        "scene": session["scene"],
        "duration": round(session["duration"], 3),
        "interactables": session["interactables"],
        "hovers": counts["hover"],
        "activates": counts["activate"],
        "selects": counts["select"],
        "first_interaction_t": round(interactions[0]["t"], 3) if interactions else None,
        "unique_targets": len(per_target),
        "per_target": per_target,
        "start_page": session["start_page"],
        "end_page": session["end_page"],
        "hints_max": session["hints_max"],
        "page_history": page_history,
        "reached_non_welcome": any(page != "Welcome" for page in page_history),
        "page_actions": observed_page_actions(session["events"]),
        "sequence": sequence,
    }


def oscillation_count(sequence):
    """同一目标的连贯操作算一次停留，再统计目标间 A→B→A 折返。"""
    targets = [target for target, _ in groupby(sequence)]
    return sum(first == third for first, third in zip(targets, targets[2:]))


def detect_indicators(metrics):
    """返回 [{id, code, indicator, severity, cost, sessions:[], evidence}]。

    会话级指标每条会话最多命中一次；目标级指标按目标聚合。
    """
    total = len(metrics) or 1
    findings = []

    # ---- 会话级 ----
    def collect(indicator_id, predicate, evidence_fn):
        hits, items = [], []
        for m in metrics:
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

    def page_action_evidence(m):
        return "；".join(
            f"{m['file']} {action['t']}s：{action['target']} 预期进入 {action['expected_page']}，"
            f"至 {action['observed_at']}s 仍为 {action['from_page']}"
            for action in m["page_actions"] if action["outcome"] == "unchanged"
        )

    collect("B5", lambda m: any(action["outcome"] == "unchanged" for action in m["page_actions"]),
            page_action_evidence)

    collect("B4", lambda m: m["reached_non_welcome"] and m["hints_max"] >= HINTS_HIGH,
            lambda m: f"{m['file']}: 求助 {m['hints_max']} 次")

    def oscillation_evidence(m):
        targets = [target for target, _ in groupby(m["sequence"])]
        return f"{m['file']}: 折返 {oscillation_count(m['sequence'])} 次，合并同目标后的序列 {targets[:8]}"

    collect("B3", lambda m: oscillation_count(m["sequence"]) >= OSCILLATION_MIN, oscillation_evidence)

    # ---- 目标级：B2 ----
    stuck = {}
    for m in metrics:
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
                f"累计 {entry['hovers']} 次，未记录 activate 或 select"
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
        "sessions_reaching_non_welcome": sum(m["reached_non_welcome"] for m in metrics),
    }


# --------------------------------------------------------------------------------------
# 人工编码（可选输入）
# --------------------------------------------------------------------------------------

CODED_COLUMNS = ["session_id", "participant", "code", "severity", "cost", "excerpt", "requirement", "decision"]


def load_coded(path: Path, session_ids):
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
            session_id = (row.get("session_id") or "").strip()
            severity = _to_float(row.get("severity"), None)
            cost = (row.get("cost") or "").strip().upper() or "M"
            row_label = f"{path.name} 第 {reader.line_num} 行"
            if session_id not in session_ids:
                raise ValueError(f"{row_label}：session_id {session_id!r} 必须匹配本次已读取的遥测 CSV 文件名")
            if severity not in range(5):
                raise ValueError(f"{row_label}：severity 须为 0–4 的整数")
            if cost not in COST_WEIGHT:
                raise ValueError(f"{row_label}：cost 须为 S、M 或 L")
            rows.append({
                "session_id": session_id,
                "participant": (row.get("participant") or "").strip(),
                "code": code,
                "severity": int(severity),
                "cost": cost,
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
        sessions = sorted({r["session_id"] for r in rows})
        severity = max(r["severity"] for r in rows)
        costs = {r["cost"] for r in rows}
        if len(costs) != 1:
            raise ValueError(f"编码 {code} 的 cost 冲突：{' / '.join(sorted(costs))}；请为同一编码统一成本")
        requirements = sorted({r["requirement"] for r in rows if r["requirement"]})
        decisions = {r["decision"] for r in rows if r["decision"]}
        label = CODE_LABELS.get(code, f"归纳码 {code}")
        out.append({
            "id": code,
            "code": code,
            "title": f"{label}（人工编码）",
            "source": "coded",
            "severity": severity,
            "cost": next(iter(costs)),
            "requirement": " / ".join(requirements) or "-",
            "affected_sessions": len(sessions),
            "affected_ratio": round(len(sessions) / (total_sessions or 1), 3),
            "sessions": sessions,
            "evidence": [f"{r['session_id']}: {r['excerpt']}" for r in rows[:8]],
            "note": "来自主持人手工编码。",
            "decision": next(iter(decisions)) if len(decisions) == 1 else "",
        })
    return out


# --------------------------------------------------------------------------------------
# 优先级与输出
# --------------------------------------------------------------------------------------

def finalize(findings, total_sessions):
    # The denominator is common across findings. Sort before rounding for display.
    def weighted_count(finding):
        return finding["severity"] * finding["affected_sessions"] / COST_WEIGHT[finding["cost"]]

    findings.sort(key=lambda f: (-weighted_count(f), -f["severity"], f["id"]))
    for f in findings:
        f["priority"] = round(weighted_count(f) / total_sessions, 3)
        f.setdefault("decision", "")
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
        ("activate_total", "activate 总数"),
        ("select_total", "select 总数"),
        ("activate_per_hover", "每次 hover 对应的 activate 数"),
        ("median_first_interaction_s", "首次交互中位数（秒）"),
        ("sessions_reaching_non_welcome", "曾观测到有效非 Welcome 页的会话数"),
    ]:
        lines.append(f"| {label} | {agg.get(key)} |")

    lines.append(f"\n## {next(section)}. 逐场会话\n")
    lines.append("结束页以末条快照为准，`-` 表示未知。曾到达的页面按整场有效快照统计。\n")
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
        "30.0,select,Panel/Exploration AIR,first;n=1,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
        "33.0,session_end,,duration=33.0;selects=1;activates=0;hovers=0,Welcome,-,0,0,0,1,Era,English,0,0,0,0",
    ],
}


def run_self_test() -> int:
    header = ",".join(SESSION_COLUMNS)
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        src = root / "in"
        src.mkdir()
        for name, rows in SELF_TEST_SESSIONS.items():
            (src / name).write_text(header + "\n" + "\n".join(rows) + "\n", encoding="utf-8")

        sessions = [read_session(p) for p in sorted(src.glob("*.csv"))]
        metrics = [session_metrics(s) for s in sessions]
        findings = finalize(detect_indicators(metrics), len(metrics))
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

        # Check the page history and evidence from exact current control names.
        cases = {
            "unknown": [(0, "session_start", "", "-", 0), (5, "session_end", "", "-", "inf")],
            "return": [
                (0, "session_start", "", "Welcome", 0),
                (1, "select", "Panel/Exploration AIR", "Welcome", 0),
                (2, "state", "", "Air", 2),
                (3, "select", "Panel/Exploration BACK", "Air", 2),
                (4, "session_end", "", "Welcome", 2),
            ],
            "language": [
                (0, "session_start", "", "Welcome", 0),
                (1, "select", "Language Selection/Language Button - Chinese", "Welcome", 0),
                (3, "session_end", "", "Welcome", 0),
            ],
            "physical": [
                (0, "session_start", "", "Welcome", 0),
                (1, "select", "HelmetExhibit/Exhibit operation markers/Physical UI - Air", "Welcome", 0),
                (3, "state", "", "Air", 0),
                (4, "select", "HelmetExhibit/Exhibit operation markers/Physical UI - Voice", "Air", 0),
                (7, "session_end", "", "Air", 0),
            ],
            "interrupted": [
                (0, "session_start", "", "Welcome", 0),
                (1, "select", "Panel/Exploration AIR", "Welcome", 0),
                (2, "select", "Panel/Exploration VOICE", "Welcome", 0),
                (2.1, "state", "", "Voice", 0),
                (3, "select", "Panel/Exploration HINT", "Voice", 0),
                (3.1, "state", "", "-", 0),
                (8, "session_end", "", "Voice", 0),
            ],
            "wrong_input": [
                (0, "session_start", "", "Welcome", 0),
                (1, "activate", "Panel/Exploration AIR", "Welcome", 0),
                (4, "session_end", "", "Welcome", 0),
            ],
        }
        for name, rows in cases.items():
            path = root / f"{name}.csv"
            with path.open("w", encoding="utf-8", newline="") as fh:
                writer = csv.writer(fh)
                writer.writerow(("t_seconds", "event", "target", "page", "hints"))
                writer.writerows(rows)
        page_metrics = {name: session_metrics(read_session(root / f"{name}.csv")) for name in cases}
        page_findings = {f["id"]: f for f in detect_indicators(list(page_metrics.values()))}
        check("未知页与非有限 hints 不计入有效观测",
              page_metrics["unknown"]["end_page"] == "" and page_metrics["unknown"]["hints_max"] == 0
              and not page_metrics["unknown"]["reached_non_welcome"])
        check("折返后仍计入曾到达非 Welcome，且符合 B4",
              page_metrics["return"]["page_history"] == ["Welcome", "Air", "Welcome"]
              and aggregate(list(page_metrics.values()))["sessions_reaching_non_welcome"] == 3
              and page_findings.get("B4", {}).get("sessions") == ["return.csv"])
        check("语言按钮、正常折返、物理选择与中断观察均不误报 B5", "B5" not in page_findings)
        check("物理探索控件按目标与快照记录，未确认的动作仍待人工核查",
              [a["outcome"] for a in page_metrics["physical"]["page_actions"]]
              == ["destination_observed", "inconclusive"])
        check("旧探索按钮只响应 select，扳机不算换页动作", not page_metrics["wrong_input"]["page_actions"])
        check("同目标 hover 与 activate 合并后保留折返", oscillation_count(["A", "A", "B", "B", "A", "A", "B", "B"]) == 2)

        coded_path = root / "coded.csv"

        def write_coded(rows):
            with coded_path.open("w", encoding="utf-8", newline="") as fh:
                writer = csv.writer(fh)
                writer.writerow(("session_id", "code", "severity", "cost", "excerpt"))
                writer.writerows(rows)

        valid_rows = [("a.csv", "RD", 2, "S", "one"), ("b.csv", "RD", 4, "S", "two"),
                      ("a.csv", "RD", 3, "S", "three")]
        write_coded(valid_rows)
        valid = load_coded(coded_path, {"a.csv", "b.csv", "c.csv"})
        coded_finding = finalize(coded_findings(valid, 3), 3)[0]
        check("人工编码按去重会话计占比并用未舍入占比算优先级",
              coded_finding["affected_sessions"] == 2 and coded_finding["affected_ratio"] == 0.667
              and coded_finding["severity"] == 4 and coded_finding["priority"] == 2.667)

        conflict_rows = [("a.csv", "RD", 4, "S", "one"), ("b.csv", "RD", 4, "L", "two")]
        for name, rows, message in [
            ("未知场次", [("missing.csv", "RD", 2, "M", "one")], "session_id"),
            ("丢失场次", [("", "RD", 2, "M", "one")], "session_id"),
            ("无效严重度", [("a.csv", "RD", 99, "S", "one")], "severity"),
            ("无效成本", [("a.csv", "RD", 4, "XL", "one")], "cost"),
            ("S/L 成本冲突", conflict_rows, "cost 冲突"),
            ("L/S 成本冲突", list(reversed(conflict_rows)), "cost 冲突"),
        ]:
            write_coded(rows)
            error = io.StringIO()
            invalid_out = root / "invalid-out"
            with redirect_stderr(error):
                exit_code = main(["--telemetry", str(src), "--coded", str(coded_path), "--out", str(invalid_out)])
            check(f"拒绝{name}，不生成产物", exit_code == 2 and message in error.getvalue() and not invalid_out.exists())

        empty_src = root / "empty"
        empty_src.mkdir()
        (empty_src / "header-only.csv").write_text(header + "\n", encoding="utf-8")
        with redirect_stderr(io.StringIO()):
            exit_code = main(["--telemetry", str(empty_src), "--out", str(invalid_out)])
        check("仅表头的文件不计作会话", exit_code == 2 and not invalid_out.exists())

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

    metrics = []
    for path in files:
        try:
            session = read_session(path)
        except (OSError, ValueError) as exc:
            print(f"跳过 {path.name}: {exc}", file=sys.stderr)
            continue
        metrics.append(session_metrics(session))
    if not metrics:
        print(f"{src} 里没有可分析会话", file=sys.stderr)
        return 2

    findings = detect_indicators(metrics)
    coded = []
    if args.coded:
        try:
            coded = load_coded(Path(args.coded), {m["file"] for m in metrics})
            findings += coded_findings(coded, len(metrics))
        except (OSError, ValueError) as exc:
            print(f"人工编码无效：{exc}", file=sys.stderr)
            return 2
    findings = finalize(findings, len(metrics))

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
