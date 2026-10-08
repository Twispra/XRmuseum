# 2026-10-08：PR #35 / #36 修复后的组合验收

用户明确授权修复、审批和合并两个 PR。#35 的局部门锁截面修复与独立 135 项验收见[单项记录](2026-10-08-pr35-section-cache-fix.md)，#36 的过期说明修复与独立 18 项验收见[单项记录](2026-10-08-pr36-expiry-fix.md)。原贡献与历史报告保留，未把 Codex 辅助检查写成真人设备或历史外观批准。

## 被测组合

- #35 head：`154bc3b744806d2fbf6040eb0e568db5c7ae0a37`；#36 head：`649819d644a994f8bc4cc3f7ae18f46718f771f2`。共同基准 main 为 `afc3a9e56734d3c7230adb5b81a5dcdd7ddd016e`，`git merge --no-commit --no-ff` 无冲突。
- 隔离副本 `C:\Users\zhaoooooo\.codex\worktrees\pr35-pr36-review-2026-10-08\XRmuseum`，测试 HEAD 为 #35，额外 staged 为上述 #36 全部变更。其他未提交项是已知自制 Editor 审查探针/meta 与测试输出，无未知产品修改；完整 dirty 与输入哈希随本地原始报告保存。
- 被测 index tree：`fa24287316e903209bac4f8ebdf18adc5719b7a9`；完整 `MuseumVR` 产品目录 tree：`8f9ef25d7a3ff6d9e9bf014117927a6a0b0a2eca`。本证据提交仅追加仓库外于 Unity 工程的报告/文档，不改变该产品 tree。

## 本轮实际运行

Windows / Unity **2022.3.62f3c1** / Direct3D 11，开测前 Unity LFS 资产已展开。均用 `Unity.exe -batchmode -projectPath <隔离工程>/MuseumVR -executeMethod <入口> -logFile <独立日志>`，不用 `-quit` / `-nographics`，没有 Builder/Install 或保存产品资产。

1. `Museum.RefinedModels.EditorTools.RefinedModelValidation.RunBatch`：**135 项通过，失败 0，报告错误 0，实际退出 0**。覆盖修复后的自然 Play 门锁截面与缓存、前窗/拿放模拟 XR 输入、年代、演示、声音开关、语言与复位。
2. `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch`：**80,999 项通过，失败 0，报告错误 0，实际退出 0**。包含[768 项热点检查](../../artifacts/helmet-interaction/qa/runs/20261008-pr35-pr36-combined/hotspot-interaction.json)，验证当前精细模型主场景中的直接/射线选择、语言/生命周期/进度/复位，以及三语保存字体、动态文案和原场景绑定。

[组合摘要与原始报告保留路径](../../artifacts/helmet-interaction/qa/runs/20261008-pr35-pr36-combined/summary.json)记录两个实际进程退出与范围。18 MB 的全量字体逐字通过项和详细日志留本地；保留有用的完整热点报告和产品树指纹，没有把旧报告冒充本次新跑，也没有扩大 Git 验证产物。

## 后续审批与合并

两个审查问题均已修复，独立与组合检查通过。按用户授权，接下来对实际最新 head 提交审批，依次合并 #35 和 #36，再核对真实 main 的产品 tree 是否与上述被测组合相同；这里没有预先写成已经合并。若一致，仍只是输入一致性核对，不是对合并 commit 重新启动 Unity 的声明。

真实头显/手柄、坐姿舒适性、参与者、历史外观人工审阅、声音试听、目标设备性能及当前可执行构建仍未验证；#25 / #33 的剩余任务不因本次整合合并而关闭。默认 Mark V 与另两年代占位模型等产品选择遵循 #35 的现有决定，本轮未改规则、平台或课程许可。
