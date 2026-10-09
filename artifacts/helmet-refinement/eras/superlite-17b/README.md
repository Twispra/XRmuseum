# SuperLite 17B 细节与外观研究件

本目录记录 Kirby Morgan 现行 SuperLite 17B（拟采用 P/N 500-010，通信 posts 版本）的**独立面窗与侧阀体研究首版**。它尚未与主体作者确定整体比例或接口，也未进入 Unity。产品图片只用于观察，未放入仓库或作贴图。

## 看首版

| 分件 | 正面 | 斜前方 | Blender 源 | GLB |
| --- | --- | --- | --- | --- |
| 面窗、压圈、密封圈、外露鼻夹旋钮 | [图](previews/SL17B_FacePort_front_v1.png) | [图](previews/SL17B_FacePort_threequarter_v1.png) | [源](sources/parts/SL17B_FacePortAssembly_v1.blend) | [导出](exports/SL17B_FacePortAssembly_v1.glb) |
| 侧阀体外壳与两个可见控制钮 | [图](previews/SL17B_SideBlock_front_v1.png) | [图](previews/SL17B_SideBlock_threequarter_v1.png) | [源](sources/parts/SL17B_SideBlockControls_v1.blend) | [导出](exports/SL17B_SideBlockControls_v1.glb) |

Blender 4.5 LTS 可分别打开两个分件源文件。两个 GLB 可在支持 glTF 的查看器中分别打开；它们以各自原点保存，**没有拼成整顶头盔**。预览中的灰色面窗背卡仅用于让透明件可辨认，名称以 `REVIEW_ONLY_` 开头，未导出。预览不是实际装配照片。

面窗、侧阀体尺寸与螺钉位置都按图像观察估算，仅用于审阅轮廓。资料、参数与待协调接口分别见 [依据](references/faceport-exterior-v1.zh-CN.md) 和 [参数](references/faceport-exterior-parameters-v1.json)。贴图是本项目脚本原创的基色与粗糙度表现，打包在两份 Blender 源文件内；不是厂家颜色/材质测量结果。

可复现脚本位于 `scripts/`，**只对新建的空输出目录运行**，会拒绝覆盖已有源文件。人工改过的文件不要重跑脚本覆盖。资产回读结果见 [检查](qa/faceport-exterior-v1.md)。

## 待讨论

- 主体负责人仍待认领。正式尺寸、整体坐标、面窗安装曲率与侧阀连接点须共同确定后再改分件。
- 目前外观仍为概略研究件：侧阀体内部气路、旋钮功能、螺纹、鼻夹内构、壳体/颈圈、呼吸调节器及排气翼均未建立。
- 需要另一位成员对照同一 17B 版本审阅后，才能决定是否汇入资产分支；Unity 接入另行处理。

## 2026-10-09 维护者审阅后的进度

[PR #37 的审阅意见](https://github.com/Twispra/XRmuseum/pull/37#issuecomment-6063779730)指出 v1 面窗上下轮廓、visor 归属、侧阀壳体/旋钮布局与参数深度问题。Ereshkigal1224 已在同一 PR 回复，接受 **500-010 整盔资料收集**；主体与最终总装仍待其他成员认领。

- [整盔来源、部件关系、数据依据与视图缺口](references/whole-helmet-source-map-v2.zh-CN.md)；[官方照片链接—现有 v1 模型近似视角—v2 修正目标](references/photo-model-comparison-v2.zh-CN.md)。厂家图片只作链接。
- [v2 参数](references/faceport-exterior-parameters-v2.json)把压圈建模估计厚度定为 0.012 m，与 v2 脚本的 -0.011～+0.001 m 一致；这**不是实测尺寸**。`scripts/build_faceport_exterior_v2.py` 改为非对称面窗轮廓、不规则侧阀体与错位/不同轴控制钮，并从独立面窗件移除属于壳体的 visor 520-054。v1 资产和其原始报告保留。
- **v2 独立源、GLB 与四张预览已生成并纳入本轮交付。**微软商店版 Blender 4.5.14 可以构建并回读；[本地新运行记录](qa/2026-10-09-store-blender-v2.md)通过名称、UV、透明镜片、辅助物排除和压圈深度。[先前的启动故障记录](qa/faceport-exterior-v2.md)保留。厂家手册新证据将压圈螺钉数改为 11 枚；孔位/尺度仍为估算，外形和主体装配尚待审阅。[统一来源目录](references/README.md)区分事实、照片观察、建模估算及未知。

后续继续按 [照片—模型对照](references/photo-model-comparison-v2.zh-CN.md)修正轮廓和局部结构，找另一位成员审阅；正式比例、整体原点和接触曲面要同未来主体作者协商。本轮可审阅交付集中记录于 Draft PR #37。
