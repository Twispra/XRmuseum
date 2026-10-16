# 2026-10-16：修复 #55 非抓取移动遗漏并整合 #53

## 范围与版本

用户明确要求修复 [PR #55](https://github.com/Twispra/XRmuseum/pull/55)，验证通过后正常合并 [PR #53](https://github.com/Twispra/XRmuseum/pull/53) 和 #55。本轮继续 [Issue #54](https://github.com/Twispra/XRmuseum/issues/54) 的“抓取或移动时隐藏，停止后不恢复，再选热点才恢复”范围。原作者的实现和 2026-10-15 报告保留，以下是新增的修复与独立验证。

开工时原目录 main 为 `502a3c1`，已有 `ProjectSettings.asset` 修改及旧本地证据；全部保留。实际远程 main 为 `4d8796b`，#53 head 为 `9a1367a`，#55 head 为 `754238e`。写入仅在独立 E 盘 worktree 的 `codex/hide-hotspot-panel-on-move` 分支进行；并行子代理只读审查，根代理是唯一写入者。没有修改场景、Prefab、模型、材质、公共 XR / Input、Packages 或 ProjectSettings，也未运行原型生成工具。

## 原因与修复

原 #55 只监听整盔的 `selectEntered`。保存主场景中调用个人面板实际使用的 `RotateDisplay(30)` 后，整盔旋转约 30°、抓取事件为 0，解释透明度仍为 1；原 23 项测试通过，新增移动探针失败。对应 [负向结果](../../artifacts/helmet-interaction/qa/runs/20261016-pr55-pose-fix/pr55-before-fix-movement.xml) 和 [原探针](../../artifacts/helmet-interaction/qa/runs/20261016-pr55-pose-fix/before-fix-movement-probe.cs) 保留，不能当作通过证据。

修复提交 `d543162` 保留抓取事件的即时隐藏，增加整盔世界位姿检测：每次 `Visit` 捕获父级 `XRGrabInteractable` 的位置/旋转；在 `LateUpdate` 比较固定检查点，位移超过 2 mm 或旋转超过 0.5° 时停止计时并隐藏。检查点不逐帧重置，持续的小幅移动可以累计触发；这两个容差是原型抗抖参数，不是实测数据。停止移动、松手、刷新语言不恢复旧解释，再次选择同一或其他热点才捕获新姿态并重启 30 秒保持 / 1 秒淡出。禁用后重启保留已有检查点；访问进度及原文/翻译入口不变。

独立 Hotspots assembly 沿用 XRI 依赖，不引入 Inspection 的程序集耦合。测试通过反射调用默认程序集中的生产 Turn、Tilt、展品复位与 Reset View API；没有替换生产运行逻辑。

## 验证与证据

环境为 Windows、Unity **2022.3.62f3c1**、现有 XRI 2.6.4。测试均运行保存的工程；LFS 检查显示 Unity 所需模型/音频均已展开。开跑时没有额外未提交产品源码；已有本代理的旧 QA 输出和新 QA 输出。组合热点运行期间仅编写本活动、accessibility 和 external-resources 文档，没有改动 `MuseumVR/`。运行命令、完整 dirty 清单、退出码和被测版本见本轮 process 文件。

- #53 原 head `9a1367ae5ad338e3dfea7fce1e906f8519081bc6`：`Museum.Exploration.Editor.VisitorViewRecoveryValidation.RunBatch` **95/95**，errors 为空，退出 0；[报告](../../artifacts/helmet-interaction/qa/runs/20261016-pr55-pose-fix/pr53-before-integration-report.json)。#53 已正常合并为 `5052a6fb8f8eb3646e00ddfa0819f57806b20cce`。
- #55 修复源码 `d5431629d9e156dc585fd90dfe418317014f6ff1`：`-runTests -testPlatform EditMode -testFilter Museum.Hotspots.Tests` **33/33**，0 failed / skipped，退出 0；[结果](../../artifacts/helmet-interaction/qa/runs/20261016-pr55-pose-fix/hotspot-tests.xml)。其中 10 项新增用例覆盖位移/旋转、累计小幅移动/抖动、停止保持隐藏、语言刷新、重选与 30+1 秒计时、禁用期间移动、子件移动、生产 Turn/Tilt、展品复位及 Reset View 不误隐藏。
- 同步含 #53 的实际 main 后，组合候选为 `ed36889744660e87aa7635f423d6ff6910e91de3`。视角/三语 Help 专项再次 **95/95**，errors 为空，退出 0；[组合报告](../../artifacts/helmet-interaction/qa/runs/20261016-pr55-pose-fix/combined-view-recovery-report.json)。组合热点再次 **33/33**，0 failed / skipped，退出 0；[组合 XML](../../artifacts/helmet-interaction/qa/runs/20261016-pr55-pose-fix/combined-hotspot-tests.xml)。两个进程结束后 **1,139 个工程文件 SHA256 全部一致**，没有额外产品源码变化；[指纹复核](../../artifacts/helmet-interaction/qa/runs/20261016-pr55-pose-fix/combined-source-verification.json)。后续提交仅包含文档与证据。

汇总入口：[本轮证据目录](../../artifacts/helmet-interaction/qa/runs/20261016-pr55-pose-fix/)。原始 Unity 日志和诊断截图留本地，不覆盖已有运行目录。新测试脚本沿用审查探针首次导入的 meta / GUID；没有重新生成既有资源的 meta。

## 限制与交接

普通壳体近距抓取的完整主场景断言在旧 main 与旧候选上都已失败，继续由 [Issue #50](https://github.com/Twispra/XRmuseum/issues/50) 跟进；本轮只运行受影响的组合专项，不宣称完整主场景回归通过。EditMode 的 `RequestResetToDisplay` 覆盖即时复位分支，实际平滑返回帧、真实手柄拿放、头显双目可读性、Windows Player 构建与用户研究仍未验证。不得将此前设备视角反馈扩大为本次热点功能的实机通过。

用户已授权正常合并；最终合并结果和 SHA 以两个 PR 的 merged 状态回读为准。下一步有设备的成员检查握把选择、整盔移动、再次选择和阅读体验，并继续排查 #50。AI 辅助包括原因复现、位姿检测、测试、只读复审和交接文档；没有新增第三方素材、字体或包，不代替另一位成员的人工批准，也不改变团队协作规则。
