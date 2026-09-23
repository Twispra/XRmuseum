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

后续添加模型、贴图、音效、字体、脚本或插件时，应在这里记录来源、许可、用途和修改内容。

记录依据为 Group Project Description（202627A）p12；本表记录已知许可或尚未核实之处，不赋予第三方内容新的再分发许可。头盔研究副本和课程官方 PDF 留在本地 `project-resource/`、`artifacts/helmet-prototype/references/`，不作为本组原创资产提交。今日尚无新增音效、扫描资产、下载模型或烘焙贴图。
