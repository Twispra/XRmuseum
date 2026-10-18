# 2026-10-18 Phase 2 Windows 提交包

用户授权：基于最新版本构建 Windows 可执行程序，将完整提交 ZIP 放入 `phase2小组和个人作业/`。执行及文档整理由 Codex 辅助完成；本次构建不重新分配六位成员的历史贡献。

## 版本、环境与保留

- 开工主目录分支 `codex/phase2-technical-note-2026-10-18`，HEAD `b3da0a14556cf7f1ec3443370109d5c90557b920`；已有修改仅列出 `MuseumVR/ProjectSettings/ProjectSettings.asset`，所有原有未跟踪资料保留。
- 已 fetch，并在开工及交付复核时用 `git ls-remote origin refs/heads/main` 确认最新主线为 [6088c736e89d05101e09645c1796556012a6fd5d](https://github.com/Twispra/XRmuseum/commit/6088c736e89d05101e09645c1796556012a6fd5d)，包含 #67 左展台接入。
- 在独立、干净、detached worktree `E:\Codex\worktrees\phase2-windows-build-20261018\XRmuseum` 构建；未纳入日常工程的未提交设置。
- Unity **2022.3.62f3c1**，Windows x64 / Mono / Development；唯一场景为保存的 `Assets/Museum/Scenes/MuseumStart.unity`。本机为 Windows 11 26200 / Ryzen 7 5800H / RTX 3070 Laptop / 16 GB，并非 PQ604 教室电脑。
- `git lfs pull`、`git lfs checkout`、`git lfs fsck --pointers HEAD` 完成；进一步逐文件核对 **82** 项 LFS 资源实际大小及 SHA-256 与对应 LFS 对象一致，不以对象库完整代替资源展开检查。
- 原主目录设置构建前后 SHA-256 均为 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`；提交文件夹原有 **11** 个直接文件逐项核对未变，包括六篇个人文案及既有文档/PDF。

## 构建与打包结果

使用仓库现有 `Museum.PhysicalHands.EditorTools.MuseumPhysicalHandsBuild.RunBatch`，参数 `-batchmode -buildTarget Win64 -projectPath <隔离工程> -logFile <独立日志> -executeMethod <上述方法>`，设置 `MUSEUM_BUILD_OUTPUT` 为全新绝对 exe 路径、`MUSEUM_QA_OUTPUT` 为独立证据目录。不加 `-quit` 或 `-nographics`，未调用原型生成/重建工具。

Editor 进程 PID **43856** 于北京时间 **21:22:15–21:29:48** 运行，首次导入后实际 BuildReport 构建耗时约 **76.57 秒**。结果 **Succeeded / 0 errors / 2 warnings**，两条均为既有 OpenXR 可选 PoseControl / StickControl 迁移建议。进程自然退出 **0**、`forced=false`。构建入口恢复了原生 preloaded-assets 缓存；设置文件字节及场景列表保持基线。独立退出后复核 **1,299** 个工程源文件与导入前清单完全一致，工作副本仍干净。

完整运行目录输出至 `E:\Codex\Builds\XRmuseum\phase2-20261018-6088c73`。保留全部 **272** 个 Unity 构建产物（含 `_Data`、Mono、UnityPlayer、OpenXR 插件/清单、CrashHandler、附带调试资料等），加入启动说明和包清单后为 **274** 个文件。exe 的 PE machine 为 `0x8664`，且 SHA-256 与 BuildReport 一致。

最终提交文件：

`E:\XRmuseum\phase2小组和个人作业\COMP5424-Group2-Phase2-Windows-x64-6088c73.zip`

- ZIP 大小 **74,726,292 bytes**（约 74.7 MB）；完整展开 **221,178,824 bytes**。
- ZIP SHA-256：`3914fb87cb3e19742fc8f46b46747fcb7154478be56bb18b096b1cdbdfd4f102`。
- 所有条目 CRC 通过，无重复路径；解压到全新目录后，全部路径、大小和 SHA-256 与打包源逐项一致。
- ZIP 内单一顶层目录 `XRmuseum-Phase2-Windows-x64/`；`README-START-HERE.txt` 说明完整解压、OpenXR 环境、Grip 选择键、语言、展品操作、退出及重新开始参观；`PACKAGE-MANIFEST.json` 记录除自身外全部文件的哈希。

## 解压版启动观察与限制

从 ZIP 的全新解压目录启动 `MuseumVR.exe`，使用窗口化参数及独立 `-logFile`。在本机完成 **40 秒存活观察**，进程 `Responding=true`；日志确认 Mono、Unity 2022.3.62f3c1、D3D11 / RTX 3070、输入系统及 `MuseumPhysicalHandsRig.Start` 初始化，未发现意外 C# 异常、shader 错误或崩溃。启动后再次核对全部解压文件哈希未变。

本机现有 OpenXR 运行时为 VirtualDesktopXR；本次没有可用头显，日志记录 `XR_ERROR_FORM_FACTOR_UNAVAILABLE`、对应原生 OpenXR/Std Exception 诊断及 OpenXR Display 初始化失败等。不能写成日志零错误、VR 跟踪或交互通过。本轮未更改系统 OpenXR 运行时。

按隐藏窗口方式启动后，`MainWindowHandle=0`、`CloseMainWindow=false`，因此在观察结束并等待关闭后仅主动终止测试自有 Player PID **42024**，记录 `forced=true / exitCode=-1`。这不是 Player 自然退出或关闭功能验收；与构建 Editor 自然退出 0 分开记录。`Responding` 属性及初始化日志也不能代替画面/立体渲染检查。

**PQ604A/B/C 的实际软硬件、真实头显和控制器、VR 交互、立体渲染、性能、舒适性及可访问性均未在本轮验收。** 本次无用户研究；没有修改历史报告或个人贡献文案。

## 证据

- [最终核查摘要](../../artifacts/phase2-submission/20261018-windows-build/submission-validation.json)
- [Unity BuildReport](../../artifacts/phase2-submission/20261018-windows-build/windows-build-report.json)
- [Editor 进程及退出后源文件复核](../../artifacts/phase2-submission/20261018-windows-build/build-process.json)
- [完整打包清单、CRC 与解压哈希验证](../../artifacts/phase2-submission/20261018-windows-build/package-verification.json)
- [Player 启动、观察与终止记录](../../artifacts/phase2-submission/20261018-windows-build/player-smoke-process.json)
- [82 项 LFS 工作区实物哈希](../../artifacts/phase2-submission/20261018-windows-build/lfs-workspace-verification.json)

可复现脚本及启动说明随上述证据目录保留。原始完整构建/Player 日志、导入前后源文件清单及构建前设置备份保存在本机 `E:\Codex\Temp\phase2-windows-20261018\evidence\`，未混入提交用 ZIP。新证据和本记录为本地工作成果；本轮没有推送、创建或合并 PR，也未执行 Canvas 上传。
