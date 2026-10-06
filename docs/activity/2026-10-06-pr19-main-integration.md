# 2026-10-06：将 PR #19 整合到当前 main

任务：[Issue #23](https://github.com/Twispra/XRmuseum/issues/23)；交付：[PR #24](https://github.com/Twispra/XRmuseum/pull/24)。用户明确要求“将 #19 成果整合入当前 main”。本记录写于验收完成、准备合并时；GitHub PR 的实际 merged 状态和 merge SHA 是后续主线状态依据。

## 责任、版本与范围

源成果由仓库账号 **Fttt1003** 提交：产品 `484c5d47ae6ce36aa94bfd703d36bf1290f917de`、源 head `b61c13c63a0b901f1002eb764140fbe1bf27fcd7`。原 [PR #19](https://github.com/Twispra/XRmuseum/pull/19) 合入三语功能分支，未自动进入 main。Twispra（Git 作者 twispark）在独立 worktree 负责本轮整合，Codex 辅助冲突处理、跨功能回归、自动检查和记录；没有新增成员姓名、工时或人工批准。

开工原目录 `E:/XRmuseum` 位于 main `b4e1886c9762a85926ef3bf58db92500a29176f3`，有未知 ProjectSettings 改动与未跟踪活动/审查材料，原样保留。fetch 后实际 main 为 `18d7f8a09c29bb0c757b8a80bdc245b0fe40b785`，已含 #21 热点修复。整合分支 `codex/integrate-pr19-main-2026-10-06`；双父合并 `623e5a3` 保留双方提交与原作者；没有把全部源成果改写为整合者原创。

共享 Scene、头盔 Prefab、文案和字体由 Issue #23 协调，本次一位写入者。并行辅助只有一个指定 Editor 回归文件可编辑，其余为只读复核。未改 Packages、ProjectSettings、公共 XR 输入、Models、Blender/FBX 或目标平台；现有 LFS 15 个文件已展开，未以 fsck 代替工作区检查。验收后 fetch 发现用户侧已于 21:31 合并仅文档 #22，main 前进到 `7f1a2b8f548078eee50cd83051e86e68769a07b9`；本轮保留其贡献/问题/周索引并增量更新。该次 main 变化只有 5 个文档，产品代码/资产与被测版本完全一致，无需重复 Unity 验收。

## 整合结果与发现

#19 的透明前窗、缓动/静音程序反馈、保留空腔的网格截面、供气/通话方向强调、平滑复位与左电话/右供气接头保留。main 的热点运行时和热点 Prefab 保持字节一致；Tour 父节点保留，AIR 锚点遵循保存接头方向，电话新网格参与剖面而不改变定位边界。

五项本地化冲突逐项解决：JSON、保存文案表、字符清单、源 TTF 和静态 SDF。保留 main 202 条三语行，追加 `experience.sound.on/off`、`experience.telephone.connection`，共 **205 条**。现有 Noto Sans SC/OFL 字体源生成 cmap 并集 7660，所需字符 526；Unity 原地补充 声/聲/音，保存 **536 字形**。原字体 GUID 与 font/material/atlas fileID 保留，已保存图集像素与内存 buffer 哈希一致。既有 meta 未改，新 22 个 meta 无 GUID 冲突；未整文件接受场景或字体 YAML 的一方版本，未运行旧 Setup/Builder 重建资产。

新增生产 Direct/Ray 输入共存回归：分别访问 1/3 个热点，抓取并移开/旋转头盔，通过公开复位 API 检查中间姿态、归位、热点/AIR 锚点随动、已访问颜色/进度/详情、三语内容与字体。返回期间以及仍按住旧 grip 时不能重抓，松开后可重选；普通拿放保留进度，显式 ResetTour 清空。

新体验 QA 两次失败分别暴露同步截图阻塞后 `Time.unscaledTime` 仍为旧帧，故第一帧直接走完窗口/复位动画。Editor 测试协程各加一处 `yield return null` 后复测，保留原断言、等待时长和运行时产品行为。失败实际退出 1 的摘要与报告哈希已保存，未冒充产品通过或删去失败历史。

## 本轮验证

**被测提交 `2fcaad12d79bf65f39ab6e27b28763818fcfc94f`，额外 tracked dirty：无。** Windows / Unity 2022.3.62f3c1 / D3D11；每组在独立 Unity 批处理进程串行执行，完整启动参数与实际退出码见[进程证据](../../artifacts/helmet-interaction/qa/runs/20261006T132956Z-pr19-integration/processes.json)。下面都是本次新运行：

| 入口/方法 | 结果 | 退出码 |
| --- | --- | --- |
| HelmetExperienceValidation.RunBatch（组件 API、诊断截图） | 76/76 | 0 |
| TrilingualInspectionRegression.RunBatch（原拿放/观察） | 206/206 | 0 |
| HelmetExplorationValidation.RunBatch（生产输入与 API 分项） | 1486/1486 | 0 |
| LocalizationIntegrationValidation.RunBatch（三语保存主场景） | 84346 次断言通过；含热点 768 次、跨功能 322 次 | 0 |
| LocalizationValidation.RunBatch | 11/11 | 0 |
| EditMode / Museum.Hotspots.Tests | 12/12 | 0 |

267 个主场景被测输入哈希逐一匹配。主场景 82884 次为重复三语检查，不能当作独立测试用例。生产输入使用合成 XR 状态，通过正常 Input Actions 和选择事件；组件 API 的体验/演示步骤另行标注。[结果、摘要与失败记录](../../artifacts/helmet-interaction/qa/runs/20261006T132956Z-pr19-integration/README.md) · [汇总](../../artifacts/helmet-interaction/qa/runs/20261006T132956Z-pr19-integration/validation-summary.json) · [字体验证](../../artifacts/helmet-interaction/qa/runs/20261006T132956Z-pr19-integration/font-report.json)。后续提交仅补文档/证据时，运行时代码和保存资产仍必须与该被测版本一致。

Codex 复核 8 张本轮截图；可见近处菜单遮挡、右缘裁切和演示/入口重叠，未扩大为布局重做。真实头显、人工音量/舒适性试听、专业译文审查、用户研究、性能与 Player 构建均未验证。没有用模拟通过代替这些结果。

## 交接与下一步

完成：#19 与 #21 保存成果兼容整合、三语/字体冲突、共存回归、六组验收与新证据。按用户授权通过 PR #24 合入 main；实际合并后在 Issue #23 记录 merge SHA 并释放共享路径。当前无自动验收阻碍；需组员后续实机和人工检查，不能宣称已取得成员审查批准。

日常打开保存的 MuseumStart 即可，不运行 HelmetPrototypeSetup/HelmetExperienceSetup 或展厅 Builder。未来接手先 fetch main、核对实际 SHA 和 Issues；原目录的未知 ProjectSettings 与未跟踪资料继续保留，不纳入本 PR。已保留 #22 的全面贡献/问题/周索引，更新 #19 的本轮整合与验收状态；21:05 原审计和各历史报告不覆盖。
