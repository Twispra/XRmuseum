# 外部资源与辅助工具记录

2026-10-04 中英语言切换模块：助手辅助实现界面语言切换系统与文档，见[模块说明](localization.zh-CN.md)与[活动记录](activity/2026-10-04-localization.md)。

**新增外部资源：Noto Sans SC 字体。** 来源为本机已安装的 `C:\Windows\Fonts\NotoSansSC-VF.ttf`（Google 的 Noto Sans SC），
许可 **SIL Open Font License 1.1**，允许自由使用、修改与再分发；该字体没有 Reserved Font Name，改名与修改后分发均被允许。
用途是界面中文显示 —— 项目原有字体只有 LiberationSans，**没有任何中文字形**。

提交进仓库的是**子集版本** `NotoSansSC-MuseumSubset.ttf`（2.32 MB）：先用 fontTools 从变量字体的 `wght` 轴取出静态
Regular 实例，再子集化到 ASCII + GB2312 全集 + 常用标点，共 7545 个码位。**原始 16.95 MB 的变量字体不提交仓库**。
同类候选还有 `NotoSerifSC-VF.ttf`（同许可）；`msyh.ttc` / `simhei.ttf` / `simsun.ttc` 为微软专有，**未采用**。

TMP 字体资产（`NotoSansSC-MuseumSubset SDF.asset`）由 Unity 的 Font Asset Creator 生成，
参数 Sampling 110 / Padding 6 / Atlas 2048×2048 / SDFAA / Character Set = `Characters from File`，
共 190 个字形。生成步骤与原因见 `Assets/Museum/Features/Localization/Fonts/README.md`。

AI 辅助范围：模块架构、Editor 工具、运行时脚本、字体子集化脚本、实机问题排查与文档。
**未**新增下载模型、贴图或音频，**未**复制任何第三方素材。编辑器内预览与自动检查**不等于**真实头显测试、
学习效果评价或组员审批。

2026-10-03 年代切换模块：Codex 辅助实现头盔「历史年代切换」系统及文档，见[模块说明](helmet-eras.zh-CN.md)与[活动记录](activity/2026-10-03-helmet-eras.md)。三个年代的年代与结构描述沿用[模型依据](helmet-prototype-reference.md)中已记录的公开来源口径（DESCO、U.S. Naval Undersea Museum、Royal Museums Greenwich 等），**没有新增下载资产**。9 套材质是程序生成的纯色占位材质，**不是历史外观复原**，未使用任何第三方贴图或模型，也未复制馆藏媒体。AI 辅助范围包括架构设计、Editor 工具、运行时脚本、实机问题排查与文档；编辑器内预览与自动检查**不等于**真实头显测试、用户研究或组员审批。

