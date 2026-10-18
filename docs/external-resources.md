# 外部资源与辅助工具记录

## 2026-10-18：Phase 2 原型设计与技术说明

Codex 依据本机课程原文、`6088c736` 已提交工程、活动／PR／构建／验证证据，辅助编写英文需求映射、设计与技术决策、证据边界、限制和后续评估重点。报告正文四页、参考文献一页；原课程文件不上传，2026-10-16 中文评审稿仅增加历史／新版导航，原文保留。没有新增用户研究、成员反思、设备结果或完成声明，也没有修改产品来迎合报告。

文档使用 bundled Python／python-docx、lxml、pypdf、pdfplumber，Microsoft Word 原生 PDF 导出，以及 bundled 文档渲染器／Poppler；采用本机 Times New Roman，未新增 Unity 字体、模型、贴图、包或运行时依赖。所有可见文字至少 12 pt，A4 四边 2.5 cm，固定版本证据链接可解析，最终全部页面经目视或字节相同页面复核。详见[编写与验收记录](activity/2026-10-18-phase2-prototype-technical-note.md)。

## 2026-10-18：Phase 2 成员登记与小组活动记录

用户提供六份个人文案文件名作为姓名、学号与账号依据，以及本地课程 Activity Record DOCX 模板。Codex 辅助核对 GitHub 公开状态、提交／PR／活动证据，登记 README，并按模板编排约一页的英文小组活动记录；没有新增或代写个人经历、工时、会议或设备／用户研究结果。原始课程模板与六篇个人文案保持不变；仓库仅保存完成后的活动记录 PDF，不上传原始模板、批注或个人文案。

文档使用本机 Microsoft Word 导出，保留模板 A4、Times New Roman 12 号及三张表格结构；中文姓名采用本机 Microsoft YaHei 回退。使用 Codex bundled Python／Node、lxml、pypdf、pdfplumber、Poppler 与文档渲染器做来源、排版、链接及保留检查；未向 Unity 增加字体、资源或运行时依赖。具体范围与证据见[本轮活动](activity/2026-10-18-phase2-readme-activity-record.md)。

## 2026-10-18：#60 1839 早期头盔外形研究

