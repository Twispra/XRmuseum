# Unity Play 中头显视角与起始高度修复

任务：[Issue #48](https://github.com/Twispra/XRmuseum/issues/48)。用户报告通过 Virtual Desktop 连接后，Unity Play 的视角不随头显转动、初始视角贴地。由 Twispra 本聊天中的 Codex 辅助执行；未编造其他成员贡献或实机通过。

## 基准与范围

2026-10-13 开工原目录 `E:\XRmuseum` 为 main / `4578e6619a28c16828268f31f669a3e374fe2ade`；fetch 后 origin/main 相同。开放 #25 / Draft #28 属于独立建模，没有未合并 XR 功能依赖。原目录已有 `ProjectSettings.asset` 修改及未跟踪本地备份/报告，全部保留。使用独立 `codex/fix-hmd-tracking-2026-10-13` 工作分支，主代理负责代码与模拟器设置；两个只读子任务核对输入链和 VDXR 环境，其中一个随后只写新验证脚本，未并行写共享资产。

实际产品改动仅为新增 `Assets/Museum/Features/XRTracking/Runtime/MuseumXRSimulatorGate.cs` / meta，以及关闭 `Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` 的无条件自动实例化。场景、XR Origin、公共 Input Actions、官方样例 Prefab、模型、packages、ProjectSettings 和现有 GUID 均未更改。没有重跑生成器。另新增独立 Editor 验证及配套说明。

## 原因与修复

保存的设置原本在每次 Editor Play 自动创建官方 XR Device Simulator；其 Prefab 开启 `m_RemoveOtherHMDDevices`。项目实际 XRI 2.6.4 包的 `XRDeviceSimulator.OnEnable` 主动移除非 `XRSimulatedHMD` 的真实头显输入，`OnInputDeviceChange` 也移除晚连接的头显。主场景请求 Floor 模式，XROrigin 将 Camera Offset 高度设为零，而模拟 HMD 初始位置为零；这与用户报告的固定、贴地视角吻合。实际相机头部动作引用经 Unity 导入回读完整且已启用，没有据此改写绑定。

Gate 在 Editor 加载含 XR Origin 的起始场景后创建。运行中的 XR Display 或非模拟 XRHMD 存在时，保留真实设备，不创建桌面模拟器；无设备时在未激活的父对象下创建原官方 Prefab，首次 OnEnable 前关闭 `removeOtherHMDDevices`。晚连接头显时同步停用模拟层级并移除模拟设备，随后销毁临时对象；本次 Play 保持设备模式，短暂断连不重新接管视角。Gate 不写相机姿态、眼睛高度、地面校准或 XR Origin 位移。

本机读到的运行环境证据：Windows active runtime 已是 VDXR；19:05–19:07 的 Unity / VDXR 日志识别 Unity.exe、Meta Quest 3、两个 Touch 控制器、Virtual Desktop Streamer 1.34.22 / VDXR 1.0.10，Unity 达到 FOCUSED。没有证据支持更改用户的 runtime。该历史设备会话仍有 `xrEndFrame XR_ERROR_HANDLE_INVALID`，另有 accessibility.json 警告；这些作为待实机复查项保留，本任务未修改第三方配置，也未认定它们是视角问题根因。

## 验证与证据

被测产品与验证源码：`1690a524dbdcda349cfe4d94431a630cbae78029`。Unity **2022.3.62f3c1** / Windows / Editor；打开前展开并确认 MuseumVR 的 18 个 LFS 对象为实际资产，独立 Library 缓存位于 E 盘，未提交缓存。被测时额外 dirty 仅使用说明、外部资源记录和本任务本地 QA 输出，报告保留完整列表。

- 定向命令：`Unity.exe -batchmode -projectPath <独立 MuseumVR> -executeMethod Museum.XRTracking.EditorTools.MuseumXRTrackingValidation.RunBatch -logFile <独立运行目录>/tracking-02.log`。**23/23 passed，检查期间 0 errors，进程退出 0**；[完整定向报告](../../artifacts/helmet-interaction/qa/runs/20261013T114519023Z-xr-tracking-5090ec0e8be84cbeabadb01011722877/report.json)。首轮出现官方模拟器帮助 UI 在无 OS 键盘的 batch 环境访问空 controls 的启动异常，后续只为测试补齐合成键鼠，第二轮未再出现该异常；没有修改产品 UI 或样例。
- 正常 Dynamic Input 更新中添加 generic XRHMD 合成设备，证实设备不会被模拟器移除，模拟 HMD / 控制器立即释放；保存相机的 Position / Rotation / Tracking State 动作解析到该设备。站立 1.70 m、蹲下 1.10 m 与旋转输入准确响应；Device 偏移测试为 1.65 m，未重复叠加高度。XR Origin 保持原位置/朝向，断开不重建模拟器，启动时已有设备会跳过模拟。保存 MuseumStart 哈希不变。
- 原主场景交互回归：运行 `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch`，**83,156 项检查通过，检查期间 0 errors / 0 warnings，进程退出 0**；包含 **786 项热点检查 / 20 次 Direct 与 Ray 选择**。这些计数含重复三语文字/字形断言，不是独立用例总数。[主场景摘要与输入哈希](../../artifacts/helmet-interaction/qa/runs/20261013T114519023Z-xr-tracking-5090ec0e8be84cbeabadb01011722877/main-regression-summary.json)、[热点子报告](../../artifacts/helmet-interaction/qa/runs/20261013T114519023Z-xr-tracking-5090ec0e8be84cbeabadb01011722877/hotspot-interaction.json)。18.7 MB 重复原报告、启动日志和截图留在本地，摘要保留原报告路径及 SHA256，不冒充已提交完整原报告。验证模拟输入和现有交互，不代替实体设备验收。

定向高度检查是临时设置的 Floor / Device 偏移输入夹具，没有使用真实 runtime 的地面测量。批处理无已连接头显，OpenXR 启动阶段的 runtime 不可用信息不能冒充用户设备会话。首场景没有 XR Origin 后才加载 XR 场景、手动另放带破坏性设置的原版模拟器，不在当前保存 MuseumStart 的已验证启动路径中。

## 交接与设备复测

使用已保存的 MuseumStart，无需重建场景。在 Virtual Desktop / VDXR 连接 Quest 3 后进入 Play，检查初始站立高度、左右转头、俯仰、前倾和蹲下，再检查手柄与展品操作。Unity PC OpenXR 的 Play Mode Runtime 使用 System Default，沿用已确认的 VDXR。若仍有 frame submission 或追踪异常，记录新日志、runtime 版本、测试 commit 和实际设备结果。

验证后，确认原目录没有 Unity 进程、HEAD 仍为开工基准、源码补丁无重叠，将已测 `1690a52` 的 8 个 XR 源码/设置文件同步到 `E:\XRmuseum`，供用户直接进行设备复测。这是 main checkout 中未提交的本地镜像；权威修复分支仍只有 `codex/fix-hmd-tracking-2026-10-13`，没有提交到 main、合并 PR 或切换用户分支。原 `ProjectSettings.asset` 为空 preloadedAssets 的既有改动仍保留，前后 SHA256 均为 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`；其他既有本地输出未改动。[应用记录](../../artifacts/helmet-interaction/qa/runs/20261013T114519023Z-xr-tracking-5090ec0e8be84cbeabadb01011722877/local-application.json)。自动测试使用独立分支保存的 ProjectSettings，原目录保留的额外设置不是本轮自动测试版本的一部分。

真实 Quest 3 / 双眼、runtime 地面校准、键鼠人工操作、声音舒适性、性能、可执行构建及用户研究均未在本轮声称通过。AI 辅助与引用来源见[外部资源记录](../external-resources.md)，使用步骤见[协作指南](../getting-started.zh-CN.md)。软件修复的审阅与实机复测分别记录；未自动合并 PR。
