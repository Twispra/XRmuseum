# 潜水头盔试制 v02 使用说明

日期：2026-09-23。本轮用真实尺寸灰模和一个精修前视窗样件，检验 AI／Blender MCP 的建模效果。灰模用于判断整体比例，样件用于观察铜壳、铜合金窗框、玻璃、密封圈和铰链细节。它们是两个独立资产，尚未组合成完整精修头盔。

先看下方 JPG 预览，再用 Blender 打开 `helmet-study-v02.blend` 检查模型；需要放大细节时查看完整 PNG。旧版 v01 保留。v02 文件内的场景名称仍为 `XRmuseum_Helmet_Study_v01`，这是保留的内部名称，并非打开了旧文件。

## 尺寸与修复

场景采用米制，1 Blender 单位＝1 米。最终网格的外包尺寸（宽×深×高）约为：灰模 **317.505×419.101×495.312 mm**；前窗样件 **288×114.080×288 mm**。灰模以公开实物外包尺寸为尺度依据，局部构造仍有设计假设。窗框名义孔径为 148 mm，后压圈限制的有效孔径为 143.6 mm，玻璃厚度为 4 mm。

v02 修正了 8 个资产网格的反向法线，并清理头盔壳体少量非流形、退化碎面。逐对象检查见 QA 文件。这些检查用于发现网格问题，不证明实物防水性、机械装配可靠性或运动过程中无碰撞。

## 怎样检查开合

在右侧对象列表搜索并选中 `DETAIL_Window_HINGE_PIVOT`，在“物体属性→变换”中调整其局部 Z 旋转：0° 为闭合，75° 便于观察内部，100° 为本次设定的最大展示角度；看完恢复 0°。窗框、玻璃与格栅随轴心移动。锁扣没有解锁动画，也没有物理约束。

## 后续制作边界

顶窗与后部接头仍为简化结构，整体为 Mark V-inspired 视觉解释，不是历史实物测绘复原。灰模含 **62,668** 个渲染三角面，精修样件含 **126,564** 个；微小零件占用较多，进入 VR 前需优化。当前未完成 UV／PBR 贴图烘焙和 LOD。灰模已于同日接入 Unity，完成简化碰撞体、抓取与复位的模拟输入验证，见[交互原型说明](../../docs/helmet-interaction.zh-CN.md)；精修前窗仍为独立 Blender 样件，未接入 Unity 交互。Blender 程序材质的效果不能直接视为 Unity 中的最终效果。

下一步在真实设备上检查灰模拿放手感，再确定前窗开合、手模型、游戏网格和材质烘焙的优先级；前窗导入时仍需核对铰链轴心与透明材质。

## 文件

- [v02 主文件](helmet-study-v02.blend)；[保留的 v01](helmet-study-v01.blend)
- [尺寸与逐对象网格检查](qa/audit-helmet-study-v02.json)
- [参考来源、尺寸依据和设计假设](../../docs/helmet-prototype-reference.md)
- [建模与处理脚本](scripts/)

| 画面 | JPG 预览 | 完整 PNG |
|---|---|---|
| 整体对照 | [查看](renders/v02/preview/01_overview.jpg) | [查看](renders/v02/full/01_overview.png) |
| 前窗细节 | [查看](renders/v02/preview/02_window_closeup.jpg) | [查看](renders/v02/full/02_window_closeup.png) |
| 前窗打开 | [查看](renders/v02/preview/03_window_open.jpg) | [查看](renders/v02/full/03_window_open.png) |
| 真实尺度灰模 | [查看](renders/v02/preview/04_graybox.jpg) | [查看](renders/v02/full/04_graybox.png) |

预览状态：四组 JPG／PNG 均已生成，四张小尺寸预览已逐一检查。日常对话优先读取 JPG（每张约 34–56 KB），需要交付或放大检查时使用完整 PNG。

导出文件：[灰模 FBX](exports/helmet-graybox-v02.fbx)、[前窗样件 FBX](exports/porthole-sample-v02.fbx)。两份文件均已在独立 Blender 进程中重新导入验证：尺寸最大差异低于 0.0001 mm，网格数量与源资产一致，没有混入摄影棚、灯光和相机；根对象已移回资产原点。前窗的 52 个子零件保持铰链层级，已检查 0°／75°／100°的位置变换。见[导出验证记录](qa/export-verification-v02.json)。这份记录仅覆盖 Blender 回读；随后灰模的 Unity 导入与拿放检查另见[Unity 验证记录](../helmet-interaction/qa/playmode-validation.json)。前窗运动碰撞尚未验证，FBX 不包含与 Blender 程序材质等效的烘焙贴图。

## 来源与复现

本轮几何、程序材质和处理脚本由 Codex 经 Blender MCP 辅助生成、修复并验证，未下载第三方网格。参考照片只作观察，不作为贴图或资产随仓库分发；来源及许可边界见[资源记录](../../docs/external-resources.md)。不能将这些产物声明为学生独立手工建模成果。

脚本 `01_helpers.py` 至 `04_studio.py` 最初在同一个 Blender Python 上下文中按顺序执行，后续脚本针对已保存文件运行。它们保留当时的本机输出路径 `E:/XRmuseum/artifacts/helmet-prototype`；换机器重跑前调整路径，并使用副本，避免覆盖保留版本。直接使用和编辑 `.blend`／FBX 无需重跑脚本。大文件由 Git LFS 管理，克隆后先执行 `git lfs pull`。
