# 2026-10-17：PR #59 实体手选择修复与合并交接

用户明确授权修复实体手选择问题，验证通过后合并 [PR #59](https://github.com/Twispra/XRmuseum/pull/59)，同步本地并保留已有改动。基准为实际 main `bc9df77a56bf6dff18af9b25c528c82b49417ca4`，PR 原 head `f845031a59b04801b6215a85ed6949e656d0ef93`；工作在独立 `codex/pr59-direct-contact-fix-2026-10-17` 分支，不使用原目录编辑或验证。

## 问题与修复

原 Station 要求 Direct 掌心距目标表面不超过 5.5 cm，但物理手开掌包络会把掌心挡在约 13 cm 外。手套已到表面，首次 Grip 仍被拒绝；StateChange 输入不会在持续握持时自动补选。

- 新 `HelmetDirectContact` 从 Direct 已注册的筛选列表找到精确绑定的物理手，复用固定开掌包络与遮挡判定，不靠宽查询球直接放行，也不逐帧搜索场景。
- Station 的悬停、初次选择、部件优先级，以及按钮松手确认/移开取消统一判定。已抓住的连续手势仍可离开原触点继续操作；按钮取消没有使用“仍在选择”快捷放行。无物理手的旧 Direct 保留原距离，Ray 规则不变。
- 新回归复现另一处归属问题：包络外距仅 4 mm，但 LOS 终点命中显式属于目标的 `Bonnet Capsule`，代理组件在另一棵层级上，被误认为独立障碍。`MuseumPhysicalHand.HasLineOfSight` 增加精确的 `candidate.colliders` 自体排除；不推导或扩大到整个头盔 Rigidbody，独立遮挡仍拒绝。
- 新增 `HelmetDiegeticDirectValidation`，通过生产 Input Actions 和保存的物理手/Direct 路径验证。没有强制 SelectEnter/SelectExit、移除产品筛选或保存场景。

本次修复不改场景、Prefab、模型、公共 Input Actions、Packages、ProjectSettings、三语表或字体；新源码 meta 配套。此前 #59 累计场景/三语/UI 成果继续保留，操作说明同步修订。AI 辅助范围沿用 [资源记录](../external-resources.md)，没有引入新外部资源。

## 验证与版本

最终工程提交 **`4eb54903616b2199349276d7cb6978cf3eed3d77`**，Unity **2022.3.62f3c1**，保存的 `MuseumStart.unity`。测试时额外已跟踪 dirty 只有操作说明，另有此前本轮已知的未跟踪 QA 输出；产品代码/保存资产没有额外未提交差异。后续只补文档和证据，合并前核对其 `MuseumVR/` 与被测版本一致。

| 验证 | 入口 | 结果 |
| --- | --- | --- |
| Direct 生产输入专项 | `Museum.Diegetic.Editor.HelmetDiegeticDirectValidation.RunBatch` | **31/31**，捕获错误/异常 0，自然退出 **0** |
| 既有完整实体 UI 回归 | `Museum.Diegetic.Editor.HelmetDiegeticValidation.RunBatch` | **807/807**，捕获错误/异常 0，自然退出 **0** |
| 退出后工程指纹 | 全部已跟踪 `MuseumVR/` 文件 SHA-256 | **1,180** 文件，零差 |

命令使用指定 Unity 的 `-batchmode -projectPath <隔离MuseumVR> -executeMethod <上表入口> -logFile <独立日志>`，不带 `-quit`；PowerShell `Start-Process -PassThru -Wait -WindowStyle Hidden` 捕获实际 Editor 退出码。

[发布证据目录](../../artifacts/helmet-interaction/qa/runs/20261017-pr59-direct-contact-fix/) 保存全部正向断言、输入指纹、进程回执及负向过程。Direct 覆盖双手首次/再次抓取、移动旋转中心固定、松手停止、实际窗口和普通按钮的松手确认与移开取消、宽查询球内但包络外目标拒绝、独立实体遮挡拒绝、原知识热点优先；31 项不是 31 个独立用户案例。807 原断言保留，含重复数据检查。

独立只读子任务复核最终 runtime/test diff、Direct 31 项及 57 个报告输入文件哈希，全部与当前文件一致；没有把 Codex 辅助检查称为成员人工或实机批准。

### 负向过程

首轮 4 项失败后 Editor 退出卡住，超过 150 秒无新输出，仅停止对应测试进程；不能记作自然退出。第二轮增加几何诊断，确认 `openBoxOutside=.004`、唯一 LOS 命中为目标自身碰撞体，4 项失败并自然退出 1。修复自体归属后，第三轮物理接触已通过，但前侧触点同时覆盖原有前窗/知识入口，正确优先级拒绝了“空白外壳”夹具，仍保留失败并自然退出 1。将该夹具移到后侧空白 bonnet，保留所有断言，前窗/知识优先另有专项，最终 31 项通过。退出等待改为进入 Edit Mode 后继续更新 20 秒，与既有清理做法一致。

这些运行不累加到最终通过数量；原报告哈希、失败断言与版本见 `negative-history.json`，详细故障日志留本地。

## 合并、保留与同步

按用户授权更新同一 PR 后执行正常 GitHub merge，约束精确 head；不更改分支保护规则。最终 merged 状态、实际 merge SHA 和同步完成结果以 PR 元数据及合并后交接为准。本记录先保存已完成的修复、验证和同步方法，不将待执行动作冒充已完成。

原 `E:\XRmuseum` 开工为 main `bc9df77`，已有 `ProjectSettings.asset` 修改保留。同步前将其完整副本、working patch 和原有 **32,461** 个未跟踪文件的路径/大小/UTC mtime 清单保存至 `_local-backup/20261017-pr59-direct-fix/`；清单包括备份开始时已创建的两个本地备份文件，不声称逐文件内容哈希。设置 SHA-256 为 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`。

同步先检查传入路径与原改动/未跟踪文件无重叠、原目录无 Unity 进程；fetch 后只做 main fast-forward，不 reset、clean、stash 或覆盖原文件。同步后核对本地/远程 HEAD、LFS 实际资产、原设置 SHA、working patch 和全部原清单，结果存本地备份目录 `after.json`。新增本轮本地证据可保留，不把它们当作丢失原文件。

## 尚未验证与下一步

新增 Direct 只覆盖默认年代和所列触面，不等于三个年代全部角度的实手验收。真实头显、双眼阅读、可达性/舒适度、性能、本轮可执行构建与用户研究仍未验证；没有启动 Player。GitHub 无 CI 记录时不记作 CI 通过。#50 的设备调试及 #60–#64 后续任务继续保留，本修复不关闭它们。

合并/同步后可从原工程直接打开保存主场景体验；不要运行原型生成器。下一步设备检查应包括空手贴近外壳后首次握持、窗口点击、松手停止与移开取消，并分别记录设备结果。
