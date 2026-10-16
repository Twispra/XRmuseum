# VR Help 与编辑器模拟器说明分离（2026-10-14）

## 范围与结果

基于 `origin/main` 的 `e5eebe0`，在独立分支 `codex/vr-help-guidance-2026-10-14` 修改头盔面板。Unity Editor 的 Help 和观察页底部继续提示模拟器按键；非 Editor 的 PC VR 运行环境改为提示手柄握把键选择、抓取／松开，以及选择展品 Reset 复位头盔。英文、简体、繁体已同时写入 JSON 维护源和保存的 `LocalizationTable.asset`。没有改主场景、Prefab、输入绑定、模型或构建设置。

本分支与尚未合并的 #51 独立；两者会修改同一个 `HelmetExplorationPanel.cs`，审查与合并时应核对该文件的最终组合行为。

## 本地验证

- 新 worktree 的 LFS 文件已展开；`git lfs ls-files` 显示实际资产。Unity Hub 用 2022.3.62f3c1 打开本分支 `MuseumVR`，载入已保存的 `MuseumStart`。
- Play 模式观察页显示 `Point + G to select`，即 Editor 仍保留模拟器提示。
- 最终文案调整后重跑 `Museum/Localization/Validate Localization System`：212 条文案，11 项检查通过，0 失败；本地报告 `artifacts/localization/qa/runs/20261014T115338853Z-validation/report.json`。
- 最终文案调整后重跑 `Museum/Localization/Validate saved main scene (Play Mode)`：状态 `passed`，86,050 项通过，0 errors / 0 warnings；运行时逐一打开三语 Help 并检查显示、字形及布局。本地报告 `artifacts/localization/qa/runs/20261014T115410136Z-integration-e5deccacc91845f387dfb7fa302d5a94/report.json`，三语截图在本次运行的 `tmp/trilingual-qa/` 时间戳目录。上述临时报告与截图未纳入 PR，防止大量生成文件混入提交。
- 测试时 HEAD 为 `e5eebe0`，另有本次三个待提交修改文件；本地化检查报告为测试期间生成文件。检查报告里的 `dirty` 字段是测试当时的快照，而不是最终提交状态。

## 限制与后续

`Application.isEditor == false` 的访客文案分支尚未通过 Windows 构建或真实头显验证；本次 Editor 测试只能验证模拟器分支与三语资源、布局。设备可用后应在 PC VR 构建中打开 Help，核对握把键与 Reset 的实际操作和头显内阅读距离，并检查与 #51 的视角重置入口并存。此记录不等于用户研究或真实设备通过。
