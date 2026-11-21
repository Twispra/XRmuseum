# 会话分析：从遥测 CSV 到优先级 issue log

把 `SessionTelemetry`（PR #81）产出的会话 CSV，变成三样可直接进报告的东西：

| 文件 | 内容 |
| --- | --- |
| `summary.md` | 聚合指标 + 逐场表 + 指标与优先级 + 证据摘录（可直接粘进报告附录） |
| `issue-log.csv` | 每条指标的编号、严重度、成本、影响占比、优先级、证据、待填的决定 |
| `report.json` | 机器可读全量结果（含每个目标的 hover/activate/select 计数） |

## 怎么跑

```bash
python analyze_sessions.py --self-test                      # 自检用合成夹具
python analyze_sessions.py --telemetry <CSV目录> --out <输出目录>
python analyze_sessions.py --telemetry <目录> --coded coded-observations.csv --out <目录>
```

使用 Python 3.10 及以上版本和标准库。成功退出码为 0。没有有效会话或人工编码校验失败时退出 2，报告不会生成。跳过无法读取的遥测文件以及缺少事件记录、事件名或有效时间的文件时，终端会指出文件与原因。

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
| B1 | 会话未记录交互事件 | hover + activate + select = 0 | 3 | S |
| B2 | 反复悬停且未记录选择或激活 | 同一目标单场 hover ≥3，且未记录 activate/select | 3 | S |
| B3 | 在目标之间反复折返 | 合并相邻同目标事件后，目标序列中出现 ≥2 次 A→B→A | 2 | S |
| B4 | 求助次数偏高 | 会话曾观测到有效的非 Welcome 页，且求助计数 ≥2 | 2 | M |
| B5 | 操作探索按钮后未观测到预期换页 | 操作可明确换页的旧探索按钮后，在下一次按键、未知快照或场景边界前仍观测到原页，且观测窗口 ≥1 秒 | 4 | M |
| B6 | 首次交互明显滞后 | 进入场景到首次交互 > 20 秒 | 1 | S |

页面历史取整场已知快照，包括 `Welcome`、`Air`、`Voice`、`Hint`、`Review`。空值、`-` 和其他未识别名称表示未知。曾到达非 `Welcome` 页的会话包括之后返回 `Welcome` 的场次。

B5 检查旧探索面板 `Exploration AIR/VOICE/HINT/REVIEW` 的 `select`。当 `session_start.detail` 含 `legacy_exploration_input=2` 时，也检查这些按钮的 `activate`。缺少版本或版本未知时只检查 `select`。对支持两路输入的会话，相邻的两次按键若是同一路径、同一记录时间（精度为 0.001 秒）的 `select` 和 `activate`，按一次换页候选处理。其他按键或场景、会话边界会结束配对，原始事件与计数分别保留。候选的 `input` 标出收到的路径。

B5 要求起始页已知、与目标页不同，后续快照须在本次动作的观察窗口内。换到目标页会结束本次观察。默认实体站的 `Physical UI - Air/Voice` 在释放时确认，也可关闭当前演示。它们的 `select` 会列为候选，操作结果仍需结合快照与观察记录核查。B5 的严重度用于人工核查排序。

优先级 = **严重度 × 影响会话占比 ÷ 成本**（S=1、M=2、L=3），与
`docs/evaluation/README.md` §4 的公式一致。以原始计数计算并排序，最后显示至小数点后三位，同分时先按严重度，再按编号排。
严重度采用 Nielsen 0–4，与 `observation-and-coding.zh-CN.md` 一致。

### 导入人工编码

`session_id` 填本次成功读取的遥测 CSV 文件名（含 `.csv`）。同一码下同一会话的多条观察只计一次影响会话。
`severity` 必须为 0–4 的整数，同一码按最高值汇总。`cost` 使用 S/M/L，留空按 M，
同一码必须统一成本。多个需求会保留，决定冲突时由团队复核。

## 本轮产物

- `inputs/`：2026-11-17 晚组员自测的 7 场会话（**验证记录器用，不是评价数据**）
  `20261117-220903-MuseumVR.csv` 第 2 行 `session_start.detail` 的 `log` 值已替换为 `[redacted-local-path]`。对照脱敏前后的三个产物，除执行时间外完全一致。
- `summary.md` / `issue-log.csv` / `report.json`：由上述 7 场生成，保留这些未带新能力版本的历史输出
- `coded-observations-template.csv`：主持人编码表的列名与两行示例

## 已知边界

- hover ≥3、折返 ≥2、首交互 >20 秒和 B5 的 1 秒观察窗口都是预设阈值。
- XRI 的 hover、activate、select 记录交互事件。B1–B6 的解释需要结合主持人观察与会话上下文。
- 会话数少于 3 时，`affected_ratio` 的统计意义很弱，只能当作个案描述。
- 未验证：未在真实参与者数据上运行过（本仓库目前还没有这类数据）。
