# 2026-11-20 面板按钮扳机输入

基于 `main@5ddaab5` 在分支 `unify-panel-input` 完成。改动让 `HelmetExplorationButton` 同时响应 XRI 的 Select 和 Activate 事件，因此原有握把选择保持不变，射线悬停时也可以使用扳机。两个事件共用原有的延迟派发标记；它们在同一交互更新中到达时只会在下一次 `Update` 派发一次。

## 范围

- 修改 `MuseumVR/Assets/Museum/Features/HelmetExploration/Runtime/HelmetExplorationButton.cs`：注册和移除 `activated` 监听，并让 Select 与 Activate 共用一次派发逻辑。
- 修改 `MuseumVR/Assets/Museum/Features/HelmetExploration/Editor/HelmetExplorationValidation.cs`：保留原有握把回归流程，并新增通过生产射线交互器和模拟扳机打开 Explore 页的检查。
- 未修改 `MuseumStart.unity`、Prefab、Input Actions、Packages 或 ProjectSettings。

## 验证

- `git diff --check`：通过。
- 使用 Unity 2022.3.62f3c1 自带的 Roslyn 编译器重新编译 `Assembly-CSharp`：通过，0 错误。
- 使用同一编译器重新编译 `Assembly-CSharp-Editor`：通过，0 错误；仅有仓库原有的 2 条 `IsSelectableBy(XRBaseInteractor)` 弃用警告。
- 本环境启动 Unity 2022.3.62f3c1 批处理时停在编辑器进程入口，连 `-version` 都没有输出，因此没有生成新的 Play Mode 报告。需要在可正常启动该编辑器的环境中运行 `Museum.Exploration.Editor.HelmetExplorationValidation.RunBatch`。
- 未进行真实头显验证。

## 复审修订与证据

在隔离工作树复现了原 `HelmetExplorationValidation.RunBatch` 的失败。默认实体站会隐藏旧面板，验证先在旧 INSPECT 的可见性检查失败，无法跑到扳机步骤。
新增断言改放到 `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch` 已有的旧面板夹具。它会测试默认实体站、暂时启用旧面板，然后恢复原场景状态。

Windows / Unity 2022.3.62f3c1 验证 `27e47ca`。**131,433/131,433** 通过，进程自然退出 0，1,446 个项目追踪文件哈希前后零差，已核对 43 项展开的 LFS 文件。新增 10 条中，扳机和握把均通过生产射线打开 Explore，分别收到一次 Activate 和 Select。恢复默认实体站和三语检查也通过。原按钮源码的独立回调夹具另验证了同轮双事件只派发一次。

遥测在 `session_start.detail` 写入 `legacy_exploration_input=2`，离线分析按标记识别扳机候选，并合并同一目标同刻的双事件。原始计数独立，旧日志保留原有判定。分析自测 **38/38**，同源遥测写入夹具 **20/20**；七份既有自测日志重跑结果一致。Unity 一轮关闭了会话 CSV，新增日志标记由独立源码夹具验证。真实头显和 Player 待验证。
