# 2026-10-08：PR #35 门锁截面审查修复

用户授权修复审查发现后审批、合并 #35 与 #36。本记录只覆盖 #35 的局部门锁截面问题，关联原任务 #25 / #33；原作者的模型、交互与历史来源交付保留，未将其贡献改记为本轮工作。

本轮基准为 #35 head `7b83a739f6e6e2583a9e6196212a5c90b75cfba9`，实际 main `afc3a9e56734d3c7230adb5b81a5dcdd7ddd016e`，本地修复分支 `codex/pr35-section-cache-fix-2026-10-08`。实际写入由本轮单一整合者进行，仅修改 `HelmetSectionSurface.cs`、`RefinedHelmetBinding.cs`、`RefinedModelValidation.cs`，没有修改场景、Prefab、meta/GUID、模型、XR 输入、Packages 或 ProjectSettings。

## 行为与修复

原截面 LateUpdate 早于门锁转动，却已缓存本帧前窗角度；瞬时开窗后角度不再改变时，局部截面会停留在旧姿态。截面现在在执行顺序 90 更新，晚于绑定组件的门锁顺序 80。门锁 release 累积变化还会使截面缓存失效，从而恢复已有 Editor 调用者提前请求 Refresh 的情形；保留与原前窗 `.05°` 相对应的变化阈值及静止缓存复用。

验收器新增同一生产截面数据与当前源矩阵的几何比较，覆盖 X/Y/Z 三方向闭合基准、停在 5°、减少动画的开/关窗，以及提前公共 Refresh 后的恢复；观察真实自然 Play 帧，未手动调度 LateUpdate 或写私有字段。

## 本轮验证

- 被测产品提交：`00ed3cbcc36a4475948185d58feefc8e86998339`。隔离副本：`C:\Users\zhaoooooo\.codex\worktrees\pr35-pr36-review-2026-10-08\XRmuseum`；旧 #36 暂存组合已撤销，测试的是本 PR 独立修复。额外未提交内容为本轮自制 Editor 审查探针、对应 meta 和审查/报告输出；具体路径与产品输入 SHA-256 见报告。未纳入这些探针或旧审查文件。
- Windows / Unity **2022.3.62f3c1**；开测前 17 个 Unity LFS 文件已实际展开。启动：`Unity.exe -batchmode -projectPath <隔离工程>/MuseumVR -executeMethod Museum.RefinedModels.EditorTools.RefinedModelValidation.RunBatch -logFile <本地独立日志>`；未加 `-quit` 或 `-nographics`，未执行任何 Builder/Install。
- [独立运行报告](../../artifacts/helmet-interaction/qa/runs/20261008T131738463Z-refined-integration-cd627727a09f463e93df6085b9c4ce05/report.json)：**135 项通过，失败 0，报告错误 0，实际进程退出 0**。保留原 113 项精细整合覆盖，新增 22 项局部门锁与缓存回归。所有门锁截面与当前生产数据/变换生成的顶点与索引吻合，顶点误差为 0；静止帧未重复重建。
- 本轮保存 14 张实际单眼诊断截图；检查了精细模型可见和结构截面。原拿放/前窗模拟 XR 输入、三个年代、演示、声音开关、三语与复位仍通过该验收器。没有声称重跑原作者的全部六组验收。
- `git diff --check` 通过；新的证据目录独立于原报告，旧证据未覆盖。[进程记录](../../artifacts/helmet-interaction/qa/runs/20261008T131738463Z-refined-integration-cd627727a09f463e93df6085b9c4ce05/process.json)。

## 交接

本轮代码由 Codex 在用户授权下辅助修复与审查，无新增外部资源、模型或历史事实。真实头显、人工历史/外观审阅、坐姿单手可达性、参与者、声音试听、目标设备性能及当前可执行构建仍未验证，#25 / #33 的剩余工作不因此关闭。

修复和独立回归已完成；审批/合并是否完成以 GitHub 的实际记录为准。#36 在另一隔离分支修复和验收；两项组合后的验证由后续交接记录提供，不将本报告称作组合测试。
