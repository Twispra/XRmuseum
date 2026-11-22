# 正式启动场景采用黄昏潜水工房

2026-11-22。旧 Windows 包的源码是主线 `7f5fdf4f`。#85 发布的黄昏工房当时是独立样板，Unity 构建列表和 Windows 构建脚本都打开 `Assets/Museum/Scenes/MuseumStart.unity`，因此旧包展示普通展厅。

本次把已接受的双高窗、港景、工作台、布光和反射接入原有 [MuseumStart](../../MuseumVR/Assets/Museum/Scenes/MuseumStart.unity)。正式场景保留原路径和 GUID，编辑器与 Windows 构建共用这个入口。其专用 [LightingData](../../MuseumVR/Assets/Museum/Scenes/MuseumStart/LightingData.asset) 绑定正式场景，并引用样板已有的烘焙贴图。自 #85 至上述基线，两个场景的已保存内容均无后续变更，导航、语音、字幕、参观回顾和低视角改进均来自最新主线的共享代码与资源。

在 Unity 2022.3.62f3c1 重新打开正式场景后，读回一套光照贴图、100 个光照探针及工房根对象下 28 个 MeshRenderer。资源依赖中没有样板场景或其 LightingData。从正式入口运行简中单眼 Play Mode，九个机位均完成采集。入口右侧回望可见双窗与窗影，中央坐姿视角可见头盔和展台。此项检查没有使用头显。

Windows 发行包需完整解压后运行 `MuseumVR.exe`。原包与新版以源码提交和文件名区分。场景美术及第一个样板的素材出处见[黄昏工房设计记录](2026-11-18-dusk-workshop.md)。
