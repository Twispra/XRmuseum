# 精细模型接入主展厅：使用与交接

2026-10-07，本轮在最新主线 `afc3a9e` 上接入已有 Mark V 和供气／通话精细资产。当前是整合候选；是否合入 main 以本轮 PR 的实际合并记录为准。[活动与本轮验证](activity/2026-10-07-refined-model-integration.md)记录最终测试结果。

## 打开后看什么

打开 `MuseumVR/` 的已保存 `Assets/Museum/Scenes/MuseumStart.unity`，使用 Unity **2022.3.62f3c1**。新 clone 先展开 Git LFS；日常查看不运行任何模型或场景生成器。

- 中央 `HelmetExhibit` 默认显示 **Mark V（年代索引 1）**。它仍使用原来拿放、悬停、复位、前窗和剖面操作入口；前窗玻璃、窗框和锁紧件随既有动作展示，背面供气与电话保持两个独立接口。
- 选择 AIR 或 VOICE，原来的演示时序、重播／停止、SOUND 和减少动画入口继续使用。AIR 显示双缸泵；VOICE 显示水面电话和潜水员电话示意件，连接线终点跟随当前头盔接口。
- 切换到年代索引 **0 或 2**，仍显示原项目的占位几何和年代材质。这两个时期的精细模型尚未接入；不能把本轮交付写成“三时期历史造型全部完成”。年代键盘预览沿用 F9／F10，具体模拟器操作见[协作指南](getting-started.zh-CN.md)及原功能说明。

开窗、旋转、剖面、AIR／VOICE、热点、切换年代、切换语言和复位应在同一主场景检查。独立浏览器资产审阅器方便讨论造型；它不能代替 Unity 主场景兼容验证。

## 文件关系与维护边界

| 层次 | 入口 | 本轮用途 |
| --- | --- | --- |
| Mark V 可编辑原源与历史依据 | [Mark V 资产目录](../artifacts/helmet-refinement/eras/mark-v/README.md)、[R2 依据](../artifacts/helmet-refinement/eras/mark-v/references/refinement-r2.zh-CN.md) | 继续合作精修；历史源、原始导出和旧报告保留 |
| 设备、动画与声音候选 | [演示资产目录](../artifacts/connection-demo-refinement/README.md)、[研究依据](../artifacts/connection-demo-refinement/references/research.zh-CN.md) | 继续补资料、修设备、比较音效，不直接覆盖运行时副本 |
| 本轮运行时派生资产 | `artifacts/refined-model-integration/exports/`、[导出汇总](../artifacts/refined-model-integration/qa/runtime-preparation.json) | 单独保留四个 FBX 和几何／动画回读报告，可追溯到原源哈希 |
| Unity 使用副本 | `MuseumVR/Assets/Museum/Features/RefinedModels/` | FBX、材质、贴图、音轨、设备 Prefab、截面网格数据及绑定代码 |
| 既有主场景和头盔 Prefab | `MuseumStart.unity`、`InspectableHelmet.prefab` | 原资源 GUID 和公共交互入口继续沿用；整合者协调单一写入 |

`RefinedHelmetBinding` 负责按年代切换可见几何，并重绑前窗触发区、剖面、背部连接和 AIR 热点。原头盔根对象、Rigidbody、XR Grab、复位与控制器继续承担交互；模型实物重量不能直接替换原项目的交互质量参数。

前窗沿用原来的交互铰链，但可见窗件使用 `FrontWindowPivot` 的独立副本，共 23 个网格。导入 FBX 的子节点不能靠直接改父级可靠地保存到现有 Prefab，因此原导入前窗保持原层级并停用，副本 `Refined front window` 放到既有 `WindowPivot` 下；绑定、玻璃和触发区都对应可见副本。固定在窗座上的 `DoorTogglePivot`／`DoorToggleNutPivot` 仍在原模型层级，不随窗门副本迁移。

换模型后，`RefreshGeometryCenter()` 在未握持剖面工具时把导向平面的旧几何偏移清零，保留手腕驱动的方向框架。精细前窗与球柄在部分姿态下会重叠，所以 Mark V 增加了很小范围的直接握持优先规则：手的 attach 点进入已显示的剖面球柄碰撞体、且球柄确实能接受该手时，附近的三个原热点和前窗让球柄先接收选择。球柄被另一只手独占或当前不可选时不抢优先级；射线、已握持前窗和另外两个年代沿用既有规则。

`RefinedConnectionEquipment` 只提供展示设备、源机械动画采样、强调和合成声音，由既有 `HelmetConnectionDemonstration` 的同一时钟驱动，不创建第二套 XR 输入或演示时钟。显示位置／缩放在动画根节点之外设置，避免导出动画覆盖展厅布局。

材质沿用 `Museum/Inspection Shell`／`Museum/Inspection Glass` 的剖切、悬停和强调属性，壳体着色器增加基色／法线／粗糙度贴图支持。内部露出和截面仍是教学观察功能，不代表所有隐藏结构都有史料证据。

## 模型更新时必须保留的契约

