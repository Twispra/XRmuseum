# SuperLite 17B 精细模型：Unity 展示台接入

本轮将用户接受的整盔侧视修正版 v2 接入中央展示台，替换 SuperLite（索引 2）的占位外观。索引 0 的早期头盔仍是占位模型；索引 1 的 Mark V 精细模型和默认年代保留。本轮接入在工作分支/PR 中，main 是否包含它请查看实际合并状态。

## 打开与查看

1. 获取本轮分支后，确认 Git LFS 已下载并展开实际 FBX。
2. 用 Unity **2022.3.62f3c1** 打开仓库下的 `MuseumVR`，打开 `Assets/Museum/Scenes/MuseumStart.unity`。
3. 点击 Play，再点击 Game。默认显示 Mark V，按 **F9** 切换到黄色 SuperLite，**F10** 回到上一年代；也可使用原展台年代切换按钮。
4. 原抓取、WINDOW、SECTION、FLOAT、RESET、AIR / VOICE 和语言入口继续使用。语言预览为 F6 简体、F7 繁体、F8 英文。

普通查看直接打开保存场景即可。**不要运行旧头盔生成器、Mark V 安装器或 SuperLite 的 Install / AlignSupportBatch**：这些工具只用于首次接入与本轮校准，不能用来覆盖后续人工修改。

## 模型与保存资产

- 建模源：`artifacts/helmet-modeling/superlite17b-2026-10-11/revision-side-profile-v2/delivery/superlite17b.blend`；保存源未修改，不从旧生成参数重建。
- 运行时导出：`artifacts/superlite17b-unity-integration/exports/superlite17b-v2-runtime.fbx`。
- Unity 副本：`MuseumVR/Assets/Museum/Features/SuperLiteModels/Models/superlite17b-v2-runtime.fbx`；两份 FBX 内容相同，均由 LFS 管理。
- 总装仍使用已有 `InspectableHelmet.prefab`，保留其 GUID、原对象 ID、XR 抓取和物理组件。合并前修复给 Prefab 加入年代文案组件，主场景只配置该组件的三个热点、表面和展签引用；原场景对象保留。
- 材质和独立剖面缓存放在 `SuperLiteModels/Materials`、`SuperLiteModels/Data`。Mark V 模型、材质和缓存没有替换。

运行时保留 **143 个可见网格、50,268 三角面**，轴转换后宽/高/深约为 **0.282 / 0.314 / 0.325 米**。这是照片建模的展示尺寸，尚无同配置实物测量，不属于制造规格。源哈希、导出回读、总装与引用检查见[资产记录](../artifacts/superlite17b-unity-integration/README.md)。

放置按原根节点落稳的位置校准：展示顶面 1.100 米，模型底部 1.103 米。原展台、质量、碰撞体和归位逻辑保留。保存的 home 高度与运行时根节点相差约 12 毫米，修改布局或物理配置后需重新验证，不能直接套用此校准。

## 已知边界

17B 实物面窗由螺栓固定。本项目保留原博物馆的侧向打开面板供内部观察，**不是该型号真实机械操作的复原**。14 个前窗分件随原交互铰点移动，切换年代时的命中范围与剖面数据随模型更新。

SuperLite 的观察点分别绑定前窗、外壳与颈部密封的实际表面；前窗点随面板移动。展签和热点说明同时维护简体、繁体及英文。切回 Mark V 时恢复原位置和文案，三部位探索进度保持；打开前窗是观察演示的限定也出现在访客说明中。

AIR 使用侧阀附近的估计锚点；VOICE 使用独立教学端点和原示意路径，并以三语标注“通信位置（示意）”。整盔源没有已确认的 posts 通信接头几何，本轮未声称补齐真实接口，也不将颈箍或铰链作为电话接头高亮。旧分件资料继续独立保存，未将不同版本拼成已认证总装。

Unity 沿用本组剖面/悬停着色器，转移源材质颜色、金属度、粗糙度并使用透明玻璃。非玻璃材质先把源线性 RGB 编码为 Unity Color 属性所需的 sRGB；保存材质与导入代码一致，避免重复线性转换。Blender 程序凹凸和涂层节点没有烘焙为 Unity 贴图，亮面细节与 Blender 渲染有差别。

自动验收是 Unity 编辑器桌面 XR 输入模拟与临时渲染；真实头显、性能、Windows 构建及访客体验仍待成员验证。原接入证据见[原活动](activity/2026-10-11-superlite17b-unity-integration.md)，四项合并前修复、复审和新报告见[修复活动](activity/2026-10-12-pr44-fix-and-rereview.md)。
