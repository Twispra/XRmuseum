# PR #51 视角回中修复与合并交接

日期：2026-10-14（北京时间）。用户在审查后明确要求「请你修复并合并」。本轮继续原 [PR #51](https://github.com/Twispra/XRmuseum/pull/51) / `codex/view-reset-panel-gap-2026-10-14`，以同一实际负责人在独立 `E:/Codex/worktrees/pr51-fix-2026-10-14/XRmuseum` 修改；子审查只读。原 checkout 开工为 `main` / `81a8e0c`，保留既有 ProjectSettings.asset 修改及未跟踪资料。

## 改动与版本

原 PR head `e5a86acbec262457dd13440103f24390fe8a127a` 与实际 main `81a8e0c5200dcbfd0c576d7f24bd2a0800eba3b3` 在本轮合并为 `3dd3df2`，保留 #49 跟踪/高度及 #52 虚拟手成果。主场景、Prefab、公共 XR 输入、packages 和 ProjectSettings 不在本轮产品修改范围。

- `39e9fd0`：保存初始 Origin 的地面位置，回中时只抵消当前相机的水平偏移；保留当前垂直追踪高度及 Device 偏移。坐下、蹲下再站起、非零地面高度和缩放均不再被固定 1.65 m 目标改变。仍校正水平朝向并重新放置个人面板。
- `05e0345`：运行时克隆 CENTER 按钮后，立即刷新 palette 的交互缓存；相机晚出现时，新按钮也参与悬停/选择冻结。
- `c64dc0e`、`7ca0f3c`、最终 **`0bc65cd972c2d77fabf20adbd10319524d630e19`**：加入可重跑的 `VisitorViewRecoveryValidation`，包含姿态/API 夹具、生产合成射线/键盘输入、晚相机生命周期与三语字形/布局。最终补齐批处理启动前的隔离键盘/鼠标，避免官方模拟器 UI 初始化的测试环境异常。两个产品修复文件自 `05e0345` 后未再改变。

## 验证与限制

环境：Windows，Unity **2022.3.62f3c1** / XRI 2.6.4 / Windows PC VR。打开隔离工程前 LFS pull / checkout 完成，模型/音频工作区为展开对象；使用空闲工作副本的 Library 导入缓存，manifest 与 packages-lock 哈希一致，没有更改依赖。每次跑测的 tracked 文件干净，额外 dirty 只有本轮未跟踪 QA 产物；没有保存产品场景或重跑原型生成器。

| 验证 | 被测版本与证据 | 结果 |
| --- | --- | --- |
| 旧代码高度问题 | `c64dc0e` 的[高度夹具摘要](../../artifacts/helmet-interaction/qa/runs/20261014-pr51-fix/height-before-summary.json) | 43 个高度检查中 14 项失败；首次运行的 controller 查找夹具另有错误，未把它算成产品缺陷，后续修正为读取保存的 inactive 控制器 |
| 高度修复后、缓存修复前 | `39e9fd0` 的[原始报告](../../artifacts/helmet-interaction/qa/runs/20261014T112514739Z-view-recovery-b496c780c8d74be79e94e247485345f0/report.json) | 56 项中仅晚创建按钮缓存、射线悬停冻结两项失败；已实际复现第二个审查问题 |
| 最终定向验证 | **`0bc65cd`** 的[完整报告](../../artifacts/helmet-interaction/qa/runs/20261014T113647376Z-view-recovery-3673efd47bbd4de5912759135f1d81ba/report.json)、[进程](../../artifacts/helmet-interaction/qa/runs/20261014-pr51-fix/focused-settled-process.json) | **86/86 通过，报告 errors=0，Editor 正常退出 0** |
| 退出后来源核对 | [指纹复核](../../artifacts/helmet-interaction/qa/runs/20261014-pr51-fix/post-exit-verification.json) | 报告登记的 284 个文件与退出后实际内容一致，0 差异；其中 44 个 Unity 虚拟 Packages 路径按锁定版本解析至 PackageCache |
| 保存主场景回归与 main 对照 | 候选 `05e0345`、未加入 #51 的 main **`81a8e0c`**；[逐项对照摘要](../../artifacts/helmet-interaction/qa/runs/20261014-pr51-fix/main-regression-comparison.json)、[候选进程](../../artifacts/helmet-interaction/qa/runs/20261014-pr51-fix/main-process.json)、[main 进程](../../artifacts/helmet-interaction/qa/runs/20261014-pr51-fix/main-control-process.json) | 两者都在 1082 项检查中的 `ordinary shell remains directly grabbable` 失败，退出 1；两份报告 errors/warnings 为 0。**完整回归未通过**，没有放宽或跳过断言 |

定向验证入口：`Museum.Exploration.Editor.VisitorViewRecoveryValidation.RunBatch`；主场景对照入口：`Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch`。均以 `Unity.exe -batchmode -projectPath <隔离 MuseumVR> -executeMethod <入口> -logFile <独立日志>` 运行，省略 `-quit`，由验证器退出 Play 后延迟退出 Editor。命令、额外未跟踪文件和退出结果在上述进程 JSON 中。后续提交只整理证据与文档，产品来源仍与已测版本一致。

86 项覆盖 Floor 0.85/1.20/1.65/1.80 m、蹲姿重置后站起、Device 向下姿态、3 m 平台、缩放与重复回中；保存主场景中的实际 Input Actions / XRRayInteractor 选择 CENTER、F11，以及晚相机克隆后的缓存和悬停冻结。非空发现记录、提示数、页面、年代、展品姿态和复位计数在点击前后保持；没有手工调用 SelectEnter/SelectExit/TriggerProgrammatically 或 InputSystem.Update。

三语 Reset View 译文、保存字形和四个顶部标签溢出检查通过；[英文](../../artifacts/helmet-interaction/qa/runs/20261014T113647376Z-view-recovery-3673efd47bbd4de5912759135f1d81ba/tabs-English.png)、[简体](../../artifacts/helmet-interaction/qa/runs/20261014T113647376Z-view-recovery-3673efd47bbd4de5912759135f1d81ba/tabs-Chinese.png)、[繁体](../../artifacts/helmet-interaction/qa/runs/20261014T113647376Z-view-recovery-3673efd47bbd4de5912759135f1d81ba/tabs-TraditionalChinese.png)诊断近景已查看，文字完整，Help 与 Reset View 有可见空隙。这些是实际运行标签的单眼正交近景，不是头显双眼视图或可读性研究。

完整回归的失败也存在于当前 main，未证明其根因，继续交由 #50 / #52 的设备与近距抓取调试处理。本轮没有修改手部、窗口抓取或热点过滤器，也没有把定向 86 项替代整个主场景通过。启动日志仍有无连接头显时的 OpenXR runtime 消息；报告 errors=0 仅指验证器采集阶段。真实头显舒适性、完整手柄流程、持有部件时回中、性能、当前功能构建和用户研究均未验证。

## 合并与接手

两个只读子审查核对最终产品差异，未发现新的阻断问题；这不冒充另一位成员的人工 APPROVED。用户已经明确授权本次修复与合并。记录提交时修复、定向验收及来源核对完成；使用正常 GitHub merge 并锁定推送后的 head，实际 merged 状态和 merge SHA 以 [PR #51](https://github.com/Twispra/XRmuseum/pull/51) 最终回读及交接为准，不改保护规则或强推。

合并后贡献者 fetch main，用 Reset View 检查站姿、坐姿、蹲下后的地面一致性，再反馈真实射线操作和舒适性。既有普通外壳近距抓取失败继续沿用 #50 的协作调试范围。本轮只提交有用报告、进程、来源摘要与三语近景；原始故障日志、重复报告及大量缓存留本地。原目录既有修改继续保留，周索引由既定汇总人维护。

Codex 辅助实现修复、验证器、合成输入、截图检查、只读复审及本记录。没有新增第三方素材，也没有代写成员贡献、设备通过、课程研究或 AI 使用许可。
