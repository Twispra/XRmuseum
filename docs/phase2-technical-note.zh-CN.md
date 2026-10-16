# XRmuseum — Phase 2 技术说明草稿

**课程／方向**：COMP5424 Extended Reality · Scenario B「Accessible Museum Experience」

**目标**：Windows PC VR 博物馆体验；Unity **2022.3.62f3c1**、OpenXR、XR Interaction Toolkit **2.6.4**、URP。

**版本**：以 `main` @ `411524e`（2026-10-16）为修订基线；本轮 [Issue #57](https://github.com/Twispra/XRmuseum/issues/57) 的最终源码与检查：**〔整合后填写被测 commit、报告与排版页数〕**。

## 1. 项目目标与访客流程

观众进入海事展厅，观察潜水头盔，按兴趣探索「头盔如何与水面连接」。现有流程为：选择语言与偏好 → 拿起、转动、开窗或剖面观察 → 查看三个部位热点与 AIR／VOICE 演示 → 回顾发现 → 恢复展项或开始下一轮参观。线索无强制顺序、倒计时或必答题；“访问过”只代表打开内容，不代表已经理解。

默认展品是 **Mark V 精细模型（年代索引 1）**，**SuperLite 17B 精细模型（索引 2）**也已接入；**1839 早期档（索引 0）仍为占位模型**。年代按钮和当前说明须清楚指出这一边界。SuperLite 侧向开窗沿用博物馆观察工具，实物面窗由螺栓固定；通信端点是教学示意。三年代不能写成三件完成历史复原的实物。

模型以公开馆藏、手册及照片为参考。局部尺寸、内部构造、气路／信号、动画和合成声音属于原型或教学表达，不是历史实测、流体／声学模拟，也没有证据证明所示泵、电话与某件头盔实际配套。[资产边界](refined-model-integration.zh-CN.md)、[SuperLite 接入](superlite17b-unity-integration.zh-CN.md)

## 2. 架构与交互

工程位于 `MuseumVR/`，构建入口为唯一启用的 `Assets/Museum/Scenes/MuseumStart.unity`；功能在 `Assets/Museum/Features/` 分模块维护。

| 模块 | 当前职责 |
| --- | --- |
| `HelmetExperience`／`HelmetInspection` | XRI 拿放、转动、前窗铰链、任意方向剖切、自定义剖切／高亮 shader、展品复位 |
| `PhysicalHands` | 手套代理、近距接触与简化阻挡、模型表面掌面贴附、双手支撑／换手；XRI 仍驱动展品刚体 |
| `HelmetHotspots` | 三个部位热点，悬停外环与已访问勾形；说明保持 30 秒、1 秒淡出，整盔抓取或明显移动后隐藏，重新选择才恢复 |
| `HelmetExploration` | AIR／VOICE、提示／回顾、约 16 秒分段设备演示、停止／重播、新访客与 Reset View |
| `HelmetEras`／`RefinedModels`／`SuperLiteModels` | 年代资料与事件、模型／材质切换，重绑前窗、热点、接口和剖面 |
| `Localization`／`MaritimeGallery` | 简／繁／英三语与展厅组织；静态文字绑定 key，动态文字使用 `MuseumText.Set` |

物理手支持一手持盔、另一手操作窗口／剖面；**FLOAT 单手替代方式仍保留**。当前没有逐指碰撞、形状驱动的指节 IK 或力反馈，不能据掌面贴附宣称达到成熟商业游戏的手感。[物理手说明](physical-hands.zh-CN.md)

## 3. 无障碍、说明与复位

三语覆盖入口、工具和展签；Help 解释控制方式与两种 Reset。热点在颜色之外提供悬停外环、已访问勾形；AIR／VOICE 以模型、连接线和文字表达关系，声音可关闭，减少动画时显示静态关系。这些是已实现措施，坐姿触达、双眼阅读、认知负担与实际受益仍待目标访客和设备评价。

| 入口 | 作用与访问记录边界 |
| --- | --- |
| 头盔 RESET／Home | 展品返回展台、恢复观察状态并停止演示；保留本轮 AIR／VOICE 与三个热点的发现 |
| RESET VIEW／F11 | 访客回到保存的起始位置与朝向，面板回到身前；保留实际跟踪眼高、展品状态与本轮发现 |
| NEW VISIT／新一轮参观 | 清 AIR／VOICE、提示／回顾与三个热点的访问状态，隐藏旧说明并停止演示，回探索欢迎页；保留语言、年代、声音、减少动画、展品位姿与视角 |

本轮新访客与年代说明修复的最终验收：**〔整合后填写，未取得结果前不得写通过〕**。展品与视角分别用上表两个 RESET 入口恢复。日常查看打开保存场景，不运行旧头盔生成器或模型安装器；新 clone／worktree 打开 Unity 前确认 LFS 资产已展开，`git lfs fsck` 不能代替工作区实际文件检查。

## 4. 构建、证据与适用范围

Windows x86_64 构建使用上述编辑器和保存场景，`File → Build Settings → Windows / x86_64 → Build`；接入兼容的 PC OpenXR runtime 后运行完整目录中的 `MuseumVR.exe`。Quest 3 的已有反馈来自 PC 串流，并非 Android 独立 APK。构建、缓存和原始课程资料不进 Git。

| 证据 | 实际版本与结论 |
| --- | --- |
| 2026-10-16 完整 Windows64 Development 构建 | `62c2994`：Succeeded、0 errors、2 条既有 OpenXR 可选迁移 warnings，Editor 自然退出 0；214,112,679 bytes，272 个产物有指纹。目录 `E:/Codex/Builds/XRmuseum/issue50-shell-grab-20261016-62c2994/`；该产物未启动。[报告与清单](activity/2026-10-16-issue50-shell-grab.md#验证与证据) |
| 同版本主场景／物理手回归 | `62c2994`：三语主场景 84,730 项（含热点 792 项／20 次生产 Direct/Ray 选择）、物理手 182 项，均 0 errors、正常退出 0。[版本与来源指纹](activity/2026-10-16-issue50-shell-grab.md) |
| 观察入口正常退出修复 | `e5655e6`：最终两轮 207/207、errors 为空、自然退出 0。修复仅涉及 Editor 验收与退手夹具，其余 1,138 个工程文件与上述构建源码逐字节相同；没有把历史构建改写为重新构建。[正负证据](activity/2026-10-16-pr56-editor-exit-and-merge.md) |
| 视角／Help 与热点移动专项 | `ed36889`：95/95 与 33/33，均正常退出 0；各自对应 #53／#55 的组合源码。[专项范围](activity/2026-10-16-pr55-pose-fix-and-pr53-merge.md) |
| 有限真实设备反馈 | 用户确认站立时转头／地面正常，随后反馈视角“没有太大的问题”；Quest 3／Virtual Desktop 首版物理手曾出现抓握空隙与左摇杆卡顿。修订版手感、移动和完整流程未获通过记录。[视角反馈](activity/2026-10-14-pr49-merge.md)、[手部反馈与待测项](activity/2026-10-14-physical-hands-device-feedback.md) |

自动断言数不是用户用例或参与者数量，模拟输入不等于实机验收。2026-10-10 原作者构建／无头显启动自述及哈希保留在[原活动记录](activity/2026-10-10-phase2-build-and-note.md)和[当时复核边界](activity/2026-10-11-pr38-technical-note-review-fix.md)，不再作为当前版本唯一证据。

## 5. 限制、交付与 AI 辅助

当前仍需：最新修订的完整头显走查（含首次进入、阅读、操作、退出和下一位复位）；坐姿／单手可达性与舒适性；目标设备帧率、内存和 draw call 检查；面向目标访客的理解效果评价。1839 精细模型未接入，内部隐藏结构仍为教学表达，LOD／批处理优化未验收。已有设备反馈是开发反馈，尚无正式参与者研究。

Phase 2 要求完整源码、可运行构建、教学团队可访问的仓库和不超过四页 A4 的技术说明；最新构建能否由教学团队访问、正式提交状态须由有课程权限的成员确认。本 Markdown 的最终排版与页数见本页版本行；不能从文档合并推断课程交付完成。[课程依据](course-requirements.md)

Codex 辅助范围包括代码、Editor 工具、模型派生处理、文档、自动验证和证据整理；具体来源／许可／授权见[资源记录](external-resources.md)与各次活动。成员贡献按真实版本证据记录，个人反思由本人撰写；AI 辅助不替代人工批准、实机或用户研究，课程最新 AI 政策仍由具备权限的成员核验。
