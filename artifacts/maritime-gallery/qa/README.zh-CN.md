# 展厅环境检查记录

2026-10-03 已在产品提交 `342cdda` 上重新运行全部三组回归，见[最新发布交接](../../../docs/activity/2026-10-03-gallery-release.md)。以下保留 10 月 2 日首次实现证据。

最终产品源文件基于 `74556aadae39607b23b8ac2b8fd35e8042019393` 加本次尚未提交的环境改动；报告记录了当时的 dirty 状态及源文件哈希，不把基准提交写成已包含环境。日期为 2026-10-02，Unity 2022.3.62f3c1 / Windows Editor / Direct3D 11 / NVIDIA GeForce RTX 5070 Ti。

| 检查 | 结果与证据 |
| --- | --- |
| 改造前基线 | [报告](../../../artifacts/maritime-gallery/qa/runs/20261002T153037426Z-baseline-3eba0237c4dc4e93bbd88d96b5f057af/report.json)、[原组件快照](../../../artifacts/maritime-gallery/qa/runs/20261002T153037426Z-baseline-3eba0237c4dc4e93bbd88d96b5f057af/snapshot.json) |
| 最终环境、保存场景及资源 | 49 项通过；[报告](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/report.json)、[最终快照](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/snapshot.json)，九方向静态渲染 |
| 原拿放流程 | 90 项通过、10 次拿放循环；[报告](../../../artifacts/helmet-interaction/qa/runs/20261002T154050715Z-b0c0d916486c4b03b83aea28a777d49d/playmode-validation.json) |
| 最终主场景交互 | 207 项通过；[报告](../../../artifacts/helmet-interaction/qa/runs/20261002T154520664Z-inspection-main-6f9eb1775e9847158d624e76195d4a46/report.json)，覆盖前窗、可调剖切、单手悬停、跟随面板、复位和临界方向连续性 |
| 受保护资产及场景差异 | 105 个原依赖文件哈希相同；[逐文件及场景块记录](protected-assets-and-scene.json) |

拿放检查来自添加最后一处双面展签前的视觉版本。之后只给本环境增加无碰撞背板与背面文字；最终环境比较和 207 项主场景检查在该改动后重跑通过。自动 XR 输入通过不等于完整官方模拟器人工操作或真实头显测试。

## 画面

这些是原 `MuseumStart` 的 Unity 单眼渲染，不是效果示意图，也没有新增产品摄像头。广角图用临时相机展示整体空间；起始视角保留原相机位置。静态编辑状态的剖切柄和面板可见性与进入 Play 后不同。

- [改造前整体](../../../artifacts/maritime-gallery/qa/runs/20261002T153037426Z-baseline-3eba0237c4dc4e93bbd88d96b5f057af/02-wide-front.png) / [最终整体](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/02-wide-front.png)
- [起始视角](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/01-startup.png) / [坐姿](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/03-seated.png)
- [左侧](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/04-left.png) / [右侧](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/05-right.png) / [身后](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/06-back.png)
- [展品后方](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/07-exhibit-from-rear.png) / [抬头](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/08-ceiling.png) / [低头](../../../artifacts/maritime-gallery/qa/runs/20261002T154505385Z-validation-a4d5567aed3548278f886be05dfe6477/09-floor.png)
- [实际 Play Mode 起始状态](../../../artifacts/helmet-interaction/qa/runs/20261002T154520664Z-inspection-main-6f9eb1775e9847158d624e76195d4a46/00-startup-controls.jpg) / [剖面状态](../../../artifacts/helmet-interaction/qa/runs/20261002T154520664Z-inspection-main-6f9eb1775e9847158d624e76195d4a46/05-oblique-cut.jpg)

逐图检查了展签朝向、遮挡、墙顶完整性与原操作区。修正了侧柱挡字、主问题被头盔挡住、展台阴影杂点以及侧装置背面反字。文字在头显中的实际可读性仍待测试。

## 方法与限制

环境验收入口 `Museum.MaritimeGallery.Editor.MaritimeGalleryValidation.ValidateBatch` 不改写场景。首次基线入口 `CaptureBaselineBatch` 只可在未装环境时使用；已接入的项目切勿为重做基线而删除环境。新 clone 复查可将 `tmp/maritime-baseline-path.txt` 指向仓库保留的上述原组件快照，然后执行 `ValidateBatch`。

原组件签名比较了 395 个条目。仅忽略 ObjectReference 的进程内 `.m_FileID/.m_PathID` 子字段，父引用 GUID/层级仍比较；地面射线排除访客自身胶囊体；未激活 TMP 标签检查字体/材质，不要求其动态网格已生成。额外 YAML 审查确认原相机的 URP 配置等未修改，弥补签名比较器未覆盖该组件的限制。

环境为最终场景新增一个 Prefab 根；原来的两件非交互占位物仅停用。只有新前墙和顶棚增加碰撞体。原地面、头盔、RESET、XR 及物理组件均保留。环境标签由 TMP 生成临时 DontSave MeshFilter，场景中的空 added-component 记录与这些文字对应，并非缺失脚本；未改 TMP 包或删除其动态组件。

编辑状态资源统计约 105,750 三角面，207 个启用 Renderer，4 盏启用灯。它们包含整个原场景，不是单独环境成本，也不是帧率或每帧 draw-call 实测。装饰几何按组组合；暖色主灯与三个无阴影聚光灯、部分 Unlit 材质是当前取舍。

尚未验证真实头显双眼画面、GPU/CPU 帧时间、成人新手/较年长访客试用或最终可执行构建。当前机器没有可用的 OpenXR 设备运行时，测试使用模拟输入；不声称已接入 Quest 或达到目标性能。

本地 PowerShell 进程记录保存在 `tmp/gallery-20261002-*-process.json`，最终环境、拿放、交互检查均实际退出码 0。历史失败的检查器开发记录与中间渲染留本地，不覆盖旧证据。
