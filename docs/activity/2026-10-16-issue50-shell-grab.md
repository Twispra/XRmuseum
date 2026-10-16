# 2026-10-16：#50 普通壳体近距抓取修复

## 范围与开工状态

用户明确要求继续排查并修复 [Issue #50](https://github.com/Twispra/XRmuseum/issues/50) 的普通壳体抓取问题，验证通过后提交 PR。本轮只修复该抓取回归，不将其扩大为整个虚拟手模块或设备体验验收；#50 保留为设备调试入口。

已 fetch，基础 `origin/main` 为 `6b6eefe5ef49af932c375c99b5c8581e376acb8b`，包含 #53/#55。原 `E:/XRmuseum` 的 main 及 `ProjectSettings.asset` 已有修改和旧本地输出保留，本轮没有写入原工程。复用独立 E 盘 worktree，在 `codex/issue-50-shell-grab-2026-10-16` 开发；根代理唯一写入，子代理只读审查。开放 #28 为独立建模范围，没有未合并的运行时依赖；写入范围已记录在 #50。

产品修改仅为 `HelmetInspectionController.cs` 的整盔/子件悬停优先级。回归检查在现有 `HelmetHotspotInteractionValidation.cs`，其余为独立证据和文档。未改场景、Prefab、模型/材质、公共 XR/Input、Packages、ProjectSettings 或既有 GUID；Unity 2022.3.62f3c1 / Windows PC VR 不变。没有运行 Install、原型重建、模型烘焙或资源生成器。

## 原因、修复与夹具修订

诊断提交 `d7fb14406f8153fad30ca52759897fa8db39e4d9` 仅增加原失败断言的细节，运行时与 main 相同。完整主场景在 `ordinary shell remains directly grabbable` 失败，0 errors / warnings：手掌追踪误差为 0，本体手部接触过滤通过，窗口 `hovered=True / contact=False`，但 Inspection 过滤器拒绝本体，`selectPossible=False`。原触点、正常 Dynamic 合成输入及原抓取断言均未更换或弱化。

物理手的 Direct 查询球覆盖整个张手包络及近距余量，能提前悬停窗口；物理手实际选择过滤仍要求子件在掌面接触范围内。旧本体过滤器只看子件悬停，导致本体被禁止、窗口又不可触及，两者都无法选。

运行时修复 `dd122531aaff382295fef6b669865414af8e91cb` 在启用 PhysicalHand 的 Direct 路径增加 `IsDirectContact` 判断，只有可实际触及的窗口/剖面工具才能保留子件优先级。Ray 和旧 Direct 保留原悬停规则；同手正在抓子件、取消后必须释放、跟踪失效保护和已持盔时跨热点的规则保留。未扩大抓取距离或关闭碰撞检查。

该修复先让原普通抓取及第一组平滑返回通过，随后暴露旧阅读夹具的退手时序：从热点回到 park 约 2.3 m，却只等 0.12 秒；物理手速度上限 6 m/s，代理尚未退开，`Progress` 字形射线被右手挡住。`62c299462f53dd214f50b4ee77a1db997a7f1121` 将该固定等待改为原输入停泊、`Approach` 安全退手和 `WaitForPalm` 实际到位检查；没有直接挪手代理、隐藏手碰撞体或放宽遮挡断言。

## 验证与证据

本轮验证使用保存的主场景、原 Input Actions、真实 XRI Direct/Ray 路径和合成设备状态。LFS 模型/音频已展开。被测源码 `62c2994` 开跑时无额外未提交产品改动；只有本代理旧 QA 和本轮输出。运行期间仅补充文档，未修改工程源码。命令、dirty 清单、退出码和版本记录在独立 process 文件；原始报告、日志、截图留本地，新摘要记录其 SHA256，不覆盖历史报告。

- `LocalizationIntegrationValidation.RunBatch`：**84,730 项通过，0 errors / 0 warnings，退出 0**。[摘要与分类统计](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/main-validation-summary.json)。其中热点子流程 **792 项 / 20 次生产 Direct/Ray 选择**通过；原普通抓取触点、窗口宽悬停但未接触的前提、本体可选和真实 Grip 选中均通过三次；[热点完整报告](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/main-hotspot-interaction.json)。
- `MuseumPhysicalHandsValidation.RunBatch`：**182 项通过，0 errors / 0 warnings，退出 0**；[手模块报告](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/physical-hands-report.json)。覆盖正常输入的单手拿放、1→2→1→0、持盔开窗、Home 取消、持续 Grip 禁止重抓与松开后恢复，以及窗口/剖面同手互斥的 API 夹具。
- `HelmetInspectionValidation.RunMainBatch`：两轮均 **207 项行为检查通过、errors 为空**，包含窗口、剖面实际操作；[首轮报告](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/inspection-main-report.json)、[复跑报告](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/inspection-main-retry-report.json)。但两轮都在测试结束、Input System Shutdown / license disconnect 之后卡住，日志停止更新、进程仍运行；核对 PID、命令和工程归属后仅结束对应无界面测试进程。退出码为 **-1，不能记为正常退出通过**；[首轮退出记录](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/inspection-shutdown-local.json)、[复跑退出记录](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/inspection-retry-shutdown.json)。正常退出异常仍需独立排查；完整主场景和手模块不复现该退出问题。
- `MuseumPhysicalHandsBuild.RunBatch` / `-buildTarget Win64`：**Windows64 Development Succeeded，0 errors / 2 条既有 OpenXR 可选迁移 warnings，退出 0**；[构建报告](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/windows-build-report.json)。完整目录为 `E:/Codex/Builds/XRmuseum/issue50-shell-grab-20261016-62c2994/`，构建报告总量 214,112,679 bytes，包含 exe、_Data、UnityPlayer 和 Mono；[272 个产物文件指纹](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/build-output-manifest.json)。程序未启动，产物不进 Git，未改变平台或启用可选迁移。
- 全部进程结束后 **1,139 个工程文件 SHA256 全部一致**；构建设置、场景列表和预加载资产状态恢复，未带入产品 dirty 文件；[来源复核](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/final-source-verification.json)。后续提交仅包含文档和证据。[完整摘要](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/summary.json)。
- 基线失败与中间夹具失败分别保留为负向证据：[失败摘要](../../artifacts/helmet-interaction/qa/runs/20261016-issue50-shell-grab/negative-results.json)，不作为通过报告。

## 限制与交接

本次自动验证不等于真实头显/手柄、手感、双眼对齐、舒适性、性能或用户研究通过。观察专项正常退出异常尚未解决，不能把其 207 项行为通过写成整套进程正常完成。用户此前反馈的抓握空隙、左摇杆卡顿和整个 #50 的设备调试不能据此一并写成解决。PR 供审阅，本轮用户未要求合并；接手者使用保存场景和原版本编辑器，不运行生成器，继续用实际设备核对普通壳体各侧、窗口/剖面优先级、双手交接和松手重抓。

Codex 辅助范围为过滤器原因诊断、运行时修复、正常输入回归夹具、自动验证和交接文档；没有新增外部模型、媒体、字体或包，也不代替组员人工批准。
