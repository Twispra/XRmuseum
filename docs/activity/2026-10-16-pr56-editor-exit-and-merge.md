# 2026-10-16：修复观察验收的 Unity 退出异常并整合 #56

## 范围与依据

用户在 [PR #56](https://github.com/Twispra/XRmuseum/pull/56) 提交后，明确要求继续修复 Unity 退出异常，验证通过后合并。此次授权接续[上一阶段记录](2026-10-16-issue50-shell-grab.md)，旧记录中的未合并状态与退出失败证据保留为当时事实。

开工时实际 main 为 `6b6eefe`，#56 head 为 `fe4f2ac`；原 `E:/XRmuseum` main 有既有 `ProjectSettings.asset` 修改。仍使用独立 E 盘 worktree 和 `codex/issue-50-shell-grab-2026-10-16`，根代理唯一写入，子代理只读审查。未创建新 PR，没有修改场景、Prefab、模型、公共 XR/Input、Packages、ProjectSettings 或已有 GUID。新增源码范围仅为 `HelmetInspection/Editor/HelmetInspectionValidation.cs`。

旧入口两轮均已完成 207 项行为检查，却在原生关闭阶段卡住，必须停止对应进程，不能作为正常退出通过。对比仓库实现发现：观察验收在 `EnteredEditMode` 后的下一次 `delayCall` 立即调用 `EditorApplication.Exit`；其他正常退出的验证器会继续处理 Editor 更新，等待 20 秒。`TrilingualInspectionRegression` 已专门绕开观察入口的立即退出路径，`HelmetExplorationValidation` 也记录过立即退出导致原生关闭挂起的情况。

## 修复

源码提交 `a384e7fde86b861350ffbba9ea199c48083ce03b` 将观察批处理对齐既有退出方式：进入 Edit Mode 后记录结果和最早退出时间，继续处理 Editor 更新；20 秒后，确认不在编译、资源更新或 Play Mode 转换中，再调用一次 `EditorApplication.Exit`。等待放在 `Active` 守卫之前，使用 Editor 时钟，不阻塞主线程。

每次 Begin 清理旧请求；非 batch 不安排退出；失败结果及负值映射为 1 的规则保留。最终源码 `e5655e6261e73ff815844ea371c5b66c9c340241` 将待退出码与 Editor 时钟期限写入 `SessionState`，避免等待期间脚本重载清空静态变量；期限使用 invariant round-trip double，实际请求退出前清码。该重载分支经过代码复审，本轮未额外注入脚本重载。报告增加等待秒数，日志记录开始等待及实际请求退出的 UTC。207 项断言、11 ms 输入节拍、合成设备清理和全部截图步骤保持原样，未用关闭 XR、禁用渲染、强制杀进程或减少检查来取得通过。

第二次复跑在按 Grip 前暴露了另一处既有夹具路径：`whole helmet held reset` 从上一次停放点直接返回左侧原壳体触点，手代理被实体挡住，追踪有效但误差 0.3307 m。该次只完成 73 项，按失败结果自然退出 **1**，没有原生关闭挂起；[负向记录](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/inspection-2-failure-summary.json)保留，不能当作 207 项通过。提交 `3a02e0978c8a29c0b3397826dece7f011e823646` 仅将这处单纯等待替换为已有 `Approach` 正常输入绕行，保留原触点、朝向、Grip 及 Home 全部断言，最后仍要求掌面误差小于 8 mm；不移动代理或展品来绕过碰撞。

该补足在第 3 轮完整通过，第 4 轮仍在绕行的 withdraw 段被阻挡（73 项、误差 0.0787 m、自然退出 1），见[第 4 轮负向摘要](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/inspection-4-summary.json)。静态复查发现更早的退手输入从壳体左面斜穿到前右方，而且没有确认代理到达；通用 `Approach` 仅围绕展品 collider 规划，并不保证避开全部环境。诊断提交 `f2bfd3e` 增加超时附近的实体碰撞体信息；其两轮均 207 项通过、自然退出 0，未再抓到失败，因此没有把具体障碍臆断为 UI 按钮或窗口。最终源码补齐双手诊断标签，并将放下后的退手改为沿与 `RootTouch` 相同的头盔外向轴撤离 0.35 m，增加原 `WaitForPalm` 实际到位要求，保留原 1.3 秒沉降、速度/抖动/穿透断言及下一次抓取触点。未修改生产碰撞策略或通用导航 helper。

## 验证与证据

环境为 Windows、Unity **2022.3.62f3c1**，实际入口仍为 `Museum.Inspection.Editor.HelmetInspectionValidation.RunMainBatch`，未通过外部包装器绕开入口。最终被测 commit 为 `e5655e6`；运行时没有额外 dirty 工程源码，只有本代理既有 QA 输出，运行期间仅补充文档。完整命令和 dirty 清单见本轮 process 文件。使用已展开 LFS 资产的 E 盘缓存工程，TEMP/TMP 为 `E:/Codex/Temp/20261016-pr56-editor-exit`。

- 第一轮完整 **207 项通过、errors 为空，进程自然退出 0**；记录包含 `SETTLING` 与 `SETTLED_EXIT`，未结束测试进程。[断言与退出摘要](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/inspection-1-summary.json)、[进程记录](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/inspection-1-process.json)。
- 中途第 2 / 4 轮是明确的夹具失败，均自然退出 1；第 3 / 5 / 6 轮均 207 项通过、自然退出 0。全部摘要分别绑定实际源码，没有把中途失败改写为通过。
- 最终源码的第 7 / 8 轮均完整 **207/207 通过、errors 为空、自然退出 0**，未结束或强杀测试进程。两个日志均记录进入 Edit Mode 后等待 20 秒再请求退出，进程总耗时分别为 135.60 / 130.46 秒。[第 7 轮断言](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/inspection-7-summary.json)、[进程](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/inspection-7-process.json)；[第 8 轮断言](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/inspection-8-summary.json)、[进程](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/inspection-8-process.json)。汇总见[本轮 summary](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/summary.json)。
- 两进程均退出后，1,139 个工程源文件与运行前指纹一致、tracked `MuseumVR` 差异为空，原工程 `ProjectSettings.asset` 的 SHA256 仍为 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`。[来源复核](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/final-source-verification.json)。后续提交仅文档/证据。

原普通壳体修复的完整主场景 84,730 项、物理手 182 项与 Windows64 Development 构建的结果仍分别绑定 `62c2994`，不改写为此次重新运行。对比其 1,139 个工程文件，仅此次 Editor 验收脚本发生预期变化，其余 **1,138 个文件逐字节一致**，Player 运行源码和保存资产未变。[前次证据适用范围复核](../../artifacts/helmet-interaction/qa/runs/20261016-pr56-editor-exit/final-prior-evidence-comparison.json)。本轮不重复构建；原构建仍绑定上述版本且未启动。

逐帧输入、原始报告和 Unity 日志留本地，本轮摘要保留全部行为断言、原报告指纹及真实进程退出码；历史运行目录未被覆盖。

## 交接与限制

本轮修复的是验收入口的退出编排；未取得原生调用栈，不将日志末尾的 Input System / 许可断开信息当作这些库故障的结论。最终合并状态和 SHA 以 #56 的 GitHub merged 回读为准。原工程的既有修改保留，合并后核对无路径冲突再快进同步，日常无需运行原型生成工具。

#50 整体设备调试继续开放；合成输入与正常退出不等于真实头显手感、所有外壳位置、性能、舒适性或用户研究通过。AI 辅助范围包括代码对比、最小退出编排修复、独立复审、自动复跑及交接，没有新增外部资源、包或工具依赖，也不替代成员人工批准。
