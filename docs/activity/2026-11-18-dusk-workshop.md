# 黄昏潜水工房首版

在 Unity 2022.3.62f3c1 中打开 [DuskWorkshop.unity](../../MuseumVR/Assets/Museum/Features/DuskWorkshop/DuskWorkshop.unity)，按 Play。样板基于默认展厅 MuseumStart，三个展位保留原来的位置与功能。进场先看中央头盔，再沿过道退至入口右侧回望。左窗、地面的连续长影与三座工作台可同时入画。窗边能看到停泊的船，靠近中央台侧能看到木板接缝、铆钉和铁带。

[全景](../../artifacts/dusk-workshop/first-edition/gallery.png) · [窗边](../../artifacts/dusk-workshop/first-edition/window.png) · [木台近景](../../artifacts/dusk-workshop/first-edition/joinery.png) · [原版与样板同机位对照](../../artifacts/dusk-workshop/first-edition/comparison.jpg)

## 光与材料

第一轮只有一跨窗，从入口看地面亮区几乎被展台挡住。第二跨向入口延伸 3.64 米，让窗影穿过可走的空地。两扇窗共用一套 3.20 × 2.26 米的模型。窗台、石拱、铁梃、扣件和铆钉均有实体几何。窗外是一块与窗框分开的静态港景，表现虚构的港务工房背景。

太阳从左墙以约 19° 仰角斜照。主灯随展品产生实时阴影，房间的间接光存入光照贴图，展台周围的光照探针服务可移动物体。室内反射探针记录窗与工作区，低位补光照出头盔前窗和铰链。深色珐琅面板给坐姿操作键提供稳定的文字背景，背墙圆环保留作轮廓参照。

三台共用一套开放式木饰面。实际比例的木板、不同粗糙度的钉头和铸铁护角形成近看层次。灰色地坪有错缝，墙面使用灰泥法线。窗和展台的 FBX 使用 Blender 后台生成，复用源在[建模脚本](../../artifacts/dusk-workshop/source/make_fittings.py)。材质来源和窗外图的 AI 辅助记在[资源台账](../external-resources.md)。

## 本机观察记录

- 产品提交为 f96ee9960ca9f2e637b9e55ff5624be146a0d061，基线为 ff786074。Windows、Unity 2022.3.62f3c1、URP 14.0.12、D3D11、RTX 3070 Laptop GPU。
- 基线与样板均以简体中文、单眼 Play Mode 采集九个固定世界机位，宽度 1600 像素。在独立编辑器进程重新打开已保存的样板后再次采集，八幅与末轮观察逐像素一致，台角图有三个像素的最大通道差为 1/255。采集脚本放在 [Editor](../../MuseumVR/Assets/Museum/Editor/DuskWorkshopCapture.cs)，机位、视场角和环境分别见[原版记录](../../artifacts/dusk-workshop/first-edition/baseline-views.json)与[样板记录](../../artifacts/dusk-workshop/first-edition/workshop-views.json)。
- 首轮烘焙提示部分细件光照 UV 重叠。最终版为窗与木饰面使用三维探针，墙体使用留有间距的六面光照 UV。末轮烘焙产出一套方向性光照贴图，日志未再报告 UV 重叠。17 个新 LFS 资源的工作区字节与提交指针 SHA-256 相符。
- 复看范围为远景、入口、坐姿、窗侧、木台转角、头盔特写与两侧展位。单眼截图可辨窗影、木板接缝、盔窗和原有中文操作区。设备未提供可用的 OpenXR 头显，双眼舒适度、帧时、构建及本轮三语实机操作仍待验证。

复查时在独立编辑器进程运行 `Museum.DuskWorkshop.Editor.DuskWorkshopCapture.Run`，传入 `-batchmode -force-d3d11 -projectPath <工程> -duskScene Assets/Museum/Features/DuskWorkshop/DuskWorkshop.unity -duskOutput <新的空目录> -logFile <日志>`。采集器会等待 Play Mode 并退出。
