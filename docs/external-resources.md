# 外部资源与辅助工具记录

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
