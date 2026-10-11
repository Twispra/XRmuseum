# 2026-10-10：Phase 2 Windows 构建与技术说明

对应课程要求：Phase 2 需要可运行构建、完整源码、教学团队可访问的仓库，以及不超过四页 A4 的技术说明（`docs/course-requirements.md`）。
本轮记录作者当时的本地构建结果，并提供技术说明 Markdown 草稿。正式构建交付、教学团队访问及 A4 排版页数尚不能由此记录核实。

**2026-10-11 审查修订说明**：下方构建时间、退出码、体积、哈希与运行结果保留为原作者自述，本轮没有重做构建或启动播放器。原始构建／播放器日志和完整产物未提供可访问的独立复核入口；技术说明尚未排版确认不超过四页 A4。当前产品的年代切换包含精细／占位几何切换，完整流程与无障碍效果仍待目标设备核验。[本次修订与验证](2026-10-11-pr38-technical-note-review-fix.md)

基线：`main` @ `270c7b72`（2026-10-08 合并 #35/#36 之后）。工作分支 `codex/phase2-build-and-note-2026-10-10`。

## 做了什么

1. 将本地工程从 2026-10-07 的 `afc3a9e` 快照同步到 `270c7b72`，纳入 PR #35（精细 Mark V 与供气／通话设备接入主场景）与 PR #36（热点可见性与说明淡出）。
2. 核验构建前提：`ProjectSettings/ProjectVersion.txt` = `2022.3.62f3c1`；`EditorBuildSettings.asset` 中 `Assets/Museum/Scenes/MuseumStart.unity` 为唯一启用场景且位于第 0 位。原记录称 61 个 Git LFS 资产已展开，并记录 `git lfs fsck --pointers HEAD` 退出 0；该命令本身不能证明工作区已展开，本轮未取得当时逐文件展开检查输出。
3. 以 Windows 播放器批处理产出一份可运行构建，并在目标机器上实际启动该产物。
4. 撰写技术说明 Markdown 草稿：[`docs/phase2-technical-note.zh-CN.md`](../phase2-technical-note.zh-CN.md)；A4 输出页数尚未确认。

**未修改任何 `MuseumVR/` 产品文件**，未改场景、Prefab、meta/GUID、模型、材质、XR 输入、Packages 或 ProjectSettings。本 PR 只新增文档。

## 原作者的构建与运行记录（本轮未独立复核）

- 被测源码：`main` @ `270c7b72`；提交时工作树除本地 `_local-backup/` 外干净。
- 构建：Windows / Unity **2022.3.62f3c1**（`1623fc0bbb97`，`2022.3/china_unity/release`），目标 `StandaloneWindows64`。
  命令：`Unity.exe -batchmode -quit -projectPath <工程>/MuseumVR -logFile <日志> -buildWindows64Player <输出>/MuseumVR.exe`。
  **退出码 0**，日志结尾 `Exiting without the bug reporter. Application will terminate with return code 0`；
  `error CS` **0** 条；仅 2 条 OpenXR 官方可选弃用建议。
  完整构建 **170.0 MB**，用户资产 95.4 MB（贴图 74.1%、网格 13.2%、动画 6.4%）；最大单项为
  `NotoSansSC-MuseumSubset SDF.asset` 16.1 MB。
  `MuseumVR.exe` SHA-256 `059c313352892718258a120b87bdc6fc7c23ce67181084abe14f86f204f5dddb`。
- 产物运行（目标机器实测，`Player.log`）：可启动并渲染主场景；OpenXR 子系统在播放器内装配成功
  （`UnityOpenXR` 匹配 1 个 displays + 1 个 inputs 描述符）；**XR 会话未建立**，报
  `xrGetSystem: XR_ERROR_FORM_FACTOR_UNAVAILABLE`（本机无头显）；键盘／鼠标输入已初始化。
- Unity 图形界面再次构建返回 `Succeeded`（19 s / 18543 ms）；因输入未变属增量构建，产物与上表一致。

**未验证范围（不得据此声称通过）**

- 因无头显，XR 会话未建立，**交互流程未在真机验证**：进入→理解→操作（拿放／开窗／剖面／年代切换／AIR-VOICE）→回顾复位未做真机走查；桌面模拟输入不等于真机。
- 手柄舒适性、坐姿可及性、目标设备帧率与用户研究均未验证。
- 年代切换包含可见几何及相关绑定切换；Mark V（索引 1）显示精细模型，年代 0／2 仍显示原占位几何与各自材质，另两年代的精细模型未接入。
- 构建环境备注：批处理构建期间 Unity 保存设置时清空过 `ProjectSettings/ProjectSettings.asset` 的 XR
  `preloadedAssets`，该文件已还原；播放器运行证据显示 OpenXR 子系统仍成功装载，因此「清空即不启用 VR」的判断**未被证实**。

## 交接

- 可运行构建与证据保存在仓库外本地目录 `_builds\MuseumVR-270c7b72\`（含 `MuseumVR.exe`、`MuseumVR_Data\`、`MonoBleedingEdge\`、`UnityPlayer.dll`）与 `_builds\player-launch-20261010.log`；构建产物按仓库约定不进 Git。
- 上述位置为原作者所报本地路径，未给出可供团队或本轮审查访问的完整位置。本轮无法由这些相对路径独立核验日志、产物与对应输入；后续应提供团队可访问的构建交付入口及必要日志／数据指纹，再完成正式交付和设备走查。
- 本轮为文档与证据交付，由 Codex 在用户授权下辅助完成：仅新增文档，未新增第三方模型、贴图、音频、字体或 package。
- 下一步最小动作：由拥有头显的成员用同一 commit 的构建完成一次实机流程走查，把设备、运行时与结果补入本记录或另开记录；`docs/project-state.md` 中「构建」一行仍停留在 2026-09-22 的历史记录，建议由主线整合人一并更新，本轮不改该共享状态页以免与进行中的修改冲突。