2026-10-03 信息安排调研：参考 [V&A 展签写作指南](https://www.vam.ac.uk/blog/wp-content/uploads/VA_Gallery-Text-Writing-Guidelines_online_Web.pdf)、[NPS 展板写作](https://www.nps.gov/subjects/hfc/wayside-exhibit-writing.htm)、[NN/G 渐进式展示](https://www.nngroup.com/articles/progressive-disclosure/)与 [Apple Ornaments](https://developer.apple.com/design/human-interface-guidelines/ornaments)。仅引用组织原则与链接，未导入媒体、字体或UI资产；按钮分组、文案和布局是本项目设计，未把实体展板或 visionOS 尺寸套成 VR 标准。Codex 辅助文案、面板状态、限定场景更新与验证，见[信息安排方案](exhibit-information.zh-CN.md)。

2026-10-03 模型动画补充：按用户反馈将供气/通话简图扩展为设备模型与动画。类型依据为 [RMG 手动空气泵 EQA0617](https://www.rmg.co.uk/collections/objects/rmgc-object-256836)、[SMG 双缸潜水泵 1963-71](https://collection.sciencemuseumgroup.org.uk/objects/co41152/two-cylinder-double-acting-divers-air-pump)和 [RMG 潜水电话箱 ZBA6671](https://www.rmg.co.uk/collections/objects/rmgc-object-561120)。以代码构建原创简化几何，未复制馆藏图片或第三方模型；这些不同馆藏不是已确认的 Mark V 配套型号。转轮、活塞、可见气流、信号波形、缩放及时序均为教学表达参数，不是测量数据或精确机械/电气复原。代码、资料核对、渲染与模拟测试由 Codex 辅助，详见[本轮记录](activity/2026-10-03-exploration-model-animation.md)。

2026-10-03 自由探索补充：Codex 辅助实现同一观察面板的可选供气/通话探索、提示、回顾、示意动画、XR 模拟测试及文档，见[功能说明与来源](helmet-exploration.zh-CN.md)和[活动记录](activity/2026-10-03-helmet-exploration.md)。重新核验 U.S. Naval Undersea Museum 的 MK V Diving Rig 页面及 DESCO 的 Mark V 产品正文，仅用于解释水面供气与电话连接，没有导入页面照片、下载模型或音频。现有接头为简化灰模，通话是概念图；动画速度、时长、布局与交互大小为设计参数，没有冒充历史测量数据。自动测试和 AI 渲染检查不等于真实头显测试、用户研究或组员审批。

2026-10-02 展厅环境补充：本次根据 [BIG 的 Danish Maritime Museum](https://big.dk/projects/danish-maritime-museum-1699)、[Kossmanndejong 的 Maritime Stories of Denmark](https://kossmanndejong.nl/project/maritime-museum-denmark/) 和 [Royal Museums Greenwich 的 Sea Things](https://www.rmg.co.uk/national-maritime-museum/attractions/sea-things-gallery) 研究空间组织与展陈方法。仅使用来源链接与简短概括，没有下载或分发其图片、视频、模型、贴图，也不推断这些媒体可再利用。新环境的建筑几何、抽象图形、解释性装置和材质由 Codex 辅助程序生成；文字继续使用项目现有 TMP 字体。AI 辅助范围包括设计、Editor 工具、场景接入、测试和文档；不等于人工审查、真实用户评价或头显性能验收。侧面装置不是历史复原，后墙线纹不是测量数据，见[环境说明](maritime-gallery.zh-CN.md)。

| 资源 | 来源 | 用途 | 许可/使用条件 | 修改情况 |
| --- | --- | --- | --- | --- |
| Unity 官方 3D 模板 | Unity Hub / 已安装编辑器的 ProjectTemplates | 初始工程 | 遵循模板随附许可；提交前核对课程要求 | 初始工程基础，不能作为小组原创功能申报 |
| Unity XR 官方 packages 及样例 | Unity Package Manager，XR Interaction Toolkit 2.6.4 | Starter Assets 的 XR Origin、输入映射与交互器；XR Device Simulator 的键鼠模拟 | Unity Companion License，见 licenses/xr-interaction-toolkit.md | 完整导入两个 sample 并保留 .meta；在 MuseumStart 内设置 XR Origin 位置、相机参数；其余版本见 manifest.json |
| TextMesh Pro 3.0.7 必需资源 | Unity Package Manager / TMP Essential Resources | 展厅文字、模拟器 UI、Liberation Sans 字体 | Unity Companion License；字体许可保留于 Assets/TextMesh Pro/Fonts | 只导入必需资源，没有导入 Examples & Extras |
| Unity .gitignore 规则参考 | https://github.com/github/gitignore/blob/main/Unity.gitignore | 排除 Unity 缓存及本地生成文件 | 以来源仓库的 LICENSE 为准 | 调整为支持仓库下的 MuseumVR 子工程 |
| Codex 辅助 | 本次项目初始化对话 | 创建独立工程、XR/URP 配置、占位展厅、测试方块、版本控制配置及协作说明 | 按课程/学校 Canvas 上的 AI 使用政策声明 | 初始化内容需要人工复核；不替成员撰写个人贡献短文 |
| DESCO Mark V 产品说明与照片 | https://www.divedesco.com/P/162/MarkVDivingHelmet | 2026-09-23 头盔轮廓、四窗、铰链、锁扣与材质观察 | 未确认照片的再分发许可；仅在本机研究，仓库保留来源链接，不分发照片或用作贴图 | 根据观察构建程序几何，并非扫描、描摹贴图或制造图纸复刻 |
| U.S. Naval Undersea Museum：MK V Diving Rig | https://navalunderseamuseum.org/mk-v/ | 区分标准空气型与混合气型，观察头盔／胸板及连接件关系 | 本轮仅引用来源与概括事实；未复制页面媒体，未对媒体作许可推断 | 用于参考和内容边界，不直接导入资产 |
| Case Auctions，Lot 636 | https://caseantiques.com/item/lot-636-us-navy-wwii-mark-v-dive-helmet/ | 使用该件实物公开外包尺寸作为灰模尺度锚点 | 仅记录公开尺寸及来源；未复制页面媒体 | 英寸换算为米；局部形状与厚度属于原型假设 |
| Hampton Roads Naval Museum：Mark V Diving Helmet | https://hamptonroadsnavalmuseum.blogspot.com/2023/02/in-collection-mark-v-diving-helmet.html | 交叉检查尺寸数量级与测量边界 | 仅引用来源与概括事实；未复制页面媒体 | 不与不同实物的尺寸混合或取平均 |
| Blender 5.2 / Blender Python API | 本机 Blender；https://www.blender.org/ | 建模、程序材质、渲染、网格审计和 FBX 导出回读 | 工具许可与生成资产的权利分开；未将 Blender 程序分发进项目 | 使用工具执行本项目脚本；未修改 Blender 本体 |
| Blender MCP，mcp-for-blender 2.0.0 | 安装包元数据指向 https://github.com/ahujasid/blender-mcp | Codex 与本机 Blender 通信，执行建模脚本 | 此次未核实安装包的具体许可证文本；未复制或分发插件代码 | 只调用现有插件；长渲染／验证通过独立 Blender 进程完成 |
| Codex 辅助：头盔原型及本次记录 | 2026-09-23 用户发起的本地开发任务；[范围与证据](activity/2026-09-23-helmet-prototype.md) | 几何与材质脚本、法线和碎面修复、导出、Unity 配置／复位／验证脚本、技术与活动记录 | 按课程及大学 Canvas 政策声明；尚未核验 Canvas 最新政策，不声称已满足其全部条款 | 明确为 AI 辅助生成和修改；自动检查与渲染初检由 Codex 执行，未获得独立人工实机验收；不代写个人反思短文 |
| Codex 辅助：六人协作与工具保护 | 2026-09-23 用户要求检查多人 AI 协作；[活动与验证](activity/2026-09-23-team-collaboration.md) | 仓库规则、当前状态、课程索引、任务/PR模板、初始化覆盖保护、验收版本记录与独立输出目录 | 同上，须遵守并核对 Canvas 政策；未声称六人已共同批准机制 | 由 Codex 辅助设计、实现和自动检查；团队身份/分工、人工审批和真实设备评价不由 AI 虚构 |
| OpenAI / GitHub 官方操作文档 | [Codex AGENTS](https://learn.chatgpt.com/docs/agent-configuration/agents-md)、[GitHub Issue 与 PR 模板](https://docs.github.com/en/communities/using-templates-to-encourage-useful-issues-and-pull-requests/about-issue-and-pull-request-templates) | 核对规则发现方式与模板生效位置 | 仅概括操作规则并链接来源；未复制页面媒体或代码资产 | 据本项目调整为简短根规则、一个活动任务入口和PR交接 |
| Codex 辅助：前窗开合与半剖面观察 | 2026-09-29 用户授权的本地实现及主展厅接入；[交接说明](helmet-inspection-handoff.zh-CN.md) | 功能 Prefab、铰链操作、剖面着色器、开发演示场景与自动验证；将功能接入原 MuseumStart/XR 流程 | 沿用课程 AI 辅助声明要求；没有新增下载模型、图片或音频 | 沿用仓库灰模并重组前窗，新增材质与代码；95° 等交互参数为原型假设，非历史实测。独立精修前窗样件没有接入。真实设备与用户评价尚待成员完成 |

2026-09-29 后续 AI 辅助范围：按用户近距离试用反馈实现局部剖切平面、相对手柄操作、悬停物理、可跟随且避开静态障碍的面板，并扩展原主场景验收。沿用既有灰模和官方 XRI；没有下载新资产、虚构内部构件或把自动测试写成人工设备测试。见[本轮记录](activity/2026-09-29-advanced-inspection.md)。

后续添加模型、贴图、音效、字体、脚本或插件时，应在这里记录来源、许可、用途和修改内容。

2026-09-30 AI 辅助范围：根据 [PR #3 的实测评论](https://github.com/Twispra/XRmuseum/pull/3#discussion_r4143841614)复现并修复剖切球柄方向跳变，补充实际 Prefab 几何扫描和原主场景模拟 XR 输入测试，整理交接证据。没有引入新外部素材或 package；AI 代码检查及自动测试不等于组员审批或真实头显测试。见[处理记录](activity/2026-09-30-section-continuity-review.md)。

记录依据为 Group Project Description（202627A）p12；本表记录已知许可或尚未核实之处，不赋予第三方内容新的再分发许可。头盔研究副本和课程官方 PDF 留在本地 `project-resource/`、`artifacts/helmet-prototype/references/`，不作为本组原创资产提交。今日尚无新增音效、扫描资产、下载模型或烘焙贴图。
