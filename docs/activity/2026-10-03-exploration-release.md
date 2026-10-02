# 2026-10-03 自由探索与模型动画发布交接

本次按用户确认，在复测原功能后提交 [Issue #6](https://github.com/Twispra/XRmuseum/issues/6) 的供气、通话探索和配套模型动画。产品源码提交为 `c2bf63cdc42d5730588321e103fd76752db5ea26`，交付分支为 `codex/helmet-exploration-2026-10-03`。功能和资料入口见[使用说明](../helmet-exploration.zh-CN.md)。

## 版本与合并顺序

发布前 fetch 确认 `origin/main = 74556aadae39607b23b8ac2b8fd35e8042019393`，环境 [PR #5](https://github.com/Twispra/XRmuseum/pull/5) 仍未合并。本功能基于其 `39bebc3a86e7f3479c0886539c67cc1389630fb3`，PR 的 base 使用 `codex/museum-atmosphere-2026-10-02`，让审查差异只包含探索部分。没有把探索提交推入环境分支，也没有代组员合并。

建议先审查并合并 PR #5，再把探索 PR 的目标改为 main，核对实际差异后合并。若 PR #5 使用 squash/rebase，或 main 出现新场景改动，整合人需先处理分支历史及场景冲突，再复测；不要直接把探索 PR 合入环境分支。各成员从同一保存场景开发时仍要协调单一写入者。

## 这次交付

- 原 MuseumStart 默认进入 INSPECT；EXPLORE 提供 AIR / VOICE、按需 HINT、随时 REVIEW 和 NEW VISIT。两条线索无必经顺序，也不增加完成门槛。
- AIR 显示双轮双缸手动泵、机构运动和沿软管到达头盔接头的流动标记。VOICE 显示水面电话箱、耳机、独立潜水员端示意单元，以及先发话、后回复的电缆波列与振膜反馈。
- 两段均约 16 秒，结束保留静止模型；STOP 隐藏，REPLAY 重播，ReduceMotion 直接显示静止关系。配套模型不要求双手操作，不参与抓取或碰撞。
- 现有转动、抓取、开窗、剖面、悬停、复位继续使用原输入；模型 RESET / Home 停止演示但保留查看记录，NEW VISIT 才清空记录。

原运行时代码只改了 `HelmetInspectionPalette.cs` 的附加页布局与显示范围；新代码集中在 `Assets/Museum/Features/HelmetExploration/`。主场景原有 319 个序列化块全部保留，新增 148 块，仅 5 个原块接入探索根、热点或面板引用。Git 场景差异较大还包含 Unity 的序列化排序变化，不能只按增删行数判断对象是否被替换。

原头盔模型、Prefab、共享输入、XR Samples、展厅功能目录、Packages 和 ProjectSettings 与依赖版本相同。新资源 `.meta` 齐全、GUID 唯一，引用可解析；没有新增下载模型、贴图、音频或 LFS 二进制资产。已有 LFS 模型在本机已展开，检查和渲染使用实际网格。

## 本次验证与证据

环境为 Windows Editor、Unity **2022.3.62f3c1**，保存场景 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`。以下是自动模拟 XR 输入，经过项目原 Input Actions 和 XRI，不是真实头显或完整官方模拟器键鼠手测。

| 检查 | 结果 | 报告 |
| --- | --- | --- |
| 探索与模型动画 | 351/351，错误 0，实际退出码 0 | [最终动画验收](../../artifacts/helmet-exploration/qa/runs/20261002T173923086Z-exploration-b8e73f03e90f491b92e5322124384704/report.json)，[该轮进程结果](../../artifacts/helmet-exploration/qa/runs/20261002T173923086Z-exploration-b8e73f03e90f491b92e5322124384704/process-results.json) |
| 发布前重新执行原观察功能 | 207/207，错误 0，实际退出码 0 | [主场景报告](../../artifacts/helmet-interaction/qa/runs/20261002T174844913Z-inspection-main-7f439c88187c4b6dbd5ff305347d27cc/report.json) |
| 发布前重新执行原抓取与复位 | 90/90，完成 10 次拿放，错误 0，实际退出码 0 | [基础回归报告](../../artifacts/helmet-interaction/qa/runs/20261002T175137006Z-034e108f89984eb3a58bdb3c2ba9a85b/playmode-validation.json) |

207/90 两次新运行的[实际进程结果](../../artifacts/helmet-exploration/qa/runs/20261003-publish-audit/process-results.json)均正常退出。更早的“207 项通过但退出超时”保留在[初版历史](2026-10-03-helmet-exploration.md)，没有追改旧报告；新结果是另一次运行。

三份报告实际运行时的 HEAD 是 `39bebc3`，另有当时未提交的探索代码、面板和场景修改。提交源码后进行了[来源映射](../../artifacts/helmet-exploration/qa/runs/20261003-publish-audit/source-mapping.json)：351 项报告的 188 份输入、207 项报告的 64 份输入和 90 项报告的 14 份输入哈希全部仍匹配；28 个产品文件经 Git 过滤后的 blob 均匹配 `c2bf63c`，其中 Git 会规范化部分文本换行。188 份输入中的 44 个虚拟 Packages 路径按已安装 PackageCache 解析。这证明测试与提交内容对应，不声称测试发生在产品提交之后。后续交接提交只增加文档和证据。

覆盖包括左右手与远近选择、开窗、剖切方向连续性、悬停、抓取/热点互斥、停止/复位/模块禁用回退、减少动态效果，以及动画阶段与资源复用。最终渲染和 6 次重播检查见[动画活动记录](2026-10-03-exploration-model-animation.md)。代码与场景另经一次只读 AI 审查，未发现阻断问题；这不代替组员的 PR 审查。

## 组员怎样试用和继续做

1. 获取本探索分支，确认 LFS 资源已展开，用指定 Unity 版本打开 `MuseumVR`。打开保存好的 MuseumStart 即可，不重跑旧头盔 Builder 或探索安装菜单。
2. Play 后点击 Game、切英文输入，T/Y 选择左右手柄，射线指向按钮后按 **G 并松开**。G 模拟 Grip → Select；鼠标左键的扳机不是这些实体按钮的选择键。
3. 先试 INSPECT 的抓取、开窗、剖面与复位，再试 EXPLORE → AIR / VOICE → STOP / REPLAY / REVIEW；交换两条线索顺序，检查 ReduceMotion。模型复位与 NEW VISIT 的作用不同。
4. 后续先补真实头显的近距离、坐姿、单手触达、文字清晰度、舒适性和性能检查，再根据反馈调整。当前没有真实参与者结果，也未生成或验收此版本的可执行构建；不声明已通过 Quest 独立运行。

复跑时关闭同工程的其他 Unity 进程，用 `-batchmode -projectPath <MuseumVR绝对路径> -executeMethod <入口> -logFile <日志绝对路径>`，不加 `-quit`；分别使用 `Museum.Exploration.Editor.HelmetExplorationValidation.RunBatch`、`Museum.Inspection.Editor.HelmetInspectionValidation.RunMainBatch` 和 `Museum.Editor.HelmetPrototypeValidation.RunBatch`。每轮同时核对独立报告和进程退出码。

泵和电话箱是有来源依据的设备类型示意，不是已确认与这顶 Mark V 配套的精确复原；时序和机械运动不是历史测量数据。资料、使用边界与 AI 辅助范围已写入[资源记录](../external-resources.md)。工作区既有 `.vscode`、旧截图与其他未跟踪草稿未纳入本次提交，也未清理。
