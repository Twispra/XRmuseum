# 2026-10-03 自由探索功能与交接

本页保留开发当时的状态。当前 16 秒设备模型版本、发布前回归和提交信息以[发布交接](2026-10-03-exploration-release.md)为准；下文“仅本地/未推送”和初版 5 秒演示均为历史记录。

用户先要求复测并发布已有展厅，再继续“自由探索、需要时提供引导”的想法。前一阶段的回归与发布见[展厅记录](2026-10-03-gallery-release.md)及 [PR #5](https://github.com/Twispra/XRmuseum/pull/5)。环境 PR 已发布，尚未合并。本记录只涉及后续 [Issue #6](https://github.com/Twispra/XRmuseum/issues/6)；新探索功能先本地试用，尚未推送或建立新 PR。

## 基础与范围

- 分支：`codex/helmet-exploration-2026-10-03`。
- 基础 HEAD：`39bebc3a86e7f3479c0886539c67cc1389630fb3`，明确依赖未合并的环境 PR #5。
- 环境：Unity `2022.3.62f3c1`，当前 Windows PC VR / OpenXR 工程；不变更为 Quest 独立 Android 平台。
- 开工时已有未跟踪 QA 图片、草稿和 `.vscode`；不把这些历史文件当作本轮成果，也不清理或批量提交。
- 运行时文件、Editor 整合工具与文档按子任务分开写入；主场景由主任务统一整合，后台文档任务不操作 Unity、GitHub 或场景。

## 这次做什么

保留原 MuseumStart、头盔和观察工具，在同一面板加入 INSPECT / EXPLORE 页签。AIR 与 VOICE 可以任选，HINT 由游客主动调用，REVIEW 随时可看。没有完成度门槛、强制讲解顺序或停留超时提醒。

供气线索使用现有 `GB_AirConnector` 简化接头作为观察目标，也提供面板按钮入口，不要求访客实际绕到展台背面。通话部分缺少对应实体模型，因此用明确标注的概念图表达双向消息，不编造内部电话设备。首次选择线索演示，约 5 秒后保留静态图；ReduceMotion 直接显示静态图。重复选择停止并隐藏图，只打开说明；REPLAY 再显示，STOP 停止并隐藏。模型复位保留查看记录，NEW VISIT 清空本次记录。查看记录不等于学会知识。

整合时修正了初版文字相互挤占的问题，并补上模块停用时的回退：探索根或 Controller 被禁用后，面板恢复原 INSPECT 工具、隐藏新页签，不再把访客困在失效的探索页。这两项修正及后续示意图标题、字体调整均已纳入最终 282 项复测。

操作保持原 G → Grip → Select 映射，不将鼠标左键悄悄改成这组按钮的点击键。使用步骤、代码入口、资料和表达边界见[自由探索说明](../helmet-exploration.zh-CN.md)。

## 验证记录

下列报告来自 `39bebc3` 加本轮未提交代码与场景改动，不能仅凭基础 HEAD 复原被测版本；报告各自保存了来源与输入信息。目录使用 UTC，本地日期为 10 月 3 日。

| 检查 | 已得到的报告结果 | 证据与限制 |
| --- | --- | --- |
| 探索功能与文字布局 | 264/264，报告 `status=passed`，记录错误 0 | [报告](../../artifacts/helmet-exploration/qa/runs/20261002T165332521Z-exploration-629640d0ae004be18ac8c393ac2f029f/report.json)；外层进程监控 240 秒超时，退出码为空 |
| 新场景上的原主场景交互 | 207/207，报告 `status=passed`，记录错误 0 | [报告](../../artifacts/helmet-interaction/qa/runs/20261002T165754698Z-inspection-main-4a9de0efe3444c0694d69fc1ba1e795a/report.json)；同样存在进程退出超时 |
| 最终探索复测 | 282/282，记录错误 0，实际退出码 0 | [报告](../../artifacts/helmet-exploration/qa/runs/20261002T170732895Z-exploration-117e2a40aa894881899cd581d657fbfa/report.json)；补入右手 EXPLORE / AIR、左手 VOICE 在 0.55 m 距离的射线选择与示意图文字布局检查 |
| 当前新场景的原拿放回归 | 90/90，10 次拿放，记录错误 0，实际退出码 0 | [报告](../../artifacts/helmet-interaction/qa/runs/20261002T170518769Z-9e8b9d7161614358b93a5b7223a1a0ab/playmode-validation.json) |

报告里的检查通过与 Unity 进程是否正常退出是两个结论。[进程结果](../../artifacts/helmet-exploration/qa/runs/20261002T170732895Z-exploration-117e2a40aa894881899cd581d657fbfa/process-results.json)保留了前几轮超时及最后两次正常退出。早期退出日志出现 `Leftover web requests after shut down`，最终运行也出现 Unity 云配置服务连接失败、改用默认配置的记录。新验证器在退出 Play 后用 Editor.update 非阻塞等待 20 秒再退出，最终实际退出码为 0；这是测试工具的退出稳定性措施，没有修改产品退出逻辑，也不能据此断言原生退出问题的根因已查明。旧 207 项这轮仍按“断言通过、退出超时”记录，不追改为成功。

复跑探索：关闭其他使用同一工程的 Unity 进程，使用 `-batchmode -projectPath <MuseumVR绝对路径> -executeMethod Museum.Exploration.Editor.HelmetExplorationValidation.RunBatch -logFile <日志绝对路径>`，不要加 `-quit`，完成后核对报告和实际进程退出码。交互编辑器也可用 Museum → Exploration → Validate main museum (Play Mode)，会临时驱动模拟设备并在结束后退出 Play；不是访客操作入口。

探索检查涵盖两种线索顺序、重复选择、提示与回顾、停止和重播、模型 RESET / Home 与 NEW VISIT、热点抓取互斥、面板分页及模块禁用回退。文字在矩形内没有溢出，也不等于头显中容易阅读。最终[静态检查](../../artifacts/helmet-exploration/qa/runs/20261002T170732895Z-exploration-117e2a40aa894881899cd581d657fbfa/static-audit.json)核对 62 个原依赖哈希相同；场景原 319 块保留，新增 148 块，原有 5 块仅增加探索根/热点/面板引用与高度参数，原 XR、相机及原物理配置未被替换。

已查看最终供气、通话与回顾渲染，示意图标题和端点文字不再互相覆盖。回顾近景见[截图](../../artifacts/helmet-exploration/qa/runs/20261002T170732895Z-exploration-117e2a40aa894881899cd581d657fbfa/03-review-panel-closeup.jpg)。背面接头仍会被真实外壳挡住，需旋转模型查看；供气图没有偷偷使用穿透渲染假装前方能看到背面。

自动输入、Unity 单眼截图、完整官方键鼠操作、真实头显体验是不同证据。即使自动检查通过，也不能据此声称真实设备、无障碍标准、用户学习效果或当前可执行构建已验收。

## 组员接手时

1. 先确认实际分支与依赖状态；环境 PR 尚未合并时，不按 main 已包含这套展厅处理。本轮本地文件尚不能直接供其他成员从远程获取。
2. 从保存好的 MuseumStart 进入 Play，先试原 INSPECT，再试 EXPLORE。无需重跑旧生成工具或首次安装菜单。
3. 保留 `.meta` 与现有资源引用。调整文案或示意逻辑优先在 HelmetExploration 内完成；改共享面板或主场景时重新协调写入范围。
4. 后续优先补设备检查与真实访客试用；若增加电话部件或内部气路，先补可靠资料与模型依据，再扩展演示，不把概念图升级成未经考证的“真实结构”。

本轮重新核验了 Naval Undersea Museum 与 DESCO 产品页正文，来源链接见功能说明。页面图片未导入或再分发；动画参数和灰模交互参数均为原型设计。AI 辅助范围为代码、文案、资料核对和检查准备；不虚构组员分工、参与者研究或实机结论。
