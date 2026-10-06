# 项目状态与下一步

2026-10-06 主线整合快照：已 fetch 并同步文档 PR #22 的 main `7f1a2b8f548078eee50cd83051e86e68769a07b9`；本轮按用户要求通过 [PR #24](https://github.com/Twispra/XRmuseum/pull/24) 将 #19 成果整合到该基准，保留 #21。代码/保存资产已在 `2fcaad1` 完成六组验收，后续仅文档/证据变化；PR #24 的实际 merged 状态及 merge SHA 为最终主线依据。接手前仍应 fetch 和查看 GitHub。

当前 main 已包含头盔拿放、前窗开合、可调剖面与悬停、海事展厅、AIR / VOICE 探索、信息分层、模拟器空格误触修复、简繁英切换及三个观察热点的修复。本次 #24 将 #19 的透明前窗/声音/缓动、空腔截面、背部电话接头、收发强调和平滑复位补入同一保存主场景。年代模块仍未接入主场景。 详细贡献、问题处理与核对边界见[本次记录](activity/2026-10-06-project-records-audit.md)和[每周索引](weekly-activity.md)。

## 版本与交付状态

| 层次 | 已核实状态 | 接手注意事项 |
| --- | --- | --- |
| 已集成 main | PR [#1](https://github.com/Twispra/XRmuseum/pull/1)、[#2](https://github.com/Twispra/XRmuseum/pull/2)、[#3](https://github.com/Twispra/XRmuseum/pull/3)、[#5](https://github.com/Twispra/XRmuseum/pull/5)、[#7](https://github.com/Twispra/XRmuseum/pull/7)、[#9](https://github.com/Twispra/XRmuseum/pull/9)、[#10](https://github.com/Twispra/XRmuseum/pull/10)、[#12](https://github.com/Twispra/XRmuseum/pull/12)、[#13](https://github.com/Twispra/XRmuseum/pull/13)、[#14](https://github.com/Twispra/XRmuseum/pull/14)、[#18](https://github.com/Twispra/XRmuseum/pull/18)、[#21](https://github.com/Twispra/XRmuseum/pull/21)、[#22](https://github.com/Twispra/XRmuseum/pull/22) 的实际合并提交均为主线祖先；#19 本次整合见 #24 | 合并不等于真实设备、用户研究或课程交付验收 |
| 本次整合三语与热点 | #18 产品 a46587e；Aragorn-cyber 的热点接入 [d84c93f](https://github.com/Twispra/XRmuseum/commit/d84c93f352541949db246440adb857361ec80ecd)；#21 修复 4b01e6c，合并于 20:58 | #24 保存表 205 条三语文案、静态字体 536 字形；沿用默认英文。三个热点支持 Direct / Ray，修复状态复位、选择、三语及信息卡布局 |
| 独立年代模块 | #10 已于 08:52 合入 main，淡变修复已有 23 项定向验证 | 仍是独立 Prefab / 演示；三个年代只以占位材质颜色区分，未完成历史造型或主场景接线。[模块说明](helmet-eras.zh-CN.md) |
| #19 主线整合 | 原 [#19](https://github.com/Twispra/XRmuseum/pull/19) 于 19:08 合入功能分支；本次 [PR #24](https://github.com/Twispra/XRmuseum/pull/24) 基于 `7f1a2b8` 完成整合，原作者 Fttt1003 产品 `484c5d4` / 源 head `b61c13c` 与提交历史保留 | 逐项解决文案/TTF/SDF 冲突；保留 main #21 热点运行时与 Prefab、原 GUID。六组验收/退出 0；[本轮整合与证据](activity/2026-10-06-pr19-main-integration.md)，实际主线合并状态以 #24 为准 |
| 远程分支后续提交 | 年代分支 [65378b9](https://github.com/Twispra/XRmuseum/commit/65378b93e5155329c3f88eac23028e5fda2baa10) 新增回归探针，未进 main；旧热点分支仍存在 | 探针是否保留需团队判断。旧热点成果已经以 d84c93f 接入并经 #21 修复，不应再整份合并旧场景覆盖修复 |
| 已关闭但未合并 | [#11](https://github.com/Twispra/XRmuseum/pull/11) 因重复关闭；同等 README 目录内容已由 [b40eb9f](https://github.com/Twispra/XRmuseum/commit/b40eb9f652a3935595575e6a5ec0f287c09777b5) 直接进入 main | 不把 #11 的临时 merge_commit_sha 当成合并证据 |
| 原目录未提交资料 | 本次开工主 checkout 位于 `b4e1886`，ProjectSettings.asset 和未跟踪审查/活动文件保留 | 整合使用独立 worktree；原设置/资料未纳入 PR。合并后原目录只能在确认无路径重叠后安全快进；其他成员本机进展未知 |
| 构建 | 只有 2026-09-22 初始化 Windows 构建的历史记录 | 没有当前头盔/三语/热点版本的可执行构建证据；旧 exe 不能作为本阶段交付 |

上述 21:05 历史核对包含 14 个 PR、7 个普通 Issue；13 个 PR merged，其中 #19 目标为功能分支。之后仅文档 #22 已于 21:31 合 main，本轮新增 Issue #23 / PR #24。原审计计数保留为历史，不是当前开放任务总数；关闭记录也不代表所有产品待办完成。

## 当前目标与已采用选择

课程方向仍为可访问的博物馆 XR 体验：观众能够理解、探索对象，工作人员能够启动、监督和复位。潜水头盔为当前原型；目标访客、具体学习目标、成功条件和完整流程仍待团队确认。[课程依据及边界](course-requirements.md)

- Windows PC VR、Unity 2022.3.62f3c1、OpenXR / XRI 2.6.4；未决定迁移为 Quest 独立 APK。
- 灰模的尺度、1.5 kg 交互质量及动画参数须区分公开实物依据与原型假设。[模型依据](helmet-prototype-reference.md)
- #3 已用现有灰模实现 Unity 前窗开合；Blender 独立精修前窗样件仍未接入，不能混为同一成果。
- 新访客文字沿用[三语维护流程](localization-authoring.zh-CN.md)，同时维护 JSON、保存表、字体与静态/动态绑定；独立年代演示和开发叠层不属于已经补全的主场景访客文案。
- 共享场景、Prefab、字体及公共输入仍按 Issue 协调单一写入者。账号与贡献按实际证据记录；已核实五个参与账号，六人完整姓名映射和正式轮值安排仍待确认。合并者不自动取得原创贡献。

## 问题、验证覆盖与限制

| 问题 | 当前结论与证据 | 尚缺内容 |
| --- | --- | --- |
| 平滑返回与热点进度共存 / QA 截图后旧帧时间 | #24 新增生产输入下 1/3 热点访问状态回归，322 次共存断言通过；两次体验失败经 Editor 帧等待修复后 76 项通过，未改运行时或放宽断言。[本轮整合与证据](activity/2026-10-06-pr19-main-integration.md) | 真实握持/返回舒适性、其他视角遮挡和声音尚待人工设备检查 |
| 剖切球柄临界方向跳变 | #3 评论后复现约 39.6 cm 跳变；61db42c 修复，40/40 连续性及原交互/拿放通过。[修复过程](activity/2026-09-30-section-continuity-review.md) | 真实手柄手感、非均匀缩放和设备性能未验证 |
| 年代淡变取消 / 旧转换覆盖 / Mark V 文案 | [#15](https://github.com/Twispra/XRmuseum/issues/15) 已关闭；1734815 修复，23 项/退出 0。[证据](activity/2026-10-06-pr10-completion.md) | 独立模块结果，不代表主场景接入完成 |
| 字形误报与入口未实际绑定 | [#16](https://github.com/Twispra/XRmuseum/issues/16)、[#17](https://github.com/Twispra/XRmuseum/issues/17) 已关闭；实际字符校验、保存表、四组入口与字体已补齐，随后 #18 扩为三语。[模块](activity/2026-10-06-pr13-completion.md) / [场景](activity/2026-10-06-pr14-completion.md) | 繁体独立审校、头显阅读和空间遮挡检查仍需人工完成 |
| 热点颜色复位、近距误抓、英文残留、信息卡遮挡 | [#20](https://github.com/Twispra/XRmuseum/issues/20) 经 #21 关闭；12 项生命周期测试、381 项热点集成检查及原探索回归通过，四进程退出 0。[修复及报告](activity/2026-10-06-hotspot-repair.md) | 结果只覆盖记录中的保存资产、模拟输入和单眼截图，未排除其他视角的旧菜单遮挡 |
| Unity 批处理退出挂住 | 早期及 #18 一次运行出现退出异常；#18 改用 Editor 延迟退出包装后复测成功，#21 四进程也正常退出 | 只能说明指定后续运行退出正常，不能认定所有原生退出异常的根因已解决 |
| 历史截图元数据含机器绝对路径 | #2 合并后的 Copilot 评论指出，当前旧 capture.json 仍保留该路径；见[核对记录](activity/2026-10-06-project-records-audit.md) | 保留原报告；可另补相对路径说明，不覆盖历史产物 |

文档 #22 的审计只读并复核历史证据；本次 #24 在 Unity 2022.3.62f3c1 / Windows 实际重跑六组：体验 76、观察 206、探索 1486、主场景 84346 次断言、本地化模块 11、热点 EditMode 12，六进程均退出 0，267 个输入哈希匹配。主场景含热点 768 次（其中跨功能 322 次）及大量重复三语文本检查，不能相加或当作独立用例总数。[本轮整合与证据](activity/2026-10-06-pr19-main-integration.md)。此前 #21 的数值继续按其报告保留。GitHub Copilot 运行不是 Unity CI；未把 AI 初检或合并操作写成另一位成员 APPROVED 批准。

真实头显/双眼、坐姿触达、舒适性、可访问性、用户学习评价、设备帧率、当前 Windows 构建和完整用户流程均未获得本次可核验证据。LFS 展开与有效网格检查仍适用于后续打开工程，不能凭对象库 fsck 或旧测试通过忽略模型导入问题。

## 待办入口与最小下一步

任务认领继续使用 [GitHub Issues](https://github.com/Twispra/XRmuseum/issues)，不另建总任务表。以下保留有来源的未完成事项及本轮整合交接；负责人未知时留待团队认领。没有因本次整合自动认领实机、课程提交或年代接入。

| 事项与来源 | 性质 / 依赖 | 最小下一步与验收目标 |
| --- | --- | --- |
| #19 主线整合交接 | 本轮已完成冲突整合与六组验收；Issue #23 / PR #24 | 核对 #24 实际合并版本、fetch main 并按 Issue 释放共享路径；使用已保存场景进行实机/试听与完整流程检查，不再重复整份合并旧 #19 分支 |
| 实机完整流程与当前 Windows 构建 | 课程要求及既有记录中的验证缺口；先确认稳定的整合版本、目标设备和 XR 设置 | 团队认领设备验证，绑定 commit 记录进入→理解→操作→回顾/离开→工作人员复位；提供对应构建、设备/runtime与结果 |
| 繁体语言审校、阅读/布局/声音舒适性 | #18/#19/#21 已明确限制；#24 整合交付后仍需声音人工检查 | 由实际成员人工检查术语、三语换行及不同视角/坐姿可读性，记录问题与版本；不以字体覆盖推断体验合格 |
| 年代接入与历史造型 | #10 仍为独立占位模块；主场景接线与造型未接受为本次整理任务 | 先由团队决定是否纳入学习流程，明确历史依据、资源及验收，再建任务；判断分支探针 65378b9 是否适合保留 |
| 历史证据可移植性 | #2 评论仍有未处理项；旧报告不得覆盖 | 建议另补相对入口映射和缺失本机日志说明，检查 clone 后可访问证据；优先级为本次 AI 建议 |
| 课程资料、Canvas 最新 AI 政策及教师访问 | [课程依据索引](course-requirements.md)中的未核验事项 | 有课程权限的成员核对正式要求及访问安排；本次无法从 GitHub 推断 Canvas 已提交或 AI 使用已获批准 |
| 受众/学习目标、手模型/双手承托、精修与性能 | 既有候选方向，未形成已认领任务 | 先确认受众、成功条件和设备反馈，再取舍；不自动分配六个人或把候选写成需求 |

## 历史与接手入口

此前逐段“仅本地 / 待审查 / 准备合并”的状态保存在[本次整理前的状态页](https://github.com/Twispra/XRmuseum/blob/18d7f8a09c29bb0c757b8a80bdc245b0fe40b785/docs/project-state.md)，原活动记录和验收报告未覆盖。日期快照只描述当时状态，当前结论以本页上方核对版本为准。

[AGENTS](../AGENTS.md) → 对应 Issue / PR → [协作指南](getting-started.zh-CN.md) → [每周记录](weekly-activity.md)。日常打开已保存的 MuseumStart，不重跑旧生成器。当前分支保护的管理员回读接口在本次返回 403，无法独立确认最新设置；本机 19:43/19:51 的配置记录仅是当时本地证据，不能据指南旧句断言远程仍未启用保护。本次未更改任何保护或账号权限。
