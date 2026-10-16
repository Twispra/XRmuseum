# 2026-10-16：新访客复位、年代说明与技术文档修订

用户在整体审查后明确要求修复这三项，检查通过后展示。关联 [Issue #57](https://github.com/Twispra/XRmuseum/issues/57)。当前对话根代理负责整合、保存共享资产与验证；两个子任务分别在独立 detached worktree 修改探索源码／专项验收与文档，另一个在独立验收副本准备环境及年代专项，随后串行提交到 `codex/visitor-flow-2026-10-16`。不代表其他成员人工审批。

## 基础与边界

- 开工 `E:/XRmuseum` 位于 `codex/project-resource-review-2026-10-16`，HEAD 及 fetch 后的 origin/main 均为 `411524e10d4eee7ba00e43d7d27a3b329d4e2ad6`。#56 已合入；#25／Draft #28 为独立建模，#50 共享编辑已结束、设备调试继续开放。
- 主修改副本为 `C:/Users/zhaoooooo/.codex/worktrees/visitor-flow-2026-10-16/XRmuseum`。因 C 盘空间有限，Unity 使用独立 E 盘副本 `E:/Codex/worktrees/visitor-validation-20261016/XRmuseum`，缓存只从旧验收工程复制，没有改动旧缓存。全部 TEMP／TMP、日志与构建留 E 盘。
- 原工程已有 `ProjectSettings.asset` 修改与未跟踪资料保留。其开工 SHA256 为 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`；本轮不修复或覆盖这些内容。
- 不改 Unity 2022.3.62f3c1、Packages、公共 XR／Input、ProjectSettings、模型／FBX／Blender、现有 GUID；未运行旧头盔生成器。主场景、表与字体由根代理单一写入，Issue 中明确范围。

## 修复后的行为

### 新一轮参观

`HelmetExplorationController.RestartVisit` 停止演示，清 AIR／VOICE、Hint 和选择／页面状态，再清同一展品三热点的访问记录、勾形、完成提示及旧说明计时。默认只在当前 inspection 子树寻找唯一匹配 Tour；保存主场景显式绑定 `visitTour`，重绑定时拒绝其他展品引用，避免误清全场状态。

New Visit 保留当前语言、年代、声音／减少动画偏好、物理展品位姿和观看位置。头盔 RESET／Home 负责归还展品，Reset View／F11 负责观看位置；这两个入口保留本轮发现。没有把原有三个入口合并成语义不清的单一 Reset。

### 年代与三语

保留原两只按钮、碰撞体尺寸和交互，增加静态 key `era.previous`／`era.next` 的简繁英标签。生产输入检查发现原按钮中心 0.955 m 被 0.96 m 的 Display Top 挡住，截图也截断文字；在 Issue 补充范围后，仅沿世界 Y 抬高 8 cm 至 1.035 m，底部与台面留 4 cm，x／z 与原正面射线断言不变。`RefinedHelmetBinding.AppliedEraIndex` 在实际几何绑定完成后发布；展签和热点按已显示的年代变化，持窗／剖面导致请求延迟时不提前宣布新年代。

早期 1839 档展签明确为占位研究模型，三个观察热点明确历史细节尚未重建；返回 Mark V 恢复原内容，SuperLite 保留原研究重建与侧开／通信示意边界。切年代、切语言不清已有发现，也不重启已选卡片的计时。

新增七条文案：上一／下一年代、早期展签、早期说明句，以及三条完整“标题＋换行＋说明”。后面三条来自独立复审发现的动态查表遗漏，已补齐，防止简繁卡片回退英文。JSON 共 220 条，字符清单 553 字；源 TTF 保留原 7,666 码位并仅补「佔」至 7,667，沿用同一本机 Noto Sans SC OFL 来源。原字体、图集、材质身份和既有 `.meta` 均保留。[源字体扩展记录](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/source-font-expansion.json)

### 文档

技术说明按当前候选重写，保留原作者历史构建记录的入口，准确区分最新构建、旧设备反馈和本轮验证。README 与年代／探索／精细接入说明同步当前功能；SuperLite 页的 F9／F10 与保存主场景一致。历史活动和已引用报告不覆盖。

## 验证与交接

最终运行时、保存资产与全部最终检查均绑定 **`806993a1723bbf7a0b5c75bedbbccf8e6f5022a5`**。此后的本轮提交仅为工程外文档／证据，不改已测工程源码。验收副本无额外未提交工程源码，存在本代理此前运行的未跟踪 QA 输出，具体列表见各进程记录的 dirtyBefore。统一使用 Unity 2022.3.62f3c1、正常 Dynamic Input Actions 合成输入，不停用物理手／碰撞过滤，不手动调用 XRI SelectEnter 绕过入口。

| 检查入口／步骤 | 结果及证据 |
| --- | --- |
| `Museum.Exploration.Editor.VisitorNewVisitValidation.RunBatch` | **128/128**，0 errors，自然退出 0。真实射线选择 New Visit／头盔 RESET／Reset View；三语言分别选择不同年代，验证发现状态清理／保留、偏好／位姿保留，以及缺 Tour、晚绑定、禁用重启和多实例。[报告](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/new-visit-report.json)、[进程](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/new-visit-process.json) |
| `Museum.Localization.EditorTools.VisitorEraValidation.RunBatch` | **691/691**，0 errors，自然退出 0。三语×三年代、完整卡片翻译、两个生产按钮选择、持窗延迟应用、计时／动画保留及19张单眼近景。[报告与截图](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/era/report.json)、[进程](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/era-process.json) |
| `Museum.Localization.EditorTools.LocalizationValidation.RunBatch` | **11/11**，0失败，自然退出 0；保存表220条、静态字形完整。[报告](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/localization-report.json)、[进程](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/localization-process.json) |
| `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch` | **86,968 次断言**、0失败／errors／warnings，自然退出0；含热点792项／20次生产Direct/Ray选择。重复文字断言不是独立用户案例。约19MB原报告和主场景渲染留本地，发布带原报告SHA的[有界汇总](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/main-summary.json)、[热点完整报告](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/main-hotspot-interaction.json)、[进程](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/main-process.json) |
| `Museum.PhysicalHands.EditorTools.MuseumPhysicalHandsBuild.RunBatch`，`-buildTarget Win64` | Windows64 Development **Succeeded**，0 errors／2条既有OpenXR可选迁移warnings，自然退出0，构建报告214,119,503 bytes。[报告](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/windows-build-report.json)、[进程](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/windows-build-process.json) |
| 全部进程退出后复核 | **1,145 个工程文件零差异**；tracked MuseumVR无dirty。原工程设置SHA仍为开工值。[来源复核](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/final-source-verification.json)、[完整运行汇总](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/summary.json) |

批处理实际命令采用 `Unity.exe -batchmode -projectPath <E盘验收工程>/MuseumVR -logFile <独立日志> -executeMethod <上表入口>`；各Play入口自行进入Edit Mode并等待20秒后退出，不加`-quit`。构建另加`-buildTarget Win64`；TMP／TEMP、MUSEUM_QA_OUTPUT及MUSEUM_BUILD_OUTPUT指向E盘独立目录。完整参数、PID、时间和自然退出码保留于上述进程记录。

构建已复制至 **`E:/Codex/Builds/XRmuseum/visitor-flow-20261016-806993a/`**，包括exe、Data、UnityPlayer及Mono等 **272个文件**，逐一与原产物核对SHA256完全相同；复制目录所有文件合计214,220,277 bytes，与BuildReport统计口径分别记录。[完整文件清单](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/build-output-manifest.json)。本轮没有启动Player，不将构建成功扩大为头显会话通过。

保留三次负向证据：`a298ea5` 的新英文卡片溢出（未降字号，随后缩短说明）；`0d4d2b9` 的Next射线悬停失败；`d6246e8` 的原触点诊断先命中Display Top后命中Next。三进程均自然退出1，没有把失败改成通过。[溢出报告](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/era-initial-overflow-report.json)、[悬停报告](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/era-next-hover-report.json)、[遮挡诊断](../../artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/era-ray-obstruction-report.json)。更早 `5e13c3a` 新访客125项是开发阶段结果，最终128项已覆盖本轮版本，不重复相加。

技术说明导出为[两页A4 PDF](../../artifacts/technical-notes/20261016/phase2-technical-note.zh-CN.pdf)，[导出记录](../../artifacts/technical-notes/20261016/phase2-technical-note.zh-CN.json)绑定Markdown与PDF哈希。使用原Noto字体排版，Poppler渲染后检查两页无缺字、截断或重叠；页面格式检查不等于课程已正式提交。

候选源码与证据供用户查看，本轮不合并。下一步为用户／另一位成员依据实际diff、程序和证据审阅，再决定合并；现有共享写入保持冻结，接手者先在Issue协调。本机原E盘工程未同步此候选，已有设置修改和未跟踪资料继续保留。日常不运行迁移工具或旧模型生成器。

真实头显、坐姿／单手舒适性、目标设备性能、参与者学习研究和正式课程提交不由本轮自动验收证明。AI 辅助范围为原有代码／文档／派生字体／自动检查和证据整理；来源许可未改变，个人贡献与反思仍按本人真实工作记录。
