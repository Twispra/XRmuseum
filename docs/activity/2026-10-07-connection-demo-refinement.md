# 2026-10-07 · 供气／通话精细演示资产

任务 [#33](https://github.com/Twispra/XRmuseum/issues/33)，负责人 Fttt1003；基线 main `0c0aeee6337607bf9d8230e6575c7ed69fde75f1`。工作分支 `codex/connection-demo-assets-2026-10-07`。按用户要求独立制作设备、动画与声音候选，暂不接入主场景。

核查 1916 Navy 手册扫描页与三个博物馆／制造商来源，区分内缸径与外径、历史配置与估算。制作 Mark III 双缸泵、水面六电池电话装置、潜水员电话示意件；提供可编辑总源／分件、GLB、FBX、原始材质、三种原创合成音效及单 HTML 浏览器演示。演示沿 AIR / VOICE 两条正确接口路径，支持分步、暂停、重播、拖动、复位、设备内部观察、三语和低动态。

模型与动画通过 Blender 源运动／双格式回读／五时刻姿态核对；时间轴 3,282 断言通过；浏览器操作、三方案音效启停及最终画面已检查。证据与界线见 [本轮验证](../../artifacts/connection-demo-refinement/qa/validation.md)。没有把 AI 检查写成人工试听、用户研究或真实设备测试。`MuseumVR/`、场景、Prefab、meta/GUID、packages 和输入均未修改，因此未启动 Unity 作无关回归。

资产入口和认领见 [README](../../artifacts/connection-demo-refinement/README.md)、[合作说明](../../artifacts/connection-demo-refinement/collaboration.zh-CN.md)。仍需组员造型／史料审查、试听选择、性能优化与后续 Unity 适配。头盔观察副本来源 PR #32 / commit `674d5a6`，原源文件未改；未宣称 1916 设备与 1944 头盔有实物配套证据。维持独立 Draft PR，不合并 main；Issue 保留为合作入口。

Codex 辅助资料整理、原创程序几何／贴图、合成音效、浏览器动画、验证及交接。没有下载使用第三方模型／声音／贴图，未分发馆藏媒体或手册扫描件。工具 Three.js MIT 声明见资产 `preview/three-LICENSE.txt`；课程 AI 使用政策仍按小组已有要求人工核对。
