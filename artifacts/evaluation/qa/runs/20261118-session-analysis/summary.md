# 会话指标与优先级（自动生成）

> 生成时间：2026-11-18T14:09:41+08:00　输入：`inputs`　会话数：**7**

> **本轮输入是 2026-11-17 晚组员自测的 7 场会话（验证记录器用），不是评价参与者数据，不得作为评价结论引用。**

> 复跑：`python analyze_sessions.py --telemetry <目录> --out <目录>`（加 `--self-test` 可先用合成夹具自检）。

## 1. 聚合指标

| 指标 | 值 |
| --- | --- |
| 会话数 | 7 |
| 总时长（秒） | 226.544 |
| 时长中位数（秒） | 24.308 |
| 有交互的会话数 | 6 |
| hover 总数 | 30 |
| activate 总数 | 4 |
| select 总数 | 0 |
| 每次 hover 对应的 activate 数 | 0.133 |
| 首次交互中位数（秒） | 3.5 |
| 曾观测到有效非 Welcome 页的会话数 | 0 |

## 2. 逐场会话

结束页以末条快照为准，`-` 表示未知。曾到达的页面按整场有效快照统计。

| 文件 | 时长(s) | hover | activate | select | 首交互(s) | 唯一目标 | 结束页 | hints |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `20261117-220903-MuseumVR.csv` | 59.425 | 0 | 0 | 0 | - | 0 | - | 0 |
| `20261117-222652-MuseumVR.csv` | 26.353 | 4 | 0 | 0 | 3.825 | 2 | Welcome | 0 |
| `20261117-222809-MuseumVR.csv` | 21.732 | 3 | 0 | 0 | 3.384 | 3 | Welcome | 0 |
| `20261117-224532-MuseumVR.csv` | 24.308 | 7 | 0 | 0 | 3.496 | 2 | Welcome | 0 |
| `20261117-225004-MuseumVR.csv` | 74.86 | 11 | 2 | 0 | 3.535 | 6 | Welcome | 0 |
| `20261117-225841-MuseumVR.csv` | 13.548 | 4 | 2 | 0 | 3.453 | 2 | Welcome | 0 |
| `20261117-230611-MuseumVR.csv` | 6.318 | 1 | 0 | 0 | 3.505 | 1 | Welcome | 0 |

## 3. 指标与优先级

优先级 = 严重度 × 影响会话占比 ÷ 成本（S=1，M=2，L=3）。
严重度为 Nielsen 0–4，与 `docs/evaluation/observation-and-coding.zh-CN.md` 一致。

| 编号 | 指标 | 来源 | 严重度 | 成本 | 影响会话 | 占比 | 优先级 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| B3 | 在目标之间反复折返 | telemetry | 2 | S | 4 | 0.571 | 1.143 |
| B2 | 反复悬停且未记录选择或激活 | telemetry | 3 | S | 2 | 0.286 | 0.857 |
| B1 | 会话未记录交互事件 | telemetry | 3 | S | 1 | 0.143 | 0.429 |

### 证据

**B3　在目标之间反复折返**（关联 `EQ5 流程可理解`）
- 20261117-222652-MuseumVR.csv: 折返 2 次，合并同目标后的序列 ['HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot']
- 20261117-224532-MuseumVR.csv: 折返 5 次，合并同目标后的序列 ['HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot']
- 20261117-225004-MuseumVR.csv: 折返 4 次，合并同目标后的序列 ['HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet left', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Reset photo helmet', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet right', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet left', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet left', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot']
- 20261117-225841-MuseumVR.csv: 折返 2 次，合并同目标后的序列 ['HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot']
- 备注：回看场次以区分正常比较与操作困难。

**B2　反复悬停且未记录选择或激活**（关联 `EQ2 状态可辨 / EQ4 可发现性`）
- HelmetExhibit/HelmetHotspotTour/Front window Hotspot：1 场会话命中，单场最高 hover 4 次，累计 4 次，未记录 activate 或 select
- HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot：1 场会话命中，单场最高 hover 3 次，累计 3 次，未记录 activate 或 select
- Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet left：1 场会话命中，单场最高 hover 3 次，累计 3 次，未记录 activate 或 select
- 备注：请结合观察记录确认是否尝试操作，以及使用的输入是否触发了 XRI 事件。

**B1　会话未记录交互事件**（关联 `EQ4 可发现性`）
- 20261117-220903-MuseumVR.csv: 时长 59.425s，无任何交互事件
- 备注：可能是不知道从哪开始，也可能是测试环境问题；必须回看录像或访谈再定性。

## 4. 本工具不做的事

- 不做定性归因：行为层只能证明“指了几次、按了几次”，不能证明“为什么”。
- 不替代主持人编码：预设码 RD/ST/TM/LG/RS/NV 需人工判读后通过 `--coded` 传入。
- 不外推：样本量与抽样方式决定结论只能用于本轮改进优先级。
