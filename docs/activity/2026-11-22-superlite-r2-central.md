# 中央展台的黄色 SuperLite 17B R2

在黄昏工房的中央展台，从默认 Mark V 选择一次“下一年代”，即可查看 500-010 双通信柱配置。黄色壳体、深色内眉、麦克风引线、阀组印字和两根通信柱来自同一套装配。固定面窗的侧开是展览用的观察动作。返回上一年代可继续体验 Mark V 的供气和水面通话演示。

## 来源与运行资产

本次基于 `main@333bb13c`，关联 [Issue #25](https://github.com/Twispra/XRmuseum/issues/25)。建模源保存在已合并的资产分支。运行模型从 [#94 最终 GLB](https://github.com/Twispra/XRmuseum/blob/8a7d319f407fea858b1f177c00b3279954e1543d/artifacts/helmet-modeling/superlite17b-r2-assembly-2026-11-21/SuperLite17B-R2-assembled.glb) 派生。可编辑的 [Blender 源](https://github.com/Twispra/XRmuseum/blob/8a7d319f407fea858b1f177c00b3279954e1543d/artifacts/helmet-modeling/superlite17b-r2-assembly-2026-11-21/SuperLite17B-R2-assembled.blend) 与两张印字贴图沿用原交付。

- 源 GLB SHA-256 为 `82de680095b45aa24b45b523510c244488394d7f84c6f32b9674a6295885e4bb`。用 Blender 4.5.14 运行[导出脚本](../../artifacts/superlite17b-unity-integration/scripts/export_r2_runtime.py)，产生[Unity FBX](../../MuseumVR/Assets/Museum/Features/SuperLiteModels/Models/superlite17b-r2-runtime.fbx) 及两张原始 PNG。
- FBX SHA-256 为 `f999c180c37653174eb484453e2611d9b8fe9f2ead7c4b740f331b9914d46ec6`。重新导入 Blender 后按材料核对了 173 个网格、265,244 个三角面及 795,732 个面角的位置、绕序和 UV。Unity 坐标下宽 0.34561 米、高 0.32726 米、深 0.34685 米。这是展示模型的尺寸。[部件与材质回读清单](../../artifacts/superlite17b-unity-integration/exports/r2-runtime-readback.json)
- [中央展品 Prefab](../../MuseumVR/Assets/Museum/Features/HelmetInspection/Prefabs/InspectableHelmet.prefab) 保留既有 GUID，第三档引用独立的 R2 材质、玻璃和剖面缓存。16 件观察面窗部件共用原交互铰点。正式场景及复用该展品的场景同步更新了观察点的表面引用。

## 接入验收

在两份干净的产品提交 `ce2fd1cdbf549d60b2c11452fd8a12f1f3a8ca80` 上，用 Windows、Unity 2022.3.62f3c1 和 Direct3D 11 运行保存的 MuseumStart。两个验证入口及逐项结果记录在[编辑器检查清单](../../artifacts/helmet-interaction/qa/runs/20261122-superlite-r2-central/editor-checks.json)。

- R2 专项通过 475 项。核对了逐件三角面、材质、两张铭牌和 32 个不透明材质色值。按展台指令验证三档往返、面窗及观察点联动、三个方向的剖切、35° 旋转与复位。手部接触缓存已重新烘焙，实际接触查询命中提手网格。图中依次为正面、侧后方、打开面窗和剖切内部。[Unity 实景四视图](../../artifacts/helmet-interaction/qa/runs/20261122-superlite-r2-central/editor-views.jpg)
- VisitorEraValidation 使用模拟 XR 输入，覆盖实际展台射线选中与释放、按住面窗时延迟换代、三语热点、抓取旋转和复位，1,194 项通过。正式场景、黄昏样板、实体手实验场景及水面通话样板各自重新打开后，三处观察点都指向新展品的镜片、壳体或颈封。
- 基线 `333bb13c` 的旧版全场景验证会在寻找旧 WINDOW 控件时停止。本轮使用上面两个入口验证当前展台。真实头显的触感、双眼观感和设备帧率仍待实测。
