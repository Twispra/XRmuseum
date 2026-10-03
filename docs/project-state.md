# 项目状态与下一步

**2026-10-03 探索功能发布交接：** 已发布 [PR #7](https://github.com/Twispra/XRmuseum/pull/7)，待审查。`codex/helmet-exploration-2026-10-03` 提交可选 AIR / VOICE 探索、16 秒设备模型动画、提示与回顾，源码 `c2bf63c`。351 项动画验收的源文件仍匹配；发布前新跑原观察 207 项、原拿放 90 项均通过，错误 0、实际退出码 0。探索 PR 依赖未合并的[环境 PR #5](https://github.com/Twispra/XRmuseum/pull/5)，以环境分支为 base；先合并环境、再将探索 PR 调整到 main 并审查。没有自动合并。[版本、证据和接手步骤](activity/2026-10-03-exploration-release.md)。真实头显、用户评价与当前构建仍未验证。以下记录保留各阶段当时的状态。

**2026-10-03 供气/通话模型动画：** 在同一探索分支继续完善16秒分段演示，增加双轮双缸泵、软管流动、电话箱和有线双向波列。最终351项检查通过、实际退出码0；本轮635个原文件未变，主场景和原资产未再次改写。[最新实现与交接](activity/2026-10-03-exploration-model-animation.md)。此版本仍仅本地，未推送；真实设备的清晰度、舒适性和性能未验证。

**2026-10-03 自由探索本地实现：** 环境已上传 [PR #5](https://github.com/Twispra/XRmuseum/pull/5)，尚未合并；后续 [Issue #6](https://github.com/Twispra/XRmuseum/issues/6) 在 `codex/helmet-exploration-2026-10-03` 依赖其 `39bebc3` 增加同一面板的可选 AIR / VOICE 探索、按需提示和随时回顾，当前尚未推送。最终新增 282 项及原拿放 90 项检查通过、实际退出码 0；原主场景 207 项断言通过，但该轮批处理退出超时，不能写成全部批处理成功。[交接与证据](activity/2026-10-03-helmet-exploration.md)说明已修问题、退出异常和真实设备验证缺口。

**2026-10-03 发布前复测：** 展厅源码 `342cdda` 的 49 项环境、207 项主场景交互及 90 项原拿放检查全部通过，10 次拿放循环完成。交付分支为 `codex/museum-atmosphere-2026-10-02`，对应 [Issue #4](https://github.com/Twispra/XRmuseum/issues/4)，尚未合并 main；[本次证据与交接](activity/2026-10-03-gallery-release.md)包含 G 键选择说明、复跑入口和未验证范围。以下日期段落保留当时快照，不能据旧“仅本地”状态判断当前远程。

**2026-10-02 核验与本地展厅开发：** fetch 后的 `origin/main = 74556aadae39607b23b8ac2b8fd35e8042019393`，GitHub 确认 PR #3 已合并，包含前窗、可调剖面及其连续性修复。下方 9 月记录保留为当时快照。当前 [Issue #4](https://github.com/Twispra/XRmuseum/issues/4) 在 `codex/museum-atmosphere-2026-10-02` 增加现代海事展厅，直接接入原 `MuseumStart`，见[环境说明](maritime-gallery.zh-CN.md)和[本轮活动](activity/2026-10-02-maritime-gallery.md)。本段写入时环境成果仅在本地，尚未推送或合并；不要据此认为远程 main 已有新环境。目标平台、XR 输入和头盔运行时逻辑保持当前主线配置，真实设备、学习效果和最终构建仍待验证。

**2026-09-30 PR #3 评论修复（仍在功能分支）：** 已复现组员指出的剖切球柄跳变，修改为保留连续局部方向及完整手腕旋转；最新源码为 `0d31e426cb97c8cd838ebb8ac050d5874e24c3bb`，见[修复、测试与复查步骤](activity/2026-09-30-section-continuity-review.md)。此次没有再改主场景、Prefab 或输入配置。原 140 项验收遗漏了该临界情况，旧通过结果不代表问题不存在。[PR #3](https://github.com/Twispra/XRmuseum/pull/3) 等待组员复查，未自动合并；真实设备与构建仍待完成。

**2026-09-29 功能分支交接（尚未合入 main）：** `codex/helmet-inspection-2026-09-29` 基于 fetch 后的 `origin/main = 4fe725294150a39ceba411f0d5088b51697e85ee`，已将前窗开合和剖面接入 `MuseumStart`，并按近距离试用反馈增加可调方向/深度的剖切工具、个人跟随面板和单手悬停观察。沿用原 XR 摄像头和输入映射。当前成果见[新版功能说明](helmet-advanced-inspection.zh-CN.md)、[本轮活动记录](activity/2026-09-29-advanced-inspection.md)和[小组交接](helmet-inspection-handoff.zh-CN.md)。这不表示远程 main 已包含新功能；以下 9 月 23 日表格保留为历史快照，不能据此判断当前 PR 状态。

快照日期：2026-09-23。**先 fetch 并查看 GitHub，再判断当前主线。** 本文件随提交保存，不自动跟随远程变化；活动任务及负责人只在 [GitHub Issues](https://github.com/Twispra/XRmuseum/issues) 维护。

## 版本与交付状态

| 层次 | 本次核验的事实 | 对接手者的意义 |
| --- | --- | --- |
| 已集成主线 | `origin/main = 545229ec2e279cb3439cd9652cd606f61bb96d4e`，即 [PR #1 合并提交](https://github.com/Twispra/XRmuseum/commit/545229ec2e279cb3439cd9652cd606f61bb96d4e) | 主线已包含头盔原型、初始化防覆盖与验收状态修复 |
| 头盔实现与修复 | [PR #1](https://github.com/Twispra/XRmuseum/pull/1) 已合并，原 head `cf1029f`；修复代码 `3189006` / `f354815` 及操作说明已保留 | 可从最新 main 获取原型；[修复与验证范围](activity/2026-09-23-pr1-review-fixes.md)。合并不等于实机或课程验收通过 |
| 本轮协作改动 | [PR #2](https://github.com/Twispra/XRmuseum/pull/2)，分支 `codex/team-collaboration-2026-09-23` 已同步主线，目标为 main，待审查合并 | 剩余范围为根 AGENTS、状态/课程索引、Issue/PR 模板、协作说明和验证产物；无 Unity 产品文件变更。合入 main 后模板才对默认分支生效 |
| 仅本地环境差异 | 原工作区 `ProjectSettings.asset` 的 `preloadedAssets` 被清空，未随头盔提交 | 原头盔报告来自有此差异的环境。不得顺手提交、还原或把它当无害 UI 设置；需具体分析 XR 初始化后决定 |
| 可执行构建 | 仅有 9 月 22 日初始化构建记录；无头盔版新构建 | 旧 exe 不能作为当前头盔版本的交付证据 |

PR #1 的依赖已完成，当前剩余整合项为 PR #2。合并后应链接实际合并提交并更新此表；不要把“PR 可合并”或临时 merge SHA 当成已合并。本轮未代用户合并 PR #2，也未变更保护或审批要求。

## 当前目标与已做选择

课程方向是可访问的博物馆 XR 体验：让观众有目的地探索对象，工作人员能够启动、监督及复位。潜水头盔是我们当前选用的原型，不是官方必选展品；目标受众、具体学习目标和完整访客流程还需团队确认。[官方依据及边界](course-requirements.md)

| 已采用的项目选择 | 理由 / 边界 | 依据 |
| --- | --- | --- |
| Windows PC VR，Unity 2022.3.62f3c1，OpenXR / XRI 2.6.4 | 六人使用一致环境；暂未决定独立 Quest APK | [环境指南](getting-started.zh-CN.md) |
| 真实尺度灰模优先，先做单手拿放与可靠复位 | 验证核心交互后再投入精修；1.5 kg 是交互参数，非历史重量 | [当日决策与证据](activity/2026-09-23-helmet-prototype.md) |
| 精修前窗保留为独立 Blender 样件 | 尚未接入 Unity，不能宣传开合已经完成 | [模型范围与来源](helmet-prototype-reference.md) |
| 产品事实、原型假设与评估结果分开 | 不是实物扫描或经馆方确认的精确复原 | [来源记录](external-resources.md) |

新决定在对应任务/PR 写明原因及取代的旧决定，合并时更新本节或链接专项设计说明。不能根据旧聊天中的“假设方案”认定六人已同意分工。

## 验证覆盖与缺口

- 头盔 v02 几何 / FBX 回读通过；Unity 旧验收 89 项检查、10 次拿放通过，含 Home、实体 RESET、掉落恢复。[原报告和环境限制](../artifacts/helmet-interaction/qa/README.zh-CN.md)
- 旧报告没有记录当时 git SHA，不能事后伪造；新验证器会写独立运行目录、版本状态和输入哈希。本轮复测结果由[协作改动活动记录](activity/2026-09-23-team-collaboration.md)链接。
- 本轮在提交 `f354815` 的干净 worktree 上展开 LFS 后，通过 90 项检查、10 次拿放和两张静态预览；比原验收增加模型有效网格检查。该代码现已合入主线，记录来自此前的模拟测试，不能当作合并后新跑的测试或实机验收。
- 新 worktree 曾只含 LFS 指针，物理测试通过但预览失败；现已补充开工检查和网格断言。成员应检查实际资产已展开，不能仅凭 `git lfs fsck` 成功判断可运行。
- 未完成真实头显/手柄验收、完整官方模拟器键鼠人工测试、学习效果/舒适性/无障碍评价、当前版本 Windows 构建和真实参与者研究。
- 手模型、双手承托、Unity 前窗开合、烘焙/LOD/设备性能优化仍是候选工作，不是已经排定的六人任务。

## 建议的下一批任务（尚未认领）

1. 在已合入 main 的头盔版本和明确 XR 设置上进行真实设备检查；分析本地 `preloadedAssets` 差异，决定应保留哪种配置，并记录设备结果和对应构建。
2. 六人确认目标受众、一个完整学习任务、成功条件与可访问性需求，再拆成互不重叠的任务。先做能从进入、理解、操作到离开/复位的完整流程。
3. 按设备和学习流程反馈决定下一项功能；前窗、手模型、性能优化分别建任务，避免六个 AI 自动都去精修模型。

团队尚未提供六位 GitHub 身份或轮值整合人，未自动分配负责人、设置 CODEOWNERS 或修改分支保护。首轮共同采用机制时应确认整合人、每人一个当前任务、共享文件的写入顺序及官方资料访问渠道。

## 接手入口

[AGENTS](../AGENTS.md) → 本状态 → 对应 Issue / PR → [操作与协作指南](getting-started.zh-CN.md)。已有功能见[头盔使用说明](helmet-interaction.zh-CN.md)；历史工作见[周记录](weekly-activity.md)。不要从完整旧聊天恢复工程状态，也不要为预览而重跑生成脚本。
