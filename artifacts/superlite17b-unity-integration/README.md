# SuperLite 17B v2：Unity 运行时派生资产

从用户接受的整盔侧视修正版 v2 派生，不覆盖保存源、已有分件和历史报告。用于 `MuseumStart` 的展台索引 2；默认 Mark V（1）与早期占位（0）保留。

| 文件 | SHA256 / 用途 |
| --- | --- |
| `../helmet-modeling/superlite17b-2026-10-11/revision-side-profile-v2/delivery/superlite17b.blend` | `54a21895b807a19ccc0345a25101cb5369c0a9c976d175a40a58a528a5b8b905`；已接受的保存源，未修改 |
| `exports/superlite17b-v2-runtime.fbx` | `febfddaf522c50b0044237998c7a661b1bbb1d1220fb07588cfc3e23f5718fda` |
| `../../MuseumVR/Assets/Museum/Features/SuperLiteModels/Models/superlite17b-v2-runtime.fbx` | 同一 FBX 实体，新增自身 meta / GUID，没有替换 Mark V |
| `exports/runtime-readback.json` | Blender 4.5.14 LTS 实际 FBX 回读；143 网格、50,268 三角面、UV 与有限顶点检查 |
| `qa/install-readback.json` | Unity 2022.3.62f3c1 保存总装；14 个移动窗件、142 个不透明网格剖面缓存、轴向及边界 |
| `qa/placement-calibration.json` | 原场景落稳根节点、展台顶面与 3 毫米间隙，只适用于原布局/物理配置 |
| `qa/asset-parity.json` | 两份 FBX 哈希、LFS 实体、482 个原对象 ID、原物理组件、meta / GUID 与未重存场景检查 |

`scripts/export_runtime.py` 为本轮专用工具，要求源哈希匹配并拒绝覆盖已有导出；以 `SL17B_InspectionWindow` 分组前窗，提供轴向和展示锚点。普通查看不运行导出或 Unity 安装器。

宽 0.282、高 0.314、深 0.325 米为 v2 展示估算。虚拟侧开不代表 17B 面窗的真实操作，通信锚点/提示为教学估计，未补齐真实 posts 接头。Unity 转移基本材质参数，没有烘焙程序凹凸/涂层。使用、限制、截图和最终测试见[接入说明](../../docs/superlite17b-unity-integration.zh-CN.md)与[活动记录](../../docs/activity/2026-10-11-superlite17b-unity-integration.md)。
