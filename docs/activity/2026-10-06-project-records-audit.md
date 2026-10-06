# 2026-10-06：项目记录历史补齐与 GitHub 核对

用户明确要求读取《项目记录定期更新指令.txt》、核对小组 GitHub 并提交仅文档 PR。本轮由 Twispra 发起及提交，Codex 辅助整理；后台子任务只读核对，全部文档由同一写入者在独立 worktree 整理。不实施功能、不分配六人任务、不代替成员审批、不合并 PR，也不建立定时自动化。

## 核对基准、范围与下次起点

- 时间：2026-10-06 21:01–21:15，Asia/Shanghai（UTC+8）；21:05 的主线检查点为 [18d7f8a09c29bb0c757b8a80bdc245b0fe40b785](https://github.com/Twispra/XRmuseum/commit/18d7f8a09c29bb0c757b8a80bdc245b0fe40b785)。下次从该 SHA 和本记录开始增量核对，仍需检查此前分支后续及新评论，不能只看提交时间。
- 实际原仓库 E:/XRmuseum；origin=https://github.com/Twispra/XRmuseum.git，GitHub 返回默认分支 main、私有仓库。原本地分支 main、HEAD b4e1886c9762a85926ef3bf58db92500a29176f3；本次成功 git fetch origin，未切换或更新原 checkout。
- 隔离工作副本 C:/Users/zhaoooooo/.codex/worktrees/project-records-2026-10-06/XRmuseum，分支 codex/project-records-2026-10-06，从上述 origin/main 创建，初始干净。只修改本活动记录、周索引、项目状态、资源/AI 范围补充及协作指南中的过期状态句。
- GitHub 登录账号及 ID 核实为 Twispra / 98499654，仓库有 push 权限；本仓库现有提交身份 twispark 与 98499654+Twispra@users.noreply.github.com 对应本人，不使用其他成员身份。未改变全局 Git 身份或 remote。
- 首次补齐范围：周索引原仅记录 9 月 23 日，故核对 2026-09-22 初始化至本轮主线，并查所有仍有独有提交的远程分支。
- REST 集合完整分页：54 个 main 提交、14 个 PR、21 个 Issue 集合项（其中 7 个普通 Issue）；每页 100，第 2 页均为空。分支连接器读到 17 条，继续 cursor 后为空。读取全部目标 PR 的正文、review/讨论/评论和七个 Issue 的评论；实际提交及合并差异用本地 Git 检查，不以旧 PR 正文判断当前状态。
- GitHub Actions 查询为 2 次历史 Copilot Code Review 运行，第 2 页为空；14 个 PR head 的 commit statuses 均为空，check-runs 仅 #2 head 有一个 Copilot 审查成功项，其他为 0。main 本轮 SHA 的 status/check-run 为空。没有把 Copilot 运行成功当作 Unity 测试。

原 checkout 的 ProjectSettings.asset 修改及 artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix、artifacts/pr-review、artifacts/repository-permissions 和多份 docs/activity 未跟踪记录均保持原地，未纳入提交。设置文件 SHA256 为 81C0EB3103E303A2567619E388C45709E89FA1940353CDFE4A163BBE402C5587；本次只读这些本机记录用于核对，不把未上传产物写成队友已经可访问。其他成员电脑上的当前文件、聊天和测试不在证据范围。

## 账号、责任与原创归属

可核实参与账号为 Twispra、Fttt1003、nikoyeah830-wq、Ereshkigal1224、Aragorn-cyber。只保留账号，不根据 Git 显示名自动补齐六人的真实姓名；完整名单/姓名映射待组长确认。API 将大部分原提交关联到对应作者账号，未发现本轮范围提交的 Co-authored-by 声明；这不证明没有线下合作。

| 日期 | 账号与角色 | 成果、范围和固定证据 | 状态 / 归属边界 |
| --- | --- | --- | --- |
| 09-22 | Twispra：初始化作者 | [cc7605e](https://github.com/Twispra/XRmuseum/commit/cc7605e765100200881e4a9eb62a10f265bccba1)，Unity 基础工程、起始场景、指南；[初始化记录](../initialization-checks.md) | main；官方模板、XRI/TMP 样例与 Codex 辅助须声明，非全组原创 |
| 09-23 | Twispra：灰模/交互/修复作者 | [#1](https://github.com/Twispra/XRmuseum/pull/1)，产品 [e7d819e](https://github.com/Twispra/XRmuseum/commit/e7d819e4d3aea1a0bb439385785a5606f0d7acc0)，建模源→两份 FBX→Unity 抓取；3189006/f354815 保护、ProbeKey 与有效网格检查；[原型记录](2026-09-23-helmet-prototype.md)、[修复记录](2026-09-23-pr1-review-fixes.md) | main；独立精修前窗未接入，不等同后来的灰模前窗交互 |
| 09-23 | Twispra：协作文档/模板作者 | [#2](https://github.com/Twispra/XRmuseum/pull/2)，AGENTS、.github 模板、状态/课程索引、证据保存；[协作记录](2026-09-23-team-collaboration.md) | main；复用已有验证，不是新跑 Unity；不虚构六人采用或独立成员审批 |
| 09-29–30 | Fttt1003：观察功能原作者及连续性修复者；Twispra：辅助审查/反馈 | [#3](https://github.com/Twispra/XRmuseum/pull/3)，HelmetInspection、InspectableHelmet、MuseumStart；原实现 [3cf6f8d](https://github.com/Twispra/XRmuseum/commit/3cf6f8ddb620f1943cef416a5eb4d72134a586ec)、修复 [61db42c](https://github.com/Twispra/XRmuseum/commit/61db42c673383a5c329e6f176e207d0f88c496bc)；[修复前后](2026-09-30-section-continuity-review.md) | main；Twispra 的反馈和合并不计为该功能原作者 |
| 10-02–03 | Fttt1003：海事环境作者、回归/交接整理 | [#4](https://github.com/Twispra/XRmuseum/issues/4) / [#5](https://github.com/Twispra/XRmuseum/pull/5)，产品 [342cdda](https://github.com/Twispra/XRmuseum/commit/342cdda2b77aab781d991cbd497c994330e5e76a)，MaritimeGallery、主场景/环境材质；[发布记录](2026-10-03-gallery-release.md) | main；本阶段 AIR/VOICE 装置为静态，动画是下一项工作 |
| 10-03 | Fttt1003：探索/模型动画作者 | [#6](https://github.com/Twispra/XRmuseum/issues/6) / [#7](https://github.com/Twispra/XRmuseum/pull/7)，产品 [c2bf63c](https://github.com/Twispra/XRmuseum/commit/c2bf63cdc42d5730588321e103fd76752db5ea26)，HelmetExploration、16 秒演示、提示/回顾；[发布记录](2026-10-03-exploration-release.md) | main；#5 的原依赖已完成，不能照抄旧 base 说明为当前依赖 |
| 10-03 | Fttt1003：信息分层作者 | [#8](https://github.com/Twispra/XRmuseum/issues/8) / [#9](https://github.com/Twispra/XRmuseum/pull/9)，产品 [2504ccd](https://github.com/Twispra/XRmuseum/commit/2504ccdbde5fcc6eba61eb456d7c9aa595243c85)，按需详情/工具/Help、背景避让；[活动](2026-10-03-exhibit-information.md) | 10-05 已合 main；原 #9“待审查”是发布快照 |
| 10-03–05 | nikoyeah830-wq：README 补齐作者 | [b40eb9f](https://github.com/Twispra/XRmuseum/commit/b40eb9f652a3935595575e6a5ec0f287c09777b5) 与候选 [60bdb4e](https://github.com/Twispra/XRmuseum/commit/60bdb4e4d4b02ca10a2f16eaff12c930916e650c) 的 README blob 相同；[#11 关闭说明](https://github.com/Twispra/XRmuseum/pull/11#issuecomment-5992980777) | b40eb9f 已进 main；#11 重复关闭未合并，成果只记一次 |
| 10-03–05；10-06 后续 | nikoyeah830-wq：年代模块/时序修复作者 | [#10](https://github.com/Twispra/XRmuseum/pull/10)，a339fb4/1734815，HelmetEras 数据、Prefab、占位材质及淡变修复；[模块活动](2026-10-03-helmet-eras.md)。后续探针 [65378b9](https://github.com/Twispra/XRmuseum/commit/65378b93e5155329c3f88eac23028e5fda2baa10)，[成员自报两项通过](https://github.com/Twispra/XRmuseum/issues/15#issuecomment-6007761031) | #10 已合 main；65378b9 仅远程分支，未在 main，未新建 PR；不把该探针当整合人的 23 项验证源码 |
| 10-04–05 | nikoyeah830-wq：本地化原作者、字形校验及初始入口修复者 | [#13](https://github.com/Twispra/XRmuseum/pull/13) c0c46e3/8a32c83；[#14](https://github.com/Twispra/XRmuseum/pull/14) 726808c/6b1a7f0/d4f7fba；Localization 模块/字体/初始场景；[模块活动](2026-10-04-localization.md)、[接入活动](2026-10-05-localization-integration.md) | main；与 Twispra 的后续补齐是同一交付的不同角色，不重复写成两人各自独立完成整套功能 |
| 10-05 | Ereshkigal1224：PR #12 提交者 | [#12](https://github.com/Twispra/XRmuseum/pull/12) / [64a4547](https://github.com/Twispra/XRmuseum/commit/64a45478def78e0d971ce5daddfd8d48ea819570)，模拟器 Manipulate Right 从 Space 改 Right Shift，指南；[活动](2026-10-05-windows-simulator-input.md) | main；Git 作者字符串为“赵祥睿”，commit API author/committer 为空，不能仅据该提交建立学生身份映射 |
| 10-05–06 | Twispra：#15/#16/#17 问题报告、接手补齐/整合/定向验证 | #10/#13 同步主线 b677875/a9852ed；#14 产品 [4d84e17](https://github.com/Twispra/XRmuseum/commit/4d84e175b3d6ec9a0721dba5224419a01c39ee5d)、四组入口/保存表/字体/许可补齐；[年代](2026-10-06-pr10-completion.md)、[模块](2026-10-06-pr13-completion.md)、[主场景](2026-10-06-pr14-completion.md) | main；保留 nikoyeah830-wq 原作者/修复者归属，Codex 复验不称为另一位成员人工批准 |
| 10-06 | Fttt1003：三语整合作者 | [#18](https://github.com/Twispra/XRmuseum/pull/18)，产品 [a46587e](https://github.com/Twispra/XRmuseum/commit/a46587e10aa37adf927f576b1507d24294fd8421)，193 条文案、507 字符，菜单/演示字幕动态刷新；[活动](2026-10-06-trilingual-museum.md) | 已进 main；OpenCC 辅助繁体初稿，独立语言审校待做 |
| 10-06 | Fttt1003：体验细节/背部电话接头作者 | [#19](https://github.com/Twispra/XRmuseum/pull/19)，产品 [484c5d4](https://github.com/Twispra/XRmuseum/commit/484c5d47ae6ce36aa94bfd703d36bf1290f917de)，HelmetExperience、前窗/截面/连续复位/收发反馈；[固定版本交接](https://github.com/Twispra/XRmuseum/blob/b61c13c63a0b901f1002eb764140fbe1bf27fcd7/docs/activity/2026-10-06-helmet-experience-release.md) | 已合入三语功能分支，未进 main；声音/几何为原型参数，不能写当前主线已具备 |
| 10-06 | Aragorn-cyber：热点原作者与迁移提交者 | 旧 [8fe3142](https://github.com/Twispra/XRmuseum/commit/8fe3142dc4ed1e3b31e6955cbbf81150d2d39fec)，迁移 eccf4c7，main 直接提交 [d84c93f](https://github.com/Twispra/XRmuseum/commit/d84c93f352541949db246440adb857361ec80ecd)；HelmetHotspots、三处可选择标记及信息/进度；[原迁移记录](2026-10-06-helmet-hotspots-latest.md) | d84c93f 已在 main；Git 作者拼写 Aragon-cyber，API 关联账号为 Aragorn-cyber。作者日期 18:38，main 提交的 committer 日期 18:59；旧文档“未推送”不代表当前状态 |
| 10-06 | Twispra：热点缺陷修复者 | [#20](https://github.com/Twispra/XRmuseum/issues/20) / [#21](https://github.com/Twispra/XRmuseum/pull/21)，产品 [4b01e6c](https://github.com/Twispra/XRmuseum/commit/4b01e6ca40f1007dff875bc96a45b8a6ee1a2412)，复位/表面距离/三语/卡片，202 文案和 533 字形；[活动](2026-10-06-hotspot-repair.md) | 已合 main；不归并为 Twispra 原创了整套热点；截图复核由 Codex 执行 |

本次 main 列表的 API 作者映射核实了上述大部分实现；#12 的例外已明确。共同成果交叉引用同一 PR/Issue，不用提交数量或代码行数评价成绩。Twispra 是所有已合并 PR 的 merged_by；这只记录整合/合并角色，包括将 #19 合入功能分支，不把他人功能改列为原创。

## PR 状态与真实主线关系

| PR | 提交者 | 实际处理时间（上海） | 目标、结果与固定提交 |
| --- | --- | --- | --- |
| [#1](https://github.com/Twispra/XRmuseum/pull/1) | Twispra | 09-23 19:41 | main，545229e |
| [#2](https://github.com/Twispra/XRmuseum/pull/2) | Twispra | 09-23 19:47 | main，4fe7252 |
| [#3](https://github.com/Twispra/XRmuseum/pull/3) | Fttt1003 | 09-30 19:50 | main，74556aa |
| [#5](https://github.com/Twispra/XRmuseum/pull/5) | Fttt1003 | 10-03 13:05 | main，242b224 |
| [#7](https://github.com/Twispra/XRmuseum/pull/7) | Fttt1003 | 10-03 13:07 | main，8e75e71 |
| [#9](https://github.com/Twispra/XRmuseum/pull/9) | Fttt1003 | 10-05 18:47 | main，73a7706 |
| [#10](https://github.com/Twispra/XRmuseum/pull/10) | nikoyeah830-wq | 10-06 08:52 | main，175f478 |
| [#11](https://github.com/Twispra/XRmuseum/pull/11) | nikoyeah830-wq | 10-05 18:51 | 关闭、未合并；main 的 b40eb9f 已含相同 README，832b43d 不是已合并证据 |
| [#12](https://github.com/Twispra/XRmuseum/pull/12) | Ereshkigal1224 | 10-05 18:49 | main，97b8810 |
| [#13](https://github.com/Twispra/XRmuseum/pull/13) | nikoyeah830-wq | 10-06 08:54 | main，9457f9b |
| [#14](https://github.com/Twispra/XRmuseum/pull/14) | nikoyeah830-wq | 10-06 09:10 | main，b4e1886 |
| [#18](https://github.com/Twispra/XRmuseum/pull/18) | Fttt1003 | 10-06 18:46 | main，bfb1b60 |
| [#19](https://github.com/Twispra/XRmuseum/pull/19) | Fttt1003 | 10-06 19:08 | codex/trilingual-museum-2026-10-06，1ffd07c；未进 main |
| [#21](https://github.com/Twispra/XRmuseum/pull/21) | Twispra | 10-06 20:58 | main，18d7f8a |

13 项 merged，其中 12 项目标为 main；这些主线合并 SHA 均通过 git merge-base --is-ancestor 检查。#19 产品 484c5d4 和合并 1ffd07c 对 main 的祖先检查均 exit 1；main 没有 HelmetExperience 模块。所有 GitHub 日期按服务器时间转为上海时间，不按本机 shell 日期猜测。

远程分支中未被 main 包含的独有提交共 8 个：#19 的三个提交及功能分支合并 1ffd07c；年代探针 65378b9；旧 README 60bdb4e；旧热点 8fe3142/eccf4c7。后两类是已存在同等成果的历史候选，不能一律当成“新功能没合并”。其他 10 个功能分支 tip 均为 main 祖先。核对时无开放 PR，#19 与年代探针均没有新的 main 目标 PR。

## 问题表现、处理和验证状态

| 发现/来源 | 表现与原因 | 修复/验证及当前结论 | 下一步 / 限制 |
| --- | --- | --- | --- |
| 09-30 [#3 评论](https://github.com/Twispra/XRmuseum/pull/3#discussion_r4143841614) | 约 78.52° 的切面法向临界跨越使工具切换 up 轴，球柄跳约 39.6 cm；40 序列中 24 失败，旧 140 项未覆盖 | Fttt1003 的 61db42c 保留连续局部方向和 wrist roll；几何 40/40（a6aae95）、主场景 207（0d31e42）、拿放 90（a6aae95），实际退出均 0；已进 main | 记录为指定模拟/几何范围已验证修复；手柄真实手感、缩放与设备性能待核验 |
| 10-05 模拟器 [#12](https://github.com/Twispra/XRmuseum/pull/12) | 用户报告空格后卡顿/移动偏移；静态查到 Manipulate Right 绑定 Space，WASD 操控目标变化 | 改 Right Shift，JSON/绑定检查通过；活动记录只记用户初步反馈，PR 后补逐键 Play 确认；没有附运行日志/帧率，Codex 活动记载本轮未运行 Unity | 不把全部卡顿归因于此，不写完整输入或真实头显验收通过 |
| 10-05 18:51 [#15](https://github.com/Twispra/XRmuseum/issues/15) | 年代淡变禁用残留、瞬时切换被旧协程覆盖、接头说明混淆 | nikoyeah830-wq 1734815 修复；Twispra 接手定向验证 23 项、退出 0，#10 合并后关闭 | 独立模块已验证；评论中的分支探针 2 项自报是另一证据，未进 main |
| 10-05 18:52 [#16](https://github.com/Twispra/XRmuseum/issues/16) | 校验器检查文案未使用的顿号而误报，不是实际缺字 | 8a32c83 按实际文案/key/固定标签逐字校验，真实缺字仍失败；11/11、退出 0，#13 合并后关闭 | 后续新增文字仍需更新字形，不以一次通过替代新版本检查 |
| 10-05 18:54 [#17](https://github.com/Twispra/XRmuseum/issues/17) | 初始三处绑定落在 #9 已隐藏标识，游客看不到译文；需处理 #13 依赖 | 原作者修订后，Twispra 补保存表/198 字符及四组真实入口；4d84e17 的场景 627、模块 11、探索 1469 全通过/退出 0，#14 合并；[共享范围释放](https://github.com/Twispra/XRmuseum/issues/17#issuecomment-6007240321) | 不撤销旧隐藏策略；部分视角标题/面板遮挡与实机阅读仍待查 |
| 10-06 [#18 活动](2026-10-06-trilingual-museum.md) | 字体容量不足、英文标题溢出、测试期间 Editor 文件哈希变化及即时退出卡住 | 扩静态图集 4096²/507 字符、修标题、冻结产品后复验；原退出异常 -1 不算成功，Editor 延迟包装复测原 206 项并退出 0；四组验证最终退出 0 | 不把后续指定运行正常退出写成原生退出根因已解决；16 MiB 原始图集和繁体审校仍缺设备/人工结论 |
| 10-06 [#19 固定活动](https://github.com/Twispra/XRmuseum/blob/b61c13c63a0b901f1002eb764140fbe1bf27fcd7/docs/activity/2026-10-06-rear-telephone-connection.md) | 加电话接头后剖切工具中心漂移约 2 mm | 保留原定位包围盒、接头继续参加剖切；分支最终体验 76、观察 206、探索 1486、三语 78760 断言，通过/退出 0；186 个文件哈希映射产品 484c5d4 | 分支内已验证修复；未覆盖主线后加热点及未来整合，不作为当前 main 运行证据 |
| 10-06 20:14 [#20](https://github.com/Twispra/XRmuseum/issues/20) | 保存 Prefab 生命周期复位颜色不同步、Trigger 默认距离使近距误抓整盔、新文字绕过三语、信息卡遮挡入口 | Twispra 4b01e6c 修复；热点 12、主场景 82319（包含热点 381）、模块 11、探索 1469 通过，四进程退出 0；257 文件哈希匹配；#21 合并后关闭并[释放范围](https://github.com/Twispra/XRmuseum/issues/20#issuecomment-6016781971) | 自动输入/单眼截图范围已验证，不等于用户研究或全部视角无遮挡；主场景大数字含重复文字/字形断言 |
| 09-23 19:51 [#2 评论一](https://github.com/Twispra/XRmuseum/pull/2#discussion_r4082092920) / [评论二](https://github.com/Twispra/XRmuseum/pull/2#discussion_r4082092985) | 两份 preview/capture.json 含原机器绝对路径，跨 clone 复用不便 | 本次在 main 实际文件确认仍存在；是未解决的历史证据可移植性问题，#2 已合并不代表该评论处理完 | 建议新增相对入口映射；保留原报告，不在本次覆盖 JSON |

环境/探索/信息分层对应 Issue #4/#6/#8 都已关闭，main 包含成果；原文中的进行中/未合并属于当时快照。Issue 关闭只是状态证据，上表另引用具体复现、修复提交和报告，没有将关闭作为唯一修复依据。

## 未合并影响与待办判断

只读检查 #19 的增量实际 diff：新增 HelmetExperience、电话 Mesh 和剖切数据，同时改 HelmetInspectionController、HelmetExhibitReset、演示模型/面板、Window 材质、InspectableHelmet、MuseumStart、三语表和字体；原模型 FBX/建模源、公共输入、Packages 和平台没有变化。新增几何、声音/速度是原型表达，双眼透明排序、声音舒适度、截面重建性能、帧率和构建未验证。

只读 git merge-tree d432a37 18d7f8a b61c13c 发现 characters-required.txt、SDF.asset、LocalizationTable.asset、MuseumTranslations.json 的文字冲突，TTF 不能自动合并。MuseumStart 双方已改，但该预览未发现场景文字冲突；这不证明整合引用/行为兼容。本轮未进行合并或修改产品。

最小下一步为团队确认一位整合人，在最新 main 建新的整合 PR，保留 #21 热点，逐项处理三语/字形与 GUID，检查新接头、AIR 锚点、Tour 的父子关系、剖切数据，再跑体验、原观察/探索、三语和热点回归。#18 的依赖已完成，不能再把它列作未合并阻碍。年代探针是否保留、年代造型/场景接线、受众/学习目标和候选手模型等继续待团队决定，不擅自认领。

现有唯一任务入口仍是 GitHub Issues；当前状态页的[待办](../project-state.md)引用上述证据，真实设备、语言审校、课程访问/AI 政策和当前构建的旧缺口保留。证据可移植性的下一步及优先级仅为本次 AI 建议，未写成团队决定。

## 审查、权限与未能确认

- #1/#2/#3 仅有 COMMENTED；#5/#7/#9/#10/#11/#12/#13/#14/#18/#19 无 review submission；#21 仅 Copilot 配额不足的 COMMENTED。未查到 APPROVED。#18 请求 nikoyeah830-wq 审查并不代表已完成审查。Twispra 的早期评论明确是 Codex 技术初检，不能记为另一位组员人工批准。
- main 分支保护回读接口返回 403 Resource not accessible by integration。本机已有 19:43 启用、19:51 管理员豁免记录，属于当时本地证据，未上传原文件/截图；本次不能独立确认当前远程设置，也未修改保护/权限。#20 的远程关闭评论记录了所有者授权绕过缺少审批合并。
- Canvas 最新政策、六人完整身份、教师访问、真实用户/头显、实际工时及其他成员电脑进展未核验。PR/commit 访问依赖小组私有仓库权限，不能宣称公众可访问。

## 本次验证与交接

本次未启动 Unity/Blender、未重跑功能测试，只检查 GitHub 当前 metadata/评论、Git 祖先/实际 diff、提交中的旧报告/活动记录及文档链接。旧报告的被测 commit、额外 dirty、环境和实际进程退出继续由原记录说明，没有冒称在 18d7f8a 上重测。9 月 22 日可执行构建仅证明初始化版。

活动记录、历史报告不覆盖；缩短状态页并通过固定版本链接保留整理前快照，周索引用 PR/Issue/commit 去重。资源页补 #21 的字体/热点辅助范围及本次文档整理声明，#19 资源只链接功能分支，不误写进入 main。逐文件暂存，仅文档 PR 交由小组审查，未自动合并。最终文档检查与提交 SHA 见本次 PR 验证段；下次需 fetch 并重核主线和仍有后续的分支。

文档检查补充：五份文档的 95 个本地链接、27 个固定 commit/blob 链接均可解析，git diff --check 通过；两个只读子任务复核历史归属与当前依赖后，已纠正“用户确认”与“本地编辑器排查”的措辞。21:15 发布前再次 fetch，origin/main 仍为 18d7f8a；原 checkout 的分支、HEAD、状态清单及上述设置哈希未变。本次校验没有重新运行产品测试。
