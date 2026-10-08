# 2026-10-08：PR #36 说明生命周期审查修复

用户授权修复审查发现后审批、合并 #35 与 #36。本轮只修复 #36 的说明过期/重新启用问题，基于 head `0d0be1003572d86b79f5ef2013bbe1d95638bb48` 和实际 main `afc3a9e56734d3c7230adb5b81a5dcdd7ddd016e`，工作分支 `codex/pr36-hotspot-expiry-fix-2026-10-08`。原作者的热点显隐和淡出实现保留，未重新归属其贡献。

`HelmetHotspotTour.OnEnable` 现在重用当前透明度，保留已过期状态、淡出中状态和剩余保持时间；有效重选仍恢复显示并重启计时，Reset 仍恢复初始提示。没有改场景、Prefab、meta/GUID、模型、材质、XR 输入、Packages 或 ProjectSettings。

既有真实 Play 生命周期 fixture 新增三项回归，每项覆盖组件和整对象禁用一帧再启用，检查说明文字和背景 property block 的 alpha；没有手动调用 OnEnable 或写私有字段。秒数由公共 AdvanceExplanation API 推进，未等待 31 秒真实时间。

## 验证

- 被测产品提交：`25c489f7386fe9f57e58c7803f17a90c2a685e29`；隔离副本 `C:\Users\zhaoooooo\.codex\worktrees\pr36-hotspot-fix-2026-10-08\XRmuseum`。开测 tracked 与 untracked 均干净，结束产品文件仍无额外改动。
- Windows / Unity **2022.3.62f3c1**；`git lfs checkout` 后 15 个 Unity LFS 资产实际展开。启动：`Unity.exe -batchmode -projectPath <隔离工程>/MuseumVR -runTests -testPlatform EditMode -testFilter Museum.Hotspots.Tests -testResults <独立结果> -logFile <独立日志>`。生命周期 fixture 通过 EnterPlayMode/ExitPlayMode 在实际 Play 环境运行。
- [独立 XML 结果](../../artifacts/helmet-interaction/qa/runs/20261008-pr36-expiry-fix/results.xml)：**18 / 18 通过，失败 0，实际进程退出 0**。保留原 15 项，新增过期、淡出中、剩余保持时间三项。覆盖重选、Reset、文字/背景 alpha 和组件/整对象两种真实重新启用。
- [进程记录](../../artifacts/helmet-interaction/qa/runs/20261008-pr36-expiry-fix/process.json)；`git diff --check` 通过。原作者的旧忽略输出和失败启动记录不覆盖、不作为本轮新跑证据。

## 交接

该修复由 Codex 在用户授权下辅助完成与审查；无新增外部资源。测试属于桌面 Editor，时间推进为公共 API，不是头显/手柄实测、人工舒适性、参与者或 Player 构建。测试日志包含无活动 OpenXR runtime 等环境提示，本记录只报告 XML 的测试结果和实际进程退出，不宣称完整环境无错误。

本轮独立修复与回归完成，审批/合并以 GitHub 实际状态为准；#35 精细模型接入及其门锁修复在另一分支独立验收。两项组合后的主场景检查另行记录，不把此结果当组合测试或历史外观审阅。
