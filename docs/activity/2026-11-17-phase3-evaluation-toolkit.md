# 2026-11-17：Phase 3 评价工具包（可访问性专项）

用户明确授权在本仓库建立 Phase 3 的评价方案与工具。本轮**只做工具与方案，没有执行任何评价会话**，
因此本记录**不含观察数据、不含结论**。参与者尚未到位，工具先行。

## 为什么现在做

课程 Phase 3 的评分标准（*Phase 3 Assessment Rubric*）把这项作业定义为
「evaluation, analysis, prioritisation, improved build and verification **as one evidence-to-action cycle**」，
组内 8 分中 3 分给"定性评价设计与执行"、2 分给"分析与证据质量"；
个人 4 分中 1 分明确要求"**named artefacts** demonstrate a substantial contribution to planning,
facilitation, observation, analysis or synthesis"。

也就是说：没有成形的评价工具与署名记录，组内那 5 分和个人那 1 分都无从落笔。
而 `docs/project-state.md` 记录"可访问性、用户学习评价…均未获得可核验证据"，
仓库此前也没有任何 Phase 3 评价产物 —— 这一层是空的。

选择可访问性作为评价专项，因为 Scenario B 本身是 *Accessible Museum Experience*，
可访问性属于"重要需求"而非附加项；且已有可直接检验的具体假设（见下），不需要从零猜问题。

## 本篇交付

新增 `docs/evaluation/`：

| 文件 | 作用 | 对应评分项 |
| --- | --- | --- |
| `README.md` | 评价问题（EQ1–EQ5）、方法、参与者理由、60 分钟流程、优先级公式、评分对应表、分工登记 | 组内 3 分、个人 1 分 |
| `consent-and-information.md` | 参与者信息页、口头同意六条、记录模板、主持人纪律 | 组内 3 分（伦理） |
| `moderator-script.zh-CN.md` | 开场/热身/任务/收尾逐句脚本、禁用句式、会话后即时备忘 | 组内 3 分（一致性） |
| `task-list.zh-CN.md` | T1–T5 任务、完成判据、提示分级、6 个收尾访谈问题 | 组内 3 分 |
| `observation-and-coding.zh-CN.md` | 观察表、预设码+归纳码、Nielsen 0–4 严重度、issue log 与优先级、匿名摘录规范 | 组内 2 分 |

**未改动任何 `MuseumVR/` 产品文件、场景、Prefab、字体、Packages 或 ProjectSettings。**

## 评价问题从哪来（可追溯）

EQ1–EQ3 来自 2026-10-11 合入的[无障碍与舒适性走查](../accessibility.zh-CN.md)及后续实现：

- 21 个 TMP 组件 `fontSizeMin ≤ 0.05`（20 个为 `0.02`），最大压缩比 **72.5×** → EQ1 可读性
- 现行热点用外环表示悬停，用勾形表示已访问，二者可同时出现 → EQ2 状态可辨。见[状态提示验证](2026-10-13-hotspot-state-accessibility.md)。
- `explanationHoldSeconds = 30`、`explanationFadeSeconds = 1`，重选可重启 → EQ3 时间压力

EQ4（语言入口可发现性）覆盖入口语言板和中央展台设置。EQ5 评估当前头盔的朝向与观察状态复位。
主持人在下一场前重新加载并核对起始状态。

> 注意：以上都是**资产层读数**，不是用户证据。所以它们只能用来**提出问题**，
> 不能用来当作"访客确实读不清"的结论 —— 这正是要做真人评价的原因。

## 未执行与限制

- **零场次**：层次 A（专家走查）与层次 B（试点 think-aloud）均未执行，无参与者。
- 本目录所有模板中的示例行（如 issue log 的 I-01）是**格式示例，不是真实数据**，引用时必须写清。
- 未验证：任何真机可读性、舒适性、可达性结论；也未验证该方案在 PQ604A/B/C 场地与设备上的可行性。
- 本轮未运行 Unity，未构建 Player。

2026-11-18 按 PR #78 的复审意见，以 `main@9e9f989` 核对热点状态和展台复位实现。T5 判据、场间初始化与可选录制授权已在工具包对应页修订，验证范围为文档及其依据。

## 后续

1. 组内 2 人先做层次 A 各一遍（每人约 40 分钟，无需外部参与者），把结果填进观察表 —— 这是**最低成本的启动动作**。
2. 约 3 名非项目组参与者做层次 B；每场 60 分钟，按 `moderator-script` 执行。
3. 合并 issue log 并按优先级公式排序，据此另开产品改动任务（届时按协作规则协调共享文件写入者）。
4. 分工登记表由各人**填自己实际做过的格子**，未做的不填。
