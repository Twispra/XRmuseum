# 2026-09-23 潜水头盔原型活动与证据

记录日期：2026-09-23（Asia/Shanghai）。这是本轮开发事实及后续阶段材料的证据索引，不是正式 Phase 2 全部交付，也不是任何成员的个人反思短文。

后续 PR 审查发现的初始化覆盖、验收状态残留及其修复迁移，另记于[PR #1 修复记录](2026-09-23-pr1-review-fixes.md)；下面保留最初原型实现与 89 项检查的历史事实。

## 责任与工作归属

- 用户提出制作真实尺度灰模和一个精修前窗样件，随后确认先接入 Unity 抓取、拿放和复位。
- 当前仓库提交账号为 `Twispra`，本地 Git 作者为 `twispark`。账号标记提交责任，不据此认定其中每项实现由学生独立完成；本记录未添加姓名、学号、工时或其他成员分工。
- Codex 辅助完成建模脚本、几何修复、导出、Unity 集成及验证脚本，并整理本记录；Blender MCP 是工具通道。生成内容、技术判断与自动初检的具体范围见下文及[资源声明](../external-resources.md)。
- 独立人工审查、真实头显验收和参与者评价尚未记录；成员应在本人审查、修改或实测后，以自己的账号补充相应证据。

## 今日完成与证据

| 工作 | 结果 | 可检查文件 |
| --- | --- | --- |
| 真实尺度简模与独立前窗样件 | v02 灰模宽 317.505 × 深 419.101 × 高 495.312 mm；窗件独立，未装配成完整精修头盔 | [Blender 源文件](../../artifacts/helmet-prototype/helmet-study-v02.blend)、[v01 保留版本](../../artifacts/helmet-prototype/helmet-study-v01.blend)、[建模脚本](../../artifacts/helmet-prototype/scripts/)、[预览](../../artifacts/helmet-prototype/README.zh-CN.md) |
| 几何修复与导出 | 修正 8 个资产网格反向法线、清理壳体碎面；两份 FBX 回读后尺寸／网格数量一致，窗件保留铰链层级 | [修复记录](../../artifacts/helmet-prototype/qa/repair-v02.json)、[网格审计](../../artifacts/helmet-prototype/qa/audit-helmet-study-v02.json)、[导出验证](../../artifacts/helmet-prototype/qa/export-verification-v02.json) |
| Unity 展品原型 | 中央方块替换为头盔；4 个简化碰撞体、重力、单手动态握点、旋转拿放、Home／实体 RESET／掉落复位 | [场景](../../MuseumVR/Assets/Museum/Scenes/MuseumStart.unity)、[Prefab](../../MuseumVR/Assets/Museum/Prefabs/HelmetExhibit.prefab)、[复位脚本](../../MuseumVR/Assets/Museum/Scripts/HelmetExhibitReset.cs) |
| 自动技术验证 | 89 项检查通过，十次连续拿放，期间零自动复位；最大放回偏移约 0.069 mm；抓取中／自由状态 Home、再次抓取、实体按钮、掉落恢复通过 | [最终报告](../../artifacts/helmet-interaction/qa/playmode-validation.json)、[验证脚本](../../MuseumVR/Assets/Museum/Editor/HelmetPrototypeValidation.cs)、[环境说明](../../artifacts/helmet-interaction/qa/README.zh-CN.md) |
| 视觉与说明 | 导出 Unity 静态预览；发现文字超出牌面后改为自动适配，重新渲染检查 | [场景近景](../../artifacts/helmet-interaction/preview/02-helmet-and-reset.jpg)、[渲染记录](../../artifacts/helmet-interaction/preview/capture.json)、[操作说明](../helmet-interaction.zh-CN.md) |

Unity 为 2022.3.62f3c1，XRI 2.6.4、URP 14.0.12。验证由合成 XR 设备事件驱动现有输入动作和交互器，不直接调用选择 API 代替控制器输入。报告的数值仅描述该固定模拟任务，不是现实手部精度、可用性或设备性能结论。未发现运行阶段 Error/Exception；无头显时的 OpenXR 启动提示已记录在说明中。

最后一次交互验收后只调整了说明牌文字自适应，并重新导出预览；物理和抓取配置未再修改。本次未生成新的可执行构建，9 月 22 日初始化构建不能代表本轮头盔版本。

## 需求、实现与决策

以下来源为课程 202627A 官方文件；“团队实现解释”不代表客户指定了该技术方案。

| 官方依据 | 本轮实现解释和取舍 | 当前范围 |
| --- | --- | --- |
| Scenario p2：让难以接触的对象可被有目的地探索；p3 将潜水头盔列为可选示例 | 选用 Mark V-inspired 头盔，先验证真实尺度与单物体拿放，延后完整精修 | 尺度和操作片段可测；学习目标、叙事环境与完整访客流程尚未完成 |
| Scenario p2–3：工作人员能可靠地启动、监督及复位 | Home、展台 RESET 和掉落恢复；复位时清除速度，防止立即回吸 | 模拟输入已测；真实工作人员使用及控制器射线操作仍需实测 |
| Scenario p3–4：区分历史证据与重建解释，并说明模型来源和精度 | 公开实物外包尺寸作锚点；局部几何、材质磨损及机械间隙标为假设；展牌标记 SCALE PROTOTYPE | 并非扫描或馆方确认复刻；[来源与假设](../helmet-prototype-reference.md) |
| Description p6：主要 XR 行为、基本说明、错误处理及已实现／计划边界 | 复用官方 XRI 输入与抓取；采用 1.5 kg 调试质量和简化碰撞体；先测试再接手模型与窗件交互 | 质量不是历史实物重量；精修前窗开合仅在 Blender 层级验证 |
| Scenario p2、p4：从开始考虑包容性和舒适性 | 本轮只建立技术基础，未将自由抓取当作已满足无障碍需求 | 替代操作、可达范围、坐姿／语言支持、舒适度需后续设计和评价 |

## 问题、处理与尚未完成项

- 原任务中断后从磁盘文件恢复，保留 v01 并另存 v02；没有声称已修复底层连接故障。
- 无画面批处理曾以数千帧每秒运行，触发 XRI 的小时间步投掷采样边界；测试改为约 90 Hz，并断言每轮没有自动复位，避免掩盖异常。
- 后台虚拟键盘被禁用，导致初期 Home 测试失败；在测试环境显式启用设备并于玩家输入阶段排队事件后复测通过。没有用关闭产品投掷或放宽标准替代修正。
- 尚未完成：真实头显／手柄、官方模拟器全套键鼠导航人工验收，手模型和手指姿态、双手承托、Unity 前窗开合、UV／烘焙／LOD、设备性能与完整博物馆学习流程。
- 未开展参与者研究，没有用户访谈、学习效果或舒适性结论。本轮自动检查不算 Phase 3 用户评估。
- 提交前工作区已有 `ProjectSettings.asset` 的 `preloadedAssets` 清空改动，本轮没有产生或修改它，也未纳入本次提交。技术报告来自该本地工作区；这是报告与仓库基础设置之间已知的环境差异，不宣称对一个全新克隆做过验证。

下一步先进行真实设备验收，明确核心受众与学习目标，再按结果安排前窗、手模型及性能优化；后续活动由实际执行成员补充记录，不预填责任或用户反馈。

## 官方记录要求与提交边界

| 文件与页码 | 本记录如何对应 |
| --- | --- |
| Group Project Description p6、p12 | 写明责任、决策、完成工作和未解决问题，持续提交可追溯源码及必要资产 |
| Description p3、p7；P2 Rubric p3 | 提供实际提交及文件证据，不以提交数量代替贡献质量，不把 AI 生成内容归为学生独立成果 |
| Description p6；P2 Rubric p2 | 需求映射、设计取舍、验证与限制分开；注明本轮无新构建、无实机验收 |
| Description p12 | 来源、已知许可、用途、修改范围及 AI 辅助记入[资源清单](../external-resources.md) |
| Description p13 | 个人反思由成员本人撰写；不由本记录代写，也不代提交 Canvas |

本地核对过六份官方 PDF。课程原件和“假设”方案留在本地 `project-resource/`，后者未作为官方事实或真实贡献证据。具体 Canvas 命名、截止时间、教学团队仓库访问和 AI 政策更新尚未核验，仍须在正式阶段提交前核对。新建分支和 PR 是本仓库的协作约定，不是课程对今日提交的硬性规定。

## 版本证据

- [实现与资产提交 e7d819e](https://github.com/Twispra/XRmuseum/commit/e7d819e4d3aea1a0bb439385785a5606f0d7acc0)：Blender 源文件、修复与导出、Unity 交互、自动验证及使用说明。
- 工作分支：`codex/helmet-prototype-2026-09-23`；活动记录与外部资源声明作为随后单独的文档提交保存，以引用上面的固定成果版本。

- [PR #1：潜水头盔交互原型及当日课程活动记录](https://github.com/Twispra/XRmuseum/pull/1)：审阅本轮实现和记录。创建时尚未合并；后续合并状态以 GitHub 为准。独立人工验收仍单独记录，不能由自动提交代替。
