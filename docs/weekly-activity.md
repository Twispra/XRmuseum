# 每周小组活动记录

本文件是每周汇总与贡献证据索引，不作实时认领表。实质工作保存独立 docs/activity 记录；任务与共享路径继续在 [GitHub Issues](https://github.com/Twispra/XRmuseum/issues) 维护。

历史核对：2026-10-06 21:05（Asia/Shanghai）核对主线 [18d7f8a](https://github.com/Twispra/XRmuseum/commit/18d7f8a09c29bb0c757b8a80bdc245b0fe40b785)，首次补齐原索引遗漏至今的工作，详见[核对范围与问题过程](activity/2026-10-06-project-records-audit.md)。周段按实际日历日期组织，不虚构教学周次、工时或个人成绩；本轮增补 10 月 7 日精细资产整合，旧条目保留对应历史阶段。姓名映射和完整六人名单仍待确认。

角色按证据分开：Twispra 是所有已合并 PR 的合并者，并有自己明确的初始化、模型/交互、补齐与热点修复工作；Fttt1003、nikoyeah830-wq、Aragorn-cyber 的原创实现保留各自归属。Ereshkigal1224 是 #12 PR 提交者，该提交的 Git 作者尚无 API 账号关联。同一成果的原作者、修复者、评审与整合者交叉链接，不算各人独立完成一套成果。相关实现/翻译/检查包含 Codex 辅助，官方样例和辅助声明见[资源记录](external-resources.md)。

## 2026-10-12–18（本轮增补）

- 10-16：[PR #58 合并复核与本地保留交接](activity/2026-10-16-pr58-review-and-merge.md)。用户授权复核无阻碍后正常合并；本轮仅文档交接，已测工程不变。原设置另存副本，未跟踪资料清单核对，最终merge／同步状态见PR回读及评论；独立AI复核不代表成员人工批准。
- 10-16：[Issue #57 新访客复位、年代说明与技术文档](activity/2026-10-16-visitor-flow.md)。本对话用户授权、Codex辅助；探索源码／文档／验收在独立副本分工后由根代理串行整合共享场景、表和字体。候选 `806993a` 的专项、主场景与新Windows构建通过，尚未合并；实机与用户研究边界及实际文件范围见活动，未替其他成员认领或推断贡献。

## 2026-09-21–27

| 实际日期 | 成员 / 角色 | 负责事项 | 完成结果与证据 | 决策 / 问题 | 后续最小步骤 |
| --- | --- | --- | --- | --- | --- |
| 09-22 | Twispra，初始化作者；Codex 辅助，含官方模板/样例 | 独立 Unity 工程、Windows PC VR / XR 配置、占位展厅和协作指南 | [cc7605e](https://github.com/Twispra/XRmuseum/commit/cc7605e765100200881e4a9eb62a10f265bccba1)；[初始化验收](initialization-checks.md)，当时 Windows Development 构建成功 | 初始化构建只有抓取方块，不含后续头盔；不是全组原创模板 | 按当前整合版本另建构建并绑定设备结果 |
| 09-23 | Twispra，模型/交互原作者与修复者；Codex 辅助 | 真实尺度灰模、独立前窗样件、网格修复、两份 FBX、Unity 拿放/复位及工具保护 | [#1](https://github.com/Twispra/XRmuseum/pull/1) 已于 19:41 合 main；[e7d819e](https://github.com/Twispra/XRmuseum/commit/e7d819e4d3aea1a0bb439385785a5606f0d7acc0)、[原型记录](activity/2026-09-23-helmet-prototype.md)、[修复迁移](activity/2026-09-23-pr1-review-fixes.md) | 原 89 项与后续干净/LFS worktree 90 项分开；10 次拿放、有效网格/预览通过，但该轮 Unity 原生退出挂住。精修窗件仍独立 | 真实设备验收，保留历史失败；不开生成器覆盖现有成果 |
| 09-23 | Twispra，协作文档/模板作者与技术初检；Codex 辅助 | 根 AGENTS、共享任务/PR 模板、版本化证据、活动/状态/课程索引 | [#2](https://github.com/Twispra/XRmuseum/pull/2) 已于 19:47 合 main [4fe7252](https://github.com/Twispra/XRmuseum/commit/4fe725294150a39ceba411f0d5088b51697e85ee)；[活动记录](activity/2026-09-23-team-collaboration.md) | 旧“#2 待合并”已由实时证据更新；机制不自动锁文件。合并后 Copilot 指出截图元数据绝对路径，至今仍保留 | 确认完整成员身份/轮值安排；另补历史证据相对入口，不覆盖报告 |

## 2026-09-28–10-04

| 实际日期 | 成员 / 角色 | 负责事项 | 完成结果与证据 | 决策 / 问题 | 后续最小步骤 |
| --- | --- | --- | --- | --- | --- |
| 09-29–30 | Fttt1003，功能作者/连续性修复者；Twispra，辅助审查与合并 | 现有灰模前窗、可调剖面/球柄、单手悬停、跟随面板，保留原 XR 流程 | [#3](https://github.com/Twispra/XRmuseum/pull/3) 于 09-30 19:50 合 main；[首次接入](activity/2026-09-29-advanced-inspection.md)、[评论修复](activity/2026-09-30-section-continuity-review.md) | 旧测试漏掉临界方向跳变；复现 24/40 失败，修后几何 40、观察 207、拿放 90 通过，指定三进程退出 0。独立精修前窗仍未用 | 真头显、坐姿/手感、完整流程及性能验证 |
| 10-02–03 | Fttt1003，环境原作者、回归/交接 | 海事展厅、地面/顶棚/展墙、展台及静态 AIR/VOICE 装置 | [#4](https://github.com/Twispra/XRmuseum/issues/4) / [#5](https://github.com/Twispra/XRmuseum/pull/5)，10-03 13:05 合 main；[环境活动](activity/2026-10-02-maritime-gallery.md)、[发布](activity/2026-10-03-gallery-release.md) | 环境 49、观察 207、拿放 90 通过/退出 0；此阶段装置仅静态，不算后续动画 | 实机阅读/坐姿、动线、性能和构建 |
| 10-03 | Fttt1003，探索/模型动画原作者 | 可选 AIR/VOICE、16 秒模型演示、按需提示、回顾、重播/停止、减少动画 | [#6](https://github.com/Twispra/XRmuseum/issues/6) / [#7](https://github.com/Twispra/XRmuseum/pull/7)，13:07 合 main；[模型动画](activity/2026-10-03-exploration-model-animation.md)、[发布](activity/2026-10-03-exploration-release.md) | 原型选择来源为 Issue 中用户已确认的自由探索方向；动画 351、观察 207、拿放 90 通过/退出 0，测试基准+dirty 产品通过哈希对应 c2bf63c；#5 原依赖已完成 | 验证学习目标、提示清晰度、真实设备舒适性 |
| 10-03 | Fttt1003，信息分层原作者 | 按需详情/工具/Help，演示期间背景文字避让 | [#8](https://github.com/Twispra/XRmuseum/issues/8) / [#9](https://github.com/Twispra/XRmuseum/pull/9)，10-05 18:47 合 main；[活动及证据](activity/2026-10-03-exhibit-information.md) | 1469 布局/探索、206 观察、90 拿放通过/退出 0；不累加成当前版本测试数，仍有其他视角布局限制 | 实机不同视角检查，后续文字遵循三语维护 |
| 10-03 | nikoyeah830-wq，README 目录作者 | 补 artifacts、.github 和 AGENTS 目录说明 | [b40eb9f](https://github.com/Twispra/XRmuseum/commit/b40eb9f652a3935595575e6a5ec0f287c09777b5) 已在 main；[#11](https://github.com/Twispra/XRmuseum/pull/11) 10-05 因重复关闭、未合并 | 两候选 README blob 相同；不把重复 PR 记第二份成果或合并 | 后续目录变化按实际成果更新 |
| 10-03–04 起步（修复/合并见下周段） | nikoyeah830-wq，年代及双语模块原作者；Codex 辅助 | HelmetEras 数据/占位材质/独立 Prefab；Localization 文案/事件/字体/独立演示 | [#10](https://github.com/Twispra/XRmuseum/pull/10)、[#13](https://github.com/Twispra/XRmuseum/pull/13)；[年代活动](activity/2026-10-03-helmet-eras.md)、[双语活动](activity/2026-10-04-localization.md) | 年代只以颜色区分、未接主场景；本地字体烘焙曾遇空图集/格式问题。后续修复与整合交叉引用同一成果 | 年代是否进入流程、造型与主场景接线由团队决定 |

## 2026-10-05–11（截至 10-07）

| 实际日期 | 成员 / 角色 | 负责事项 | 完成结果与证据 | 决策 / 问题 | 后续最小步骤 |
| --- | --- | --- | --- | --- | --- |
| 10-05 | Ereshkigal1224，PR 提交者；Git 作者显示名的账号映射待确认 | 模拟器 Manipulate Right 从空格改右 Shift，保留 Y/T/U/Esc | [#12](https://github.com/Twispra/XRmuseum/pull/12)，18:49 合 main [97b8810](https://github.com/Twispra/XRmuseum/commit/97b88102de3c296bff90590efce8e39784a454f7)；[活动](activity/2026-10-05-windows-simulator-input.md) | JSON/绑定核对通过；活动为初步用户反馈，PR 后补逐键 Play 确认但无日志/帧率；不推断所有卡顿已解决 | 完整设备输入/焦点路径验证，核实本人提交关联 |
| 10-05 | nikoyeah830-wq，原模块修复及入口接入 | 年代淡变取消/旧协程覆盖与接头文案、实际字符校验、入口绑定与整合工具编译修复 | [#15](https://github.com/Twispra/XRmuseum/issues/15)、[#16](https://github.com/Twispra/XRmuseum/issues/16)、[#17](https://github.com/Twispra/XRmuseum/issues/17)；1734815、8a32c83、6b1a7f0/d4f7fba；[年代修订](activity/2026-10-05-helmet-eras-review-fix.md)、[本地化修订](activity/2026-10-05-localization-fixes.md) | 原三处绑定碰到 #9 已隐藏标识；后续交给 Twispra 补齐保存资产/验收，不把这两阶段算两套独立功能 | 原作者修订与下一行整合交叉追溯 |
| 10-06 08:52–09:10 | Twispra，接手整合/补齐与定向验证；原作者 nikoyeah830-wq | 同步 #10/#13 主线；补 #14 四组真实入口、保存表/198 字符和 OFL 来源 | [#10](https://github.com/Twispra/XRmuseum/pull/10)、[#13](https://github.com/Twispra/XRmuseum/pull/13)、[#14](https://github.com/Twispra/XRmuseum/pull/14) 按顺序合 main；[年代 23 项](activity/2026-10-06-pr10-completion.md)、[模块 11 项](activity/2026-10-06-pr13-completion.md)、[场景 627/探索 1469](activity/2026-10-06-pr14-completion.md) 均退出 0 | #15/#16/#17 关闭并记录交接；默认英文及原隐藏策略保留。AI 复验不是另一位成员批准 | 实机/布局及语言审校，旧数字按阶段保留 |
| 10-06 09:58 | nikoyeah830-wq，分支回归探针作者 | 两个年代时序缺陷的 Play Mode 探针 | [65378b9](https://github.com/Twispra/XRmuseum/commit/65378b93e5155329c3f88eac23028e5fda2baa10)；[#15 后续自报](https://github.com/Twispra/XRmuseum/issues/15#issuecomment-6007761031) 两项通过 | 仅年代远程分支，未进 main、无新 PR；与 23 项整合验证不同来源 | 团队决定是否保留及放置方式，不自动并入运行时 |
| 10-06 | Fttt1003，三语整合原作者；Twispra，合并者 | 简体/繁体/英文、193 文案、507 字符，菜单/演示/帮助动态刷新，维护说明 | [#18](https://github.com/Twispra/XRmuseum/pull/18)，18:46 合 main [bfb1b60](https://github.com/Twispra/XRmuseum/commit/bfb1b6090b8c36c3a3645a0c6b013cb8fd52e15c)；[活动/失败过程](activity/2026-10-06-trilingual-museum.md) | 四组最终验收退出 0；保留字体容量、溢出、哈希及退出失败路径。OpenCC 繁体辅助转换，尚无独立语言审校 | 三语人工审校、图集设备性能与真实阅读 |
| 10-06 | Fttt1003，体验/接头作者；Twispra，功能分支合并者 | 前窗玻璃/声音/缓动、空腔截面、平滑复位、电话鹅颈和收发强调 | [#19](https://github.com/Twispra/XRmuseum/pull/19)，19:08 合 codex/trilingual-museum-2026-10-06；[固定交接](https://github.com/Twispra/XRmuseum/blob/b61c13c63a0b901f1002eb764140fbe1bf27fcd7/docs/activity/2026-10-06-helmet-experience-release.md) | 当时仅在功能分支；当前通过 #24 整合，见后续整合行。分支体验 76、观察 206、探索 1486、三语重复断言通过/退出 0；未覆盖主线新热点，字体/文案只读预览已有冲突 | 原作者成果由后续 #24 兼容整合，仍保留本行历史验证来源 |
| 10-06 | Aragorn-cyber，热点原作者/迁移提交者；Codex 辅助 | 前窗/上壳/胸甲三个观察热点、信息/进度/完成状态、独立 Prefab 与场景接入 | [d84c93f](https://github.com/Twispra/XRmuseum/commit/d84c93f352541949db246440adb857361ec80ecd) 直接在 main；[原迁移记录](activity/2026-10-06-helmet-hotspots-latest.md) | 作者 18:38 / main committer 18:59；原记录测试 9/10→10/10，旧“未推送”为快照。后续真实保存生命周期暴露复位/选择/三语问题 | 与下一行交叉链接；不要重新整份合并旧热点场景 |
| 10-06 20:14–20:58 | Twispra，热点问题报告/修复/整合；Codex 辅助 | 保存 Prefab 复位、Direct/Ray 表面距离、三语/字体、台沿卡片布局 | [#20](https://github.com/Twispra/XRmuseum/issues/20) / [#21](https://github.com/Twispra/XRmuseum/pull/21)，20:58 合 main [18d7f8a](https://github.com/Twispra/XRmuseum/commit/18d7f8a09c29bb0c757b8a80bdc245b0fe40b785)；[修复报告/截图](activity/2026-10-06-hotspot-repair.md) | 热点 12、主场景 82319（内含热点 381）、模块 11、探索 1469 通过/四进程退出 0；202 文案、533 字形。大量重复文字断言不等于独立测试数量 | 设备/其他视角验收，与 #19 后续兼容回归；截图仅 Codex 复核 |
| 10-06 | Twispra 发起、Codex 辅助；子任务只读核对 | 首次补齐历史贡献/角色、问题/待办和周索引，缩短当前状态并保留历史入口 | [本次活动与检查范围](activity/2026-10-06-project-records-audit.md)，仅文档 [#22](https://github.com/Twispra/XRmuseum/pull/22)，21:31 合 main `7f1a2b8` | 发现 #19 合入功能分支及年代探针未进 main；#2 绝对路径仍待处理；没有正式 APPROVED 记录，不把 AI 评论或合并算成员批准 | 以核对记录和后续 #24 增量更新；设备/构建及资料访问仍待补 |
| 10-06 | Fttt1003 为 #19 原成果作者；Twispra/twispark 负责本轮整合，Codex 辅助 | Issue #23：将 #19 整合到当前 main，保留 #21 修复；三语/字体冲突和热点平滑复位兼容 | [PR #24](https://github.com/Twispra/XRmuseum/pull/24)、[本轮整合与证据](activity/2026-10-06-pr19-main-integration.md)；六组 76/206/1486/84346 次断言/11/12 通过且退出 0，267 输入哈希一致，205 文案/536 字形 | 双父合并保留作者历史；测试截图旧帧起点两次失败后修复；Blender/FBX/公共输入/Packages/平台不变。主场景含热点/跨功能，数量不重复累加 | 按 #24 实际合并版本交接并释放共享范围；真实头显、人工试听、用户/性能及构建验收 |
| 10-07 | Fttt1003，精细资产与本轮整合负责人；Codex 辅助 | 在 main `afc3a9e` 接入 Mark V、精细泵／电话设备及合成声音，保留原交互；源资产历史到 `e2ee58f` | [本轮整合／验证](activity/2026-10-07-refined-model-integration.md)、[接手说明](refined-model-integration.zh-CN.md)；Blender 四资产回读通过，Unity 回归结果以新活动记录为准；源阶段见 [Mark V R2](activity/2026-10-07-mark-v-refinement-r2.md)／[演示资产](activity/2026-10-07-connection-demo-refinement.md) | 首次导入发现原生对象初始化和坐标轴问题，修复后重测；Mark V 仅索引 1，另两时期仍占位。1916 设备与 1944 头盔实际配套没有证据 | 测试通过后提交本轮 PR，并调整 #25／#33 中的接入状态和遗留项；后续另两时期造型、人工审阅／试听和设备性能 |

历史测试均为各自记录中的 commit / 基准加额外产品文件，具体环境、输入哈希及退出限制以链接为准。#22 整理只读并复核旧证据；#24 的六组新运行单独列出。两者均没有真实头显或用户评价；不将各阶段通过数相加成当前版本总验收。9 月 23 日原周索引及“PR #2 待审查”等旧说法保留在[整理前快照](https://github.com/Twispra/XRmuseum/blob/18d7f8a09c29bb0c757b8a80bdc245b0fe40b785/docs/weekly-activity.md)，独立历史记录未覆盖。

记录依据：Group Project Description（202627A）p6 的责任、决策、完成及未解决问题，以及 p12 的可归属版本历史与资源/辅助声明。官方依据和未确认的 Canvas 要求见[课程索引](course-requirements.md)。正式个人短文与反思由成员本人根据真实工作撰写。
