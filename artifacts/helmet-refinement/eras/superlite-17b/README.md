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
