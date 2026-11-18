# 会话分析：从遥测 CSV 到优先级 issue log

把 `SessionTelemetry`（PR #81）产出的会话 CSV，变成三样可直接进报告的东西：

| 文件 | 内容 |
| --- | --- |
| `summary.md` | 聚合指标 + 逐场表 + 指标与优先级 + 证据摘录（可直接粘进报告附录） |
| `issue-log.csv` | 每条指标的编号、严重度、成本、影响占比、优先级、证据、待填的决定 |
| `report.json` | 机器可读全量结果（含每个目标的 hover/activate/select 计数） |

## 怎么跑

```bash
python analyze_sessions.py --self-test                      # 先自检（13 项断言，不读外部数据）
python analyze_sessions.py --telemetry <CSV目录> --out <输出目录>
python analyze_sessions.py --telemetry <目录> --coded coded-observations.csv --out <目录>
```

纯标准库，无需安装任何依赖。退出码 0 = 成功。

## 两层，刻意不混

| 层 | 来源 | 产出 | 能不能自动 |
| --- | --- | --- | --- |
| 行为层 | 遥测 CSV | **B1–B6** 行为指标 | ✅ 全自动 |
| 定性层 | 主持人按 `docs/evaluation/observation-and-coding.zh-CN.md` 编码 | **RD/ST/TM/LG/RS/NV**（+ `X*` 归纳码） | ❌ 必须人工 |

两侧在 `issue-log.csv` 里用 `source` 列区分（`telemetry` / `coded`）。
**工具不对行为做归因**：它只能证明"指了 4 次没按下"，不能证明"因为以为悬停即选中"。
定性原因必须来自主持人的编码或访谈，否则就是编造。

## 行为指标定义

| 编号 | 指标 | 触发规则 | 严重度 | 成本 |
| --- | --- | --- | --- | --- |
| B1 | 整场没有任何交互 | hover + activate + select = 0 | 3 | S |
| B2 | 指得到但按不下去 | 同一目标单场 hover ≥3，且从未 activate/select | 3 | S |
| B3 | 在目标之间反复折返 | 交互序列中出现 ≥2 次 A→B→A | 2 | S |
| B4 | 求助次数偏高 | 求助计数 ≥2（仅对进入探索流程的会话判定） | 2 | M |
| B5 | 面板按钮按了但流程没推进 | 对 `*Button` 有动作，且结束页 = 开始页 | 4 | M |
| B6 | 首次交互明显滞后 | 进入场景到首次交互 > 20 秒 | 1 | S |

优先级 = **严重度 × 影响会话占比 ÷ 成本**（S=1、M=2、L=3），与
`docs/evaluation/README.md` §4 的公式一致；同分时按严重度、再按编号排。
严重度沿用 Nielsen 0–4，与 `observation-and-coding.zh-CN.md` 一致。

## 本轮产物

- `inputs/`：2026-11-17 晚组员自测的 7 场会话（**验证记录器用，不是评价数据**）
- `summary.md` / `issue-log.csv` / `report.json`：由上述 7 场生成
- `coded-observations-template.csv`：主持人编码表的列名与两行示例

## 已知边界

- 阈值（hover ≥3、折返 ≥2、首交互 >20s）是**先验设定**，不是从数据里拟合出来的；
  若要调整，改脚本顶部常量，并在报告里说明改动理由。
- 折返不等于缺陷，可能是在比较两个选项；它标记的是"决策成本最高的位置"，需人工定性。
- 会话数少于 3 时，`affected_ratio` 的统计意义很弱，只能当作个案描述。
- 未验证：未在真实参与者数据上运行过（本仓库目前还没有这类数据）。
