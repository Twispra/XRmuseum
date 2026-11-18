# 会话指标与优先级（自动生成）

> 生成时间：2026-11-18T10:45:30+08:00　输入：`inputs`　会话数：**7**

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
| activate（扳机）总数 | 4 |
| select（抓握）总数 | 0 |
| 每次 hover 对应的 activate 数 | 0.133 |
| 首次交互中位数（秒） | 3.5 |
| 推进到 Welcome 以外页面的会话数 | 1 |

## 2. 逐场会话

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
| B2 | 指得到但按不下去 | telemetry | 3 | S | 2 | 0.286 | 0.858 |
| B3 | 在目标之间反复折返 | telemetry | 2 | S | 3 | 0.429 | 0.858 |
| B1 | 整场没有任何交互 | telemetry | 3 | S | 1 | 0.143 | 0.429 |

### 证据

**B2　指得到但按不下去**（关联 `EQ2 状态可辨 / EQ4 可发现性`）
- HelmetExhibit/HelmetHotspotTour/Front window Hotspot：1 场会话命中，单场最高 hover 4 次，累计 4 次，从未按下
- HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot：1 场会话命中，单场最高 hover 3 次，累计 3 次，从未按下
- Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet left：1 场会话命中，单场最高 hover 3 次，累计 3 次，从未按下
- 备注：行为层只能证明“指了没按”，原因（不知道要按、按错键、以为悬停即选中）需人工编码。

**B3　在目标之间反复折返**（关联 `EQ5 流程可理解`）
- 20261117-222652-MuseumVR.csv: 折返 2 次，序列 ['HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot']
- 20261117-224532-MuseumVR.csv: 折返 5 次，序列 ['HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'HelmetExhibit/HelmetHotspotTour/Bonnet shell Hotspot', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot']
- 20261117-225004-MuseumVR.csv: 折返 3 次，序列 ['HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet left', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Reset photo helmet', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet right', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet left', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot', 'Exhibit 01 - 1988 Photo Helmet Study/1988 Photo Helmet Display/Turn photo helmet left', 'HelmetExhibit/HelmetHotspotTour/Front window Hotspot']
- 备注：折返本身不是缺陷，可能是在比较；但它标记了“决策成本最高的位置”。

**B1　整场没有任何交互**（关联 `EQ4 可发现性`）
- 20261117-220903-MuseumVR.csv: 时长 59.425s，无任何交互事件
- 备注：可能是不知道从哪开始，也可能是测试环境问题；必须回看录像或访谈再定性。

## 4. 本工具不做的事

- 不做定性归因：行为层只能证明“指了几次、按了几次”，不能证明“为什么”。
- 不替代主持人编码：预设码 RD/ST/TM/LG/RS/NV 需人工判读后通过 `--coded` 传入。
- 不外推：样本量与抽样方式决定结论只能用于本轮改进优先级。