先在 [Issue #25](https://github.com/Twispra/XRmuseum/issues/25) 或 [#33](https://github.com/Twispra/XRmuseum/issues/33)说明本轮改哪些部件。同一 `.blend`、FBX、Prefab、场景或公共材质同时只有一个写入者；模型命名／节点路径会被动画、接口与剖面使用，不宜随意清理。

- Mark V 保留 `FrontWindowPivot`、`DoorTogglePivot`、`DoorToggleNutPivot` 及锚点层级；`Bonnet_HollowCopper_TinFinish`、`Corselet_CopperSaddle_WithUnderside` 和实际玻璃角色须能明确区分。含有 `Glass` 的金属压圈不是玻璃材质。
- 设备保留 `Anchor_AirOutlet`、`Anchor_TelephoneOutlet`、`Anchor_DiverTelephone`、膜片与泵的运动节点。完整 FBX take 一起采样，不能只留下一个手轮的轨道。
- Blender 源采用 Z 向上、−Y 为前。当前 Mark V 显示根使用 `Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 180, 0)`，即先做 Y 180°、再做 X +90° 修正；安装器依据壳体的原始高度方向决定是否先做 Y 180°。不直接假设 FBX 导出选项已经完成最终坐标转换，也不把这组旋转照搬到其他资产。修改导入选项后重新核对头盔高度、前方、铰链、背面接口以及设备动画。
- 模型／材质更新同时检查闭窗和开窗、多个方向与深度的剖面、头盔移动后的线路终点、年代切换和连续复位。前窗可见几何变大或变小后，必须核对触发区；仅截图好看不证明仍能操作。

`Museum/Refined Models/Install saved models` 是整合安装工具，不是成员日常打开工程的步骤。再次接入其他时期前，应扩展绑定方案并审查 saved Prefab，不通过删除组件、重建 meta 或重跑旧灰模生成器强制覆盖。

## 优化结果与证据边界

| 资产 | 源三角形 | 运行时 FBX 的 Blender 回读三角形 | 保留网格数 |
| --- | ---: | ---: | ---: |
| Mark V | 409,524 | 141,502 | 255 |
| Mark III 双缸泵 | 56,340 | 56,340 | 307 |
| 水面电话 | 29,584 | 29,584 | 108 |
| 潜水员电话示意件 | 9,528 | 9,528 | 16 |

Mark V 只对允许精简的高面数静态件做保守减面，保护壳体／窗孔、玻璃、关键窗件与小件；没有跨前窗或运动根合并。设备保留几何和动画节点。本轮 Blender 报告通过源哈希、节点路径、边界和五时刻动画／锚点回读；它们不包含 Unity 功能测试。Unity 导入日志观察到 Mark V **141,262** 三角形，与 Blender 回读数分开记录。

[资产一致性报告](../artifacts/refined-model-integration/qa/asset-parity.json)核对四个运行时 FBX、贴图与声音的导出／源副本和 Unity 使用副本，SHA-256 均匹配且 LFS 已展开；原头盔 Prefab 的 386 个既有 fileID 均保留，没有新增 GUID 冲突。fileID 保留证明对象身份没有被整套重建，具体行为仍需 Unity 回归验证。

电话膜片原先只在相隔 0.5 秒的两个时刻比较位置，可能刚好落在相同振动相位而得到近零差值。补充的[膜片运动读回报告](../artifacts/refined-model-integration/qa/diaphragm-motion-readback.json)对源 `.blend` 与运行时 FBX 各取 **257 个时刻**：2 秒内 241 个密集样本，加 16 个错开周期的时刻，并按各自动画起点对齐。报告在局部、根相对与世界米制坐标中均确认运动；两套电话的世界坐标采样峰峰位移约 **1.591 mm**，源与 FBX 的最大采样位置误差小于 **0.00001 m**。这是教学动画的读回证据，不是实测历史膜片参数，也不证明每个连续时刻或 Unity 导入后的行为。Unity 验收另外按多帧采样并补偿稳定显示父节点，使用层级变换后的世界位移判断，避免把缩放后的局部数值当成米；不能把原来两个时刻的近零差值归因于动画压缩。

没有因减面完成 LOD、静态批处理、碰撞网格或 draw call 优化，也没有设备帧率、内存或头显验收保证。255／307 等分件数保留了可维护性，也带来后续性能检查需求。重新导出可运行 `artifacts/refined-model-integration/scripts/prepare_runtime.py`；指定新的 `--output` 目录，脚本拒绝覆盖已生成文件，原证据不重写。

历史外观主要参照 1944 DESCO 标准空气式 Mark V，设备部分参考 1916 手册的 Mark III 泵和电话说明。两者用于解释水面支持，**没有证据证明这套泵／电话与该件 1944 头盔实际配套**。局部尺寸、旧化、内部路径、布局缩放、可见气流、信号波及音效均沿用源研究中的估算或教学表达边界；不推算安全下潜深度、供气性能或真实声学效果。

Unity 当前选择较柔和的 `gentle-air.wav`／`gentle-voice.wav` 合成方案，并跟随原声音开关。它们不是历史录音、真实通话或参与者试听后的最终选择，其他音效候选仍在原资产目录。

## 验证与下一位接手

本轮最终运行结果、被测版本、报告和截图见[活动记录的验证段](activity/2026-10-07-refined-model-integration.md#验证)。自动验收入口为 `Museum/Refined Models/Validate saved main museum`；批处理使用 `Museum.RefinedModels.EditorTools.RefinedModelValidation.RunBatch`，需图形渲染，不加 `-quit` 或 `-nographics`。报告输出到 `artifacts/helmet-interaction/qa/runs/` 的独立运行目录，不覆盖旧报告。

优先接手项是另两时期精细造型、人工造型／史料复核、声音试听和目标设备性能。原输入、观察、探索、热点及三语回归要绑定同一个待提交版本；真实头显、坐姿单手可达性、学习效果、舒适性和可执行构建继续分别记录，不能从桌面模拟通过推断完成。新增访客说明遵循[三语维护流程](localization-authoring.zh-CN.md)。
