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