本轮以 [Science Museum Group Collection 1881-47](https://collection.sciencemuseumgroup.org.uk/objects/co40614/first-closed-diving-helmet-and-breastplate) 为唯一馆藏原型。馆藏条目记载 1839 年、Siebe／Deane 关联、铜合金与玻璃等材料、整体 465 × 460 × 417 mm 和 11.5 kg；记录没有标出三轴对应关系，也未提供可核验的背面、铰链或接头尺寸。模型外轮廓参照页面上的一张照片；照片标注为 CC BY-NC-SA 4.0，本仓库**未复制、上传或贴图使用原照片**。馆藏事实和模型估算分别记录在[本轮活动](activity/2026-10-18-early-helmet-1881-47.md)与几何清单中。

Codex 辅助编写可编辑的 Python 几何定义、生成 OBJ／GLB、Unity 索引 0 绑定和三语说明。建模使用本地已有 Python、NumPy、trimesh；预览使用 Pillow，均为构建工具，不进入 Unity 运行时；没有增加 Unity package 或第三方模型。虚拟前面板、剖面及 AIR／VOICE 教学锚点是沿用现有原型交互的解释性设计，不主张其存在于 1839 原物。自动检查与 AI 复核不能替代组员史料审校、真实头显测试或用户研究。

## 2026-10-16：#57 新访客、年代说明与技术文档

Codex 辅助实现同一展品的新访客发现记录清理、年代展签／按钮三语说明、独立验收与证据复审，并更新当前技术文档；见[本轮活动](activity/2026-10-16-visitor-flow.md)。原模型、Unity／XRI 和运行时依赖保持；源 TTF 沿用已记录的 Noto Sans SC OFL 来源及 fontTools 4.60.1 Regular 实例化／子集流程，只补「佔」并保留原码位，保存 SDF 原身份补烤。没有新增模型、图像、录音或第三方运行时包。技术说明 PDF 使用本机已有 ReportLab／Noto 字体排版，Poppler 渲染、pypdf 计页；这些为文档工具，不进入 Unity 运行时。桌面自动输入、AI 复审和排版检查不代表成员人工批准、真实设备或用户研究通过。

## 2026-10-16：#56 观察验收退出修复

Codex 辅助对比既有 Editor 验收入口、修复退出等待及脚本重载状态保留、补足同侧退手夹具、独立只读复审并复跑，见[本轮活动](activity/2026-10-16-pr56-editor-exit-and-merge.md)。沿用现有 Unity / XRI 和全部资产，没有新增外部素材、包或运行时依赖；未修改生产碰撞规则或行为断言。最终两轮 207 项与自然退出 0 属于桌面合成输入结果，不代表真实设备、用户研究或另一位成员人工批准；未取得原生关闭调用栈。

## 2026-10-16：#50 普通壳体近距抓取修复

Codex 辅助复现物理手宽悬停导致本体/窗口均不可选的原因、修复整盔选择优先级、完善真实输入退手夹具并运行独立回归，见[本轮活动](activity/2026-10-16-issue50-shell-grab.md)。沿用现有 Unity 2022.3.62f3c1 / XRI、手套和全部资产，没有新增外部模型、媒体、字体、包或运行时依赖。掌面接触仍使用原包络、距离余量和视线规则；自动检查与 AI 复审不代表真实设备手感、性能、用户研究或组员批准。

## 2026-10-16：#53 / #55 审查与热点移动隐藏修复

Codex 辅助复现 #55 的 Turn 路径遗漏、增加独立整盔位姿检测、扩展热点测试并验证与 #53 三语 Help / Reset View 的组合行为，见[本轮活动](activity/2026-10-16-pr55-pose-fix-and-pr53-merge.md)。沿用现有 Unity / XRI、字体和资产，没有新增第三方模型、媒体、字体、package 或运行时依赖。自动测试与 AI 复审不代表成员人工批准、真实头显体验、构建或用户研究通过。

## 2026-10-14：个人面板视角回中与合并前修复

Codex 辅助完成 [PR #51](https://github.com/Twispra/XRmuseum/pull/51) 的个人面板回中入口、Floor 追踪高度与晚相机交互缓存修复、定向姿态/生产合成输入验证、三语近景检查及交接。沿用本机 Unity 2022.3.62f3c1 / XRI 2.6.4 包源码与现有三语字体，没有新增第三方模型、媒体、字体或 package。模拟验证与真实头显、构建、用户研究分别记录，完整主场景回归的既有 main 抓取失败未被写成通过。见[本轮活动](activity/2026-10-14-pr51-fix-and-merge.md)。

## 2026-10-14：博物馆虚拟手与双手协作候选

同日设备反馈修订：Codex 辅助编写自身角色碰撞隔离、近距表面贴合、支撑握姿与本地 Development 诊断，并从仓库已有模型只读网格派生独立 `GripSurfaceCache.asset`（480 个网格、254,198 个三角形）。原 FBX、导入器及 GUID 保留，没有新增第三方素材。小面法线归一化、模型实时变换与碰撞对恢复的依据是当前 Unity/XRI API；采用 [Unity 2022.3 MeshUtility.AcquireReadOnlyMeshData](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MeshUtility.AcquireReadOnlyMeshData.html) 与 [EditorSceneManager.NewPreviewScene](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.EditorSceneManager.NewPreviewScene.html)，预览场景的物理隔离由运行报告实际检查。用户以 Alyx 说明预期抓握体验，没有据此复制 Valve 资产或算法。具体反馈、被测版本和限制见[设备反馈记录](activity/2026-10-14-physical-hands-device-feedback.md)。

本轮对应 [Issue #50](https://github.com/Twispra/XRmuseum/issues/50)。用户引用的 ChatGPT 对话「解释物体操控逻辑」及 Half-Life: Alyx 的手柄、游戏手和物体交互思路仅作设计启发；没有取得或使用 Valve 源码、模型、手套素材或游戏媒体，也未导入原对话的 zip 示例。引用对话中的示例与初始参数不作为本项目已验证实现或设备验收依据，不能由参考关系推断媒体许可或课程 AI 使用许可。

Codex 辅助编写 `Assets/Museum/Features/PhysicalHands/` 的手套动画、碰撞代理、控制器绑定、双手抓取目标、开发接入和验证代码，并协助调整既有头盔部件协作与验证。浅灰手套、海军蓝袖口及五指几何由本项目程序原创生成，Grip / Trigger 驱动预设姿态；没有新增第三方模型、贴图、音频或字体。Box 手套包络、换手规则和保护释放阈值属于本轮原型设计，没有逐指物理或真实重量力反馈，也不称为 Alyx 交互系统复刻。

实际实现沿用 **Unity 2022.3.62f3c1 / XRI 2.6.4 / Windows PC VR / OpenXR**，使用本机既有包源码核对接口，没有升级编辑器、packages 或平台，没有复制包源码或更改官方 Samples。独立追踪目标、手代理与可见手分层，持有物继续由 XRI 驱动；对话中的 Unity 6 接口和自定义 Joint 抓取示例没有直接替换现有抓取核心。使用、包络限制和设备反馈步骤见[手模块指南](physical-hands.zh-CN.md)，候选版本与运行证据见[本轮活动记录](activity/2026-10-14-physical-hands.md)。当前候选已完成版本绑定的手模块、观察/探索/热点/三语自动验证与 Windows64 Development 构建；按[活动记录](activity/2026-10-14-physical-hands.md)区分几何/API 夹具、生产合成输入和可执行产物，并明确数学夹具的隔离条件。真实头显、性能、程序启动与用户研究仍未验证；不得沿用先前视角反馈认定新手部功能通过。

## 2026-10-13：头显跟踪与桌面模拟器启动策略

Codex 辅助核对本机 Unity / VDXR 日志、项目现有 XRI 2.6.4 与 Input System 1.14.0 包源码，修复 Editor 中模拟器删除真实头显输入的问题，并新增定向合成输入验证。继续使用现有官方 XR Device Simulator Prefab 与输入资源，未复制包源码、下载媒体或新增 package。参考 [Unity Play 模式 OpenXR runtime 说明](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/project-configuration.html#choose-an-openxr-runtime-to-use-in-play-mode)和 [VDXR 开发说明](https://github.com/mbucchia/VirtualDesktop-OpenXR/wiki/Developers)。AI 辅助范围包含诊断、启动策略代码、验证及交接；真实 Quest 3 / Virtual Desktop 复测、设备舒适性和构建仍单独记录。见[活动记录](activity/2026-10-13-xr-head-tracking-fix.md)。

用户随后确认站姿、地面正常、展品太高。Codex 辅助降低中央展示与操作区域 20 cm、保持台座底部贴地/显式复位点一致，并保留热点信息卡的原阅读高度；新增保存布局、物理复位和三年代验证，运行既有输入/三语回归并目视检查合成站姿截图。没有新增外部资源或文案；20 cm 是本轮布局假设，临时相机 1.60 m 是测试夹具，均不等于真实设备高度验收或用户研究结论。[本轮调整记录](activity/2026-10-13-standing-display-height.md)。

## 2026-10-11：SuperLite 17B v2 Unity 接入

Ereshkigal1224 按用户要求认领主体/最终总装，并由 Codex 辅助将已接受、已由 PR #43 合入的本组整盔 v2 派生为 FBX，接入现有三年代展台索引 2。原资料/媒体与估算边界沿用整盔资产记录，本轮未下载或分发第三方模型、参考照片、标牌、音频或贴图，未新增 Unity package。旧研究分件继续独立保存。

使用已有 Blender 4.5.14 LTS、Unity 2022.3.62f3c1 和本组剖面着色器。AI 辅助范围为运行时导出、轴转换、材料适配、交互绑定、剖面缓存、展台高度校准、桌面 XR 输入模拟、渲染及交接记录；没有重建/覆盖用户接受的保存源。Blender 程序凹凸/涂层没有烘焙，真实 17B 螺栓面窗的虚拟侧开与通信端点均是教学假设，不声称真实机械或接口复原。见[接入说明](superlite17b-unity-integration.zh-CN.md)与[本轮活动](activity/2026-10-11-superlite17b-unity-integration.md)。自动检查不等于实测尺寸、真实头显、构建、性能、用户研究或另一成员批准。

## 2026-10-11：SuperLite 17B 侧面比例修正 v2

沿用用户本地资料，重点对照 `04_side_rear_view.png` 与用户 Blender 截图修正壳体、颈圈、配重、头带、提手、侧阀位置和排气罩。Codex 辅助新建独立 v2 原创程序几何、Blender 渲染、保存源回读、回归对照和 Pillow 对比图；没有新增第三方模型、贴图、媒体、Unity 包或字体资产，原照片没有复制到仓库或用作贴图。中文对比标注仅使用本机既有字体渲染成图片。约 30.1° 颈圈角度与全部尺寸仍为照片建模估计，不称实测或摄影测量；AI 检查不能代替成员/用户审阅。保留用户已保存的旧模型和历史报告，未导出或接入 Unity。[v2 交付/限制](../artifacts/helmet-modeling/superlite17b-2026-10-11/revision-side-profile-v2/REPORT.md)、[活动记录](activity/2026-10-11-superlite17b-side-profile-v2.md)。

## 2026-10-11：用户资料驱动的 SuperLite 17B 整盔

用户提供本地 12 张参考图、BUILD_SPEC 和两页 17B.pdf（实际为厂家 2015 Document #150424001，覆盖 500-010 / 500-011）。Codex 逐张查看图片、读取/渲染 PDF，并访问[厂家产品页](https://www.kirbymorgan.com/products/helmets/superlite-17b)和[规格页](https://www.kirbymorgan.com/product/specs/314/superlite-17b)核对部件/材质；没有取得整盔实测尺寸。原媒体许可未核实可再分发，只在本地观察，仓库保存来源摘要与哈希，不复制原图、PDF 页面或商标贴图。

Codex 辅助新建独立原创 Blender 外观模型、程序材质、六阶段渲染、实际源文件回读与空目录重复构建。使用本机既有 Blender 4.5.14 LTS，PDF 阅读用既有 pypdf / pypdfium2，检查图整理用 Pillow；不分发工具本体，不新增 Unity 包。全部几何尺寸和材质参数为展示估计，不称实物测绘；没有覆盖原作者面窗/侧阀研究件，没有导出或接入 Unity。AI/机器检查不等于人工成员批准、真实头显、性能或用户研究。[交付/限制](../artifacts/helmet-modeling/superlite17b-2026-10-11/REPORT.md)、[本次活动](activity/2026-10-11-superlite17b-whole-helmet.md)。

2026-10-10 Phase 2 构建与技术说明：Codex 辅助把本地工程同步到 `main` @ `270c7b72`、核验构建前提（LFS 展开、编辑器版本、构建场景）、以批处理产出 Windows 播放器构建、采集产物哈希与播放器运行日志，并撰写[技术说明 Markdown 草稿](phase2-technical-note.zh-CN.md)，见[原作者记录](activity/2026-10-10-phase2-build-and-note.md)。**未新增**任何第三方模型、贴图、音频、字体或 package；**未修改** `MuseumVR/` 产品文件。构建产物按仓库约定不进 Git。原作者报告播放器在无头显本机可启动并渲染主场景，OpenXR 子系统已装配，但会话未建立（`XR_ERROR_FORM_FACTOR_UNAVAILABLE`）。2026-10-11 文档审查修订没有重做构建；原始日志和完整产物未供本轮独立复核，A4 排版页数和正式交付仍待核验，见[本次修订](activity/2026-10-11-pr38-technical-note-review-fix.md)。自动构建与模拟输入不等于真机验收或用户研究。

2026-10-06 PR #19 主线整合：Codex 辅助逐项冲突合并、现有 Noto Sans SC/OFL 字体并集与 Unity 原地补烤、生产输入共存回归、六组批处理和截图/版本证据核对；责任与限制见[本轮记录](activity/2026-10-06-pr19-main-integration.md)。205 条三语文案、536 保存字形。源字体仍来自既有 Noto Sans SC 及其许可证，不引入外部模型/音频/图片或 package。以下旧“本地”条目保留为原开发时快照。

2026-10-06 项目记录核对补充：已合入 main 的 [PR #21](https://github.com/Twispra/XRmuseum/pull/21) 沿用现有 Noto Sans SC / OFL 1.1，保留原源子集码位并补入 8 字，保存静态字体为 533 字形。Codex 辅助热点生命周期/选择距离修复、九条三语文案及适配、字体扩充/烘焙、卡片布局、自动输入测试和单眼截图复核，见[修复记录](activity/2026-10-06-hotspot-repair.md)。热点原始实现的 Codex 辅助声明见[迁移记录](activity/2026-10-06-helmet-hotspots-latest.md)；原作者 Aragorn-cyber 与修复者 Twispra 分开记录，没有新增第三方模型、图片、音频或 package。

PR #19 的电话几何、程序声音和体验细节通过本次 [#24](https://github.com/Twispra/XRmuseum/pull/24) 整合交付；来源/原型参数仍保留[源分支固定声明](https://github.com/Twispra/XRmuseum/blob/b61c13c63a0b901f1002eb764140fbe1bf27fcd7/docs/external-resources.md)，实际主线合并状态以 #24 为准。

2026-10-06 背部电话接头（本地）：Codex 辅助独立程序网格、Prefab 上的接头布局校正、两条连接锚点、通话到达／回复反馈和模拟检查，见 [交接](activity/2026-10-06-rear-telephone-connection.md)。左右用途依据 [美国海军水下博物馆说明](https://navalunderseamuseum.org/mk-v/)；曲线、局部尺寸、壁厚和视觉响应属于原型设计，非历史测绘数据。没有下载照片、外部模型或音频；程序 Mesh 保存为新增资产，原 FBX 未改。新增三語标签使用已有字形，不引入新字体。

2026-10-06 体验细节完善（本地）：Codex 辅助玻璃／截面 shader、已有网格截面算法、前窗缓动、设备方向强调、短返回动画、程序机械音效和模拟验收，见 [交接](activity/2026-10-06-helmet-experience-polish.md)。没有下载或引入外部音频、图片、模型、贴图或新 package；音效在 HelmetMechanicalFeedback 中由确定性的振荡／噪声及包络合成，不是历史设备录音。字体沿用既有 Noto Sans SC／OFL 来源，补充声音按钮所需字形与三语文案；不增加新字体来源。新画面和声音不构成精确历史复原、设备实测或用户研究。

此前仅文档 PR #22 由 Codex 辅助读取现有代码/报告和 GitHub 证据、整理贡献/问题/周索引与检查链接，见[核对记录](activity/2026-10-06-project-records-audit.md)。该文档核对未生成或下载产品资产、未重跑 Unity、未做实机/用户研究；不代写个人反思或替代成员人工审批。以下按日期保留旧资源与字形数量快照。

2026-10-06 PR #10/#13/#14 补齐：Codex 辅助冲突整合、保存文案与四组场景绑定、字体缺字重烤、源文件和像素核对、模拟 XR 输入/演示回归、截图复核及交接记录，见[本轮记录](activity/2026-10-06-pr14-completion.md)。沿用既有源 TTF，静态 TMP 图集由 190 增至 198 字符；没有新增模型、贴图、音频或 package。补入 [Noto Sans SC 官方 OFL 文件](https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/OFL.txt)至 [docs/licenses/noto-sans-sc-OFL.txt](licenses/noto-sans-sc-OFL.txt)，SHA256 `1c05c68c34f9708415aada51f17e1b0092d2cea709bf4a94cd38114f9e73d7d9`。字体 name 表与该文件均注明 Adobe 2014–2021 版权及保留字体名 `Source`；当前 subset 的字体族名为 Noto Sans SC。以下旧版本字形数量为历史记录。自动验证和 AI 复核不代替成员人工审批、真实头显或用户研究。

2026-10-04 中英语言切换模块：助手辅助实现界面语言切换系统与文档，见[模块说明](localization.zh-CN.md)与[活动记录](activity/2026-10-04-localization.md)。

**新增外部资源：Noto Sans SC 字体。** 来源为本机已安装的 `C:\Windows\Fonts\NotoSansSC-VF.ttf`（Google 的 Noto Sans SC），
许可 **SIL Open Font License 1.1**；使用、修改与再分发须保留版权/许可并遵循其条件。2026-10-06 核对内嵌声明后纠正：保留字体名为 `Source`，并非没有 Reserved Font Name，完整许可见上方链接。
用途是界面中文显示 —— 项目原有字体只有 LiberationSans，**没有任何中文字形**。

提交进仓库的是**子集版本** `NotoSansSC-MuseumSubset.ttf`（2.32 MB）：先用 fontTools 从变量字体的 `wght` 轴取出静态
Regular 实例，再子集化到 ASCII + GB2312 全集 + 常用标点，共 7545 个码位。**原始 16.95 MB 的变量字体不提交仓库**。
同类候选还有 `NotoSerifSC-VF.ttf`（同许可）；`msyh.ttc` / `simhei.ttf` / `simsun.ttc` 为微软专有，**未采用**。

TMP 字体资产（`NotoSansSC-MuseumSubset SDF.asset`）由 Unity 的 Font Asset Creator 生成，
参数 Sampling 110 / Padding 6 / Atlas 2048×2048 / SDFAA / Character Set = `Characters from File`，
共 190 个字形。生成步骤与原因见 `Assets/Museum/Features/Localization/Fonts/README.md`。

AI 辅助范围：模块架构、Editor 工具、运行时脚本、字体子集化脚本、本地编辑器问题排查与文档。
**未**新增下载模型、贴图或音频，**未**复制任何第三方素材。编辑器内预览与自动检查**不等于**真实头显测试、
学习效果评价或组员审批。

2026-10-03 年代切换模块：Codex 辅助实现头盔「历史年代切换」系统及文档，见[模块说明](helmet-eras.zh-CN.md)与[活动记录](activity/2026-10-03-helmet-eras.md)。三个年代的年代与结构描述沿用[模型依据](helmet-prototype-reference.md)中已记录的公开来源口径（DESCO、U.S. Naval Undersea Museum、Royal Museums Greenwich 等），**没有新增下载资产**。9 套材质是程序生成的纯色占位材质，**不是历史外观复原**，未使用任何第三方贴图或模型，也未复制馆藏媒体。AI 辅助范围包括架构设计、Editor 工具、运行时脚本、本地编辑器问题排查与文档；编辑器内预览与自动检查**不等于**真实头显测试、用户研究或组员审批。

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

2026-10-07 Mark V精细资产候选：Fttt1003通过Codex辅助检索/对照七项公开史料、生成原创几何和两套2K金属基色/粗糙度/法线贴图、渲染、FBX/GLB回读及浏览器检查。主要外观对象为Hampton Roads Naval Museum的1944 DESCO标准空气式馆藏件；制造商当前参数、1916原始海军手册和1944件资料分开使用，局部尺寸/旧化/内部路径为估算。模型仅保存在独立资产目录，没有替换Unity模型。详见[研究与完整来源](../artifacts/helmet-refinement/eras/mark-v/references/research.zh-CN.md)及[活动记录](activity/2026-10-07-mark-v-refined.md)。

本轮原始网页、馆藏照片和手册PDF仅作本机研究，仓库保留链接、访问记录和参数摘要；未复制原图、扫描贴图或第三方模型，不推断其媒体可再分发。新增资产的材质图是本项目脚本原创合成，不是测量获得的历史材质。工具为既有官方Blender4.5.14 LTS；浏览器审阅复用three.js0.180.0（MIT），许可保留于模型 `preview/three-LICENSE.txt`，打包工具为esbuild0.25.10，不引入Unity package。Codex辅助检查与渲染观察不等于其他成员人工批准、精密实物复原、VR设备测试或用户研究。两份NAVSEA PDF返回403，明确未作为已读图纸使用。

2026-09-30 AI 辅助范围：根据 [PR #3 的实测评论](https://github.com/Twispra/XRmuseum/pull/3#discussion_r4143841614)复现并修复剖切球柄方向跳变，补充实际 Prefab 几何扫描和原主场景模拟 XR 输入测试，整理交接证据。没有引入新外部素材或 package；AI 代码检查及自动测试不等于组员审批或真实头显测试。见[处理记录](activity/2026-09-30-section-continuity-review.md)。

记录依据为 Group Project Description（202627A）p12；本表记录已知许可或尚未核实之处，不赋予第三方内容新的再分发许可。头盔研究副本和课程官方 PDF 留在本地 `project-resource/`、`artifacts/helmet-prototype/references/`，不作为本组原创资产提交。今日尚无新增音效、扫描资产、下载模型或烘焙贴图。


2026-10-06 三语改动：Codex 辅助整理 193 条三语文案、连接静态与动态文字、扩展字形及自动验收。繁体初稿使用 OpenCC 的 s2t 转换，本地工具未放进 Unity 或运行时依赖；术语、提示和历史边界由本轮代码／文案检查复核，尚无独立语言审校。源字体仍为本机 Noto Sans SC 变量字体（OFL 1.1），使用 fontTools 静态实例化并子集化，新增繁体所需码位，不引入新模型、图片、音频或 package。许可保留，官方来源 https://github.com/google/fonts/tree/main/ofl/notosanssc 。功能和限制见 [三语记录](activity/2026-10-06-trilingual-museum.md)；自动检查不等于真实用户或头显验证。


2026-10-07 供气／通话独立精修：见 [研究依据](../artifacts/connection-demo-refinement/references/research.zh-CN.md)和[本轮记录](activity/2026-10-07-connection-demo-refinement.md)。新依据为 U.S. Navy Department《Diving Manual, July 1916》的托管扫描、South Georgia Museum 的 Diver’s suit 页面、DESCO 的 Mark V construction 页面及 U.S. Naval Undersea Museum 的 MK V 页面。仅提交可查链接、页码、读取记录和有限事实摘要，不分发原扫描／馆藏媒体，不将托管扫描的来源误标为制造图纸。当前制造商信息与 1916 设备事实分开。

本轮设备几何、六张1024px木材／金属PNG、三方案六条WAV均为原创程序生成、Codex辅助；音效不是历史录音，没有真实人声。资产沿用本项目权利安排，不为第三方素材另行授予许可。已有PR #32的本组Mark V模型只制成轻量浏览器参照副本，原源／导出不变，哈希追溯见资产qa/context-provenance.json；不声明与1916设备实际配套。

浏览器使用本机已有 Three.js 0.180.0（MIT），许可全文保留于资产preview/three-LICENSE.txt并内嵌HTML。构建用本机已有 esbuild 0.25.10，建模用官方 Blender 4.5.14 LTS，音效用numpy；不向Unity增加package或工具本体。AI辅助范围为研究核查、几何材质、动画与合成音轨、代码及验证／交接；自动初检不等于人工试听、用户研究、头显性能或组员批准。
## 2026-10-07：Mark V R2 补充资料与精修

在原PR #32 / Issue #25继续，Fttt1003通过Codex辅助检索DESCO制造说明、2011年Making an Icon及前窗/锁紧件/铰链销三个现售零件页，实际核对四张零件照片。[完整URL、适用边界及改模依据](../artifacts/helmet-refinement/eras/mark-v/references/refinement-r2.zh-CN.md)，[下载记录](../artifacts/helmet-refinement/eras/mark-v/references/sources.json)。原媒体只在本机研究，不再分发、未作贴图。未新增Unity依赖、第三方模型或收费素材。

Codex辅助增量改模、原创材质图、导出、渲染与自动/浏览器检查；内部走线仍为示意。资产自动检查不是成员人工批准、设备测试或精密历史复原；three.js/esbuild依赖与MIT声明沿用R1。

## 2026-10-07：精细资产 Unity 整合

Fttt1003 发起，Codex 辅助将已有 Mark V R2 和供气／通话设备派生为 Unity 运行时资源，并接入现有主展厅。主要依据继续使用 [Mark V 研究](../artifacts/helmet-refinement/eras/mark-v/references/research.zh-CN.md)、[R2 制造商资料](../artifacts/helmet-refinement/eras/mark-v/references/refinement-r2.zh-CN.md)与[设备研究](../artifacts/connection-demo-refinement/references/research.zh-CN.md)；本轮没有新增下载第三方模型、贴图或音频，没有分发馆藏媒体／手册扫描，也没有将不同年代的展览组合声明为已确认实物配套。

新增 FBX 为本组原创源模型的派生导出，Mark V 高面数静态件保守减面；设备保留动画、节点和锚点。源文件、旧导出和历史 QA 不覆盖，源哈希与 FBX 回读记录见 [运行时报告](../artifacts/refined-model-integration/qa/runtime-preparation.json)。建模／导出使用已有 Blender 4.5.14 LTS，不分发工具本体，也不增加 Unity package；Unity 仍为 2022.3.62f3c1。材质图沿用原资产的程序合成作品；运行时采用较柔和的原创 `gentle` 空气／通信 WAV，未把合成轨道当作历史录音或真实人声。

AI 辅助范围为运行时导出、原功能兼容绑定、材质／截面升级、机械动画及声音接入、桌面模拟输入与渲染验证、代码审查和交接文档。具体结果以[本轮活动记录](activity/2026-10-07-refined-model-integration.md)为准；源模型回读不等于 Unity 通过，桌面模拟和 AI 初检不等于成员批准、人工试听、真实头显、用户研究、性能或构建验收。历史尺寸、局部结构、旧化、音效、可见传输与展厅缩放继续分别标明事实／估算／教学表达，课程 AI 政策仍由有访问权限的成员核对。

## 2026-10-08：SuperLite 17B 面窗与侧阀外观研究

Ereshkigal1224 负责本轮细节与外观，Codex 辅助核对 Kirby Morgan 的 [17B 产品页](https://www.kirbymorgan.com/products/helmets/superlite-17b)、[规格页](https://www.kirbymorgan.com/product/specs/314/superlite-17b)及 [2025 零件分解图 Document #241108001](https://kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf)。参考对象拟定 P/N 500-010；对应观察、部件编号及估算界线见[独立资料](../artifacts/helmet-refinement/eras/superlite-17b/references/faceport-exterior-v1.zh-CN.md)。原图片和 PDF 仅供观察，不复制到仓库或作贴图，也不推断其再分发许可。

两张 1024px 基色/粗糙度贴图由本项目脚本原创生成；Blender4.5.14 LTS 从官方便携版仅在本机临时使用，未提交软件本体或增添 Unity 包。Codex 辅助独立 Blender 分件、GLB、渲染和回读检查，详见[活动记录](activity/2026-10-08-superlite17b-details-v1.md)。材质和尺寸为展示估算，自动检查不等于组员人工审阅、实物测绘、真实 VR 或用户研究。

## 2026-10-09：SuperLite 17B 扩展资料核对

Ereshkigal1224 接受维护者提出的 500-010（posts）**整盔资料研究**，Codex 辅助整理 [厂家 17B 产品/局部照片](https://www.kirbymorgan.com/products/helmets/superlite-17b)、[规格](https://www.kirbymorgan.com/product/specs/314/superlite-17b)、[零件图 Document #241108001](https://kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf)、[通信手册 Document #240617001](https://www.kirbymorgan.com/sites/default/files/2024-07/communications-on-superLite-17b-and-17c-helmets-kmb-18-and-28-bandmasks.pdf)和[压圈材料变更公告 #3/2021](https://www.kirbymorgan.com/sites/default/files/2021-06/Bulletin-03-2021.pdf)。媒体版权未核实为可再分发，本仓库只留链接、观察、引用位置与原创 v1 预览；未复制照片/PDF 页面、未做贴图。适用配置、未知年代、欠缺视角与事实/估算分界详见[整盔索引](../artifacts/helmet-refinement/eras/superlite-17b/references/whole-helmet-source-map-v2.zh-CN.md)。

Codex 辅助编写 v2 修正脚本和静态检查，**没有产生新的模型二进制或渲染**：官方 Blender 4.5.14 便携版运行时被本机 Windows 完整性策略拦截，未关闭保护，见[活动记录](activity/2026-10-09-superlite17b-review-response.md)。不把脚本设计写成完成的可编辑模型、装配或人工外形验收；未新增第三方模型、贴图或 Unity 依赖。

## 2026-10-09：SuperLite 17B v2 资料与可编辑分件

本次继续由 Ereshkigal1224 负责，Codex 辅助使用 Kirby Morgan 原始[面窗手册 #260901001](https://www.kirbymorgan.com/sites/default/files/2026-09/face-port-port-retainer-nose-block-260901001.pdf)、[侧阀手册 #260701004](https://www.kirbymorgan.com/sites/default/files/2026-07/brass-chromed-brass-side-block-260701004.pdf)、[侧阀分解图 #230118015](https://www.kirbymorgan.com/sites/default/files/2023-01/chromed-brass-side-block-blowapart-230118015.pdf)、厂家照片及[使用条款](https://www.kirbymorgan.com/company/terms-usage)核对部件和外形。[完整来源登记、图号/页码、适用范围和许可状态](../artifacts/helmet-refinement/eras/superlite-17b/references/whole-helmet-source-map-v2.zh-CN.md)在独立年代资料目录。厂家照片、PDF 页面、标牌和商标未复制入仓库或贴图；公开可浏览不作为重新分发许可。新源文件/GLB/预览使用仓库已有两张脚本原创贴图。

微软商店官方 Blender 4.5.14 LTS 在本机完成 v2 两分件生成、GLB 导出与回读；便携/安装版的 Windows 应用控制阻挡记录保留为历史，没有改安全策略。Codex 自动检查只能证实文件结构、UV、导出透明材质和局部估算参数一致；不代表外形测绘、装配、Unity/VR 或其他成员人工批准。见[本次活动记录](activity/2026-10-09-superlite17b-v2-research-and-assets.md)。未新增 Unity 包或运行时依赖。

## 2026-10-10：SuperLite 17B 资料收束和 v3 结构修正

Ereshkigal1224 负责，Codex 辅助核查 [Kirby Morgan 产品页](https://www.kirbymorgan.com/products/helmets/superlite-17b)、[黄色整盔照片](https://www.kirbymorgan.com/sites/default/files/2021-02/17B-Helmet-Colors-yellow.jpg)、[侧阀照片](https://www.kirbymorgan.com/sites/default/files/2021-02/fiberglass-side-block-IMG_0387.jpg)、[面窗手册 #260901001](https://www.kirbymorgan.com/sites/default/files/2026-09/face-port-port-retainer-nose-block-260901001.pdf)、[侧阀分解图 #230118015](https://www.kirbymorgan.com/sites/default/files/2023-01/chromed-brass-side-block-blowapart-230118015.pdf)及[厂家 FAQ](https://www.kirbymorgan.com/faq)。完整逐项用途、局限和未找到的资料见[本轮资料收束](../artifacts/helmet-refinement/eras/superlite-17b/references/research-freeze-2026-10-10.zh-CN.md)。照片和 PDF 只链接，不复制或作贴图；许可未核实为可再分发。v3 Blender/GLB/预览由本项目生成，颜色与几何均为展示估计，未新增第三方模型或 Unity 依赖。官方 Blender 4.5.14 回读通过只证明文件结构，不代表外形精度、整机装配或成员批准。

## 2026-10-10：SuperLite 独立资产主线归档

按维护者明确授权，Codex 将 Ereshkigal1224 在 [PR #37](https://github.com/Twispra/XRmuseum/pull/37) 交付、已合入资产分支的独立资料、原创源模型、GLB 和预览整合到最新 main。原作者提交与历史记录保留；仅处理来源记录冲突、范围核对、LFS 实体校验和实际导出几何复验，没有生成或修改模型，没有接入 Unity。见[整合记录](activity/2026-10-10-superlite17b-main-integration.md)。

独立几何复验使用本地 Node.js、Three.js 0.170.0 与 three-mesh-bvh 0.8.3（后两者为 MIT），仅作临时验证，不提交工具本体、不新增 Unity 或产品运行时依赖。本轮未重新运行 Blender、Unity、真实头显或用户研究；AI 技术审查不等于另一位成员的人工批准，也不将局部分件写成整盔装配完成。

## 2026-10-12：SuperLite 展示台合并前修复

按用户授权，Codex 修复 [PR #44](https://github.com/Twispra/XRmuseum/pull/44) 的年代热点/展签、VOICE 部件高亮及材质颜色转换，并补充输入、三语和 GPU 回读验证。没有生成或替换保存的 SuperLite 模型；几何与来源沿用 #43 和原接入记录。

五条新三语文案需要补字，继续从本机同一 Noto Sans SC 变量字体（SIL OFL 1.1）以 fontTools 4.60.1 的 Regular 400 实例扩展既有子集，保留全部 7660 原码位，加入「側、僅、夾、環、輕、頸」至 7666。保存静态 SDF 原位补烤为 558 字形，保留原 GUID、字体/材质/图集子资源引用；许可仍见 [OFL](licenses/noto-sans-sc-OFL.txt)。工具只用于本次本地维护，没有新增 Unity 包或产品运行时依赖。来源哈希及保存字节证据见[本轮字体报告](../artifacts/superlite17b-unity-integration/qa/runs/20261012-pr44-fix/font-completion.json)；实际回归、AI 辅助范围与未验证边界见[修复活动](activity/2026-10-12-pr44-fix-and-rereview.md)。

## 2026-10-16：实体 UI 本地验收候选

按用户要求，Codex 辅助实现环绕旋转／剖面图形、实际前窗就地提示和用户视角拖动旋转，并编写生产输入回归。图标为 C# LineRenderer 原创几何，无外部图标、图片、模型或新增 Unity 包。字体沿用上方 Noto Sans SC / OFL 来源及本地 fontTools，保留 main 原 cmap，补入 啟／顯；保存表、静态 SDF 和原 GUID 同步维护。[本轮范围、验证及限制](activity/2026-10-16-diegetic-icons-view-rotation.md)。成果尚未提交、推送或创建 PR；AI 与模拟输入验证不代表成员人工验收或真实头显结果。

本地后续按用户选择删除独立旋转入口、制作固定切面滑轨、松开确认及取消反馈，并添加可从展台关闭演示的入口。均由 Codex 辅助编写原模块内 C#／三维线条，未新增运行时依赖或外部图像。字体沿用同一来源，原 7669 cmap 保留后增加 塊／軌 至 7671，三语表及静态字体一起维护；仍待用户验收，尚未提交或推送。

固定滑轨与本体操作的实际验证／未验证范围见[第二轮活动](activity/2026-10-16-stable-section-body-controls.md)。

## 2026-10-17：高度偏好与演示设备观察（本地候选）

Codex 按用户要求辅助编写可调视角高度、固定摆放的演示设备及共用旋转手势；沿用已有精细模型、机械动画、音频及接口资料，没有生成模型、历史事实或引入新的运行时依赖。只读核对本地 XROrigin 2.5.2 源码的高度管理，采用跟踪树临时偏移，原型参数不作为测量或人体工学标准。新增四条三语文案；源 TTF 未改变，沿用同一 Noto Sans SC / OFL 字体及原 GUID，静态图集补齐所需字形。验证边界及证据见 [活动记录](activity/2026-10-17-view-height-demo-rotation.md)；未提交、未推送，未修改远程 Issue / PR。

2026-10-17 发布补记：用户已验收并授权提交上述实体UI累计版本及发布五项待认领任务。实现与来源范围未再扩大，公开证据只省去无关旧文件清单及本机绝对路径，原报告保留。提交/PR与状态以 [发布交接](activity/2026-10-17-diegetic-release.md) 为准，未合并main、未新增实机或构建结论。

2026-10-17 后续：按用户选定的体验建议，Codex 辅助实现持续旋转防抖、低速精细增益，以及不改变命中范围的近距提示避让／成功操作后短提示，并编写实际 Input Actions 模拟回归。算法、三维引线和提示布局为本项目代码，没有新增外部素材、历史数据、包或产品依赖。参数为待校准的原型设置，不作为人体工学或老年用户实测数据。新增两条三语短文案未增加字符，现有 Noto 静态字体完整覆盖，字体二进制和 GUID 不变。[本轮实现、证据与交接](activity/2026-10-17-rotation-precision-hints.md)；模拟和 AI 审阅不等于真实头显、用户研究或另一位成员批准。

## 2026-10-17：照片头盔 Unity 对比与解释性剖切

2026-10-18 #67 复审接入：按用户条件合并授权，Codex 辅助将同一研究模型作为主场景左台独立展品，补充松手确认／取消、实体遮挡与面板避让的集成检查及修复、生产输入模拟、三语和主线回归。模型、原照片来源和尺寸／身份未核实的边界沿用既有记录，没有新增第三方媒体、模型、字体或 Unity 包；1988 仍仅来自组员照片中的标牌声明。独立对比场景继续保留，本段接入范围以[本轮复审活动](activity/2026-10-18-pr67-review-and-merge.md)和 PR #67 为准，不据此声称 #64 的全组布局意见或真实头显验收已完成。

依据仍是小组前四张实物照片及既有 [PR #28](https://github.com/Twispra/XRmuseum/pull/28) 的照片外形研究；本轮不新增或分发原照片、馆藏媒体、第三方模型或 Unity 包。照片头盔第二稿 GLB 为此前本地原创/AI 辅助成果，本轮用本机 Trimesh 4.12.2 转成 Unity 可导入的空心壳体与细节 OBJ，贴图沿用 GLB 中既有的两张解释性材质图。Trimesh 只用于本地导出，不进入 Unity 运行时。

Codex 辅助编写独立对比场景、预制件、剖切预览和编辑器验证；借用项目已有 `Museum/Inspection Shell` 与 `SectionCapGeometry`，没有把 Mark V 铰链搬到照片模型上。模型身份、隐藏结构、尺寸及开窗机构未核实；编辑器测试不是实物测绘、真实头显或组员验收。[具体范围和结果](activity/2026-10-17-photo-helmet-unity-study.md)。
