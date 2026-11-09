# 展览旁白、字幕与声音控制

2026-11-09（北京时间）。[Issue #63](https://github.com/Twispra/XRmuseum/issues/63) 以主线 `6088c736` 和 [PR #72](https://github.com/Twispra/XRmuseum/pull/72) 的 `cac0a28` 年代门控为基线。工作分支为 `codex/issue-63-narration`。#64 的右台采用决定与本项的触发位置无依赖。

## 实施验收

| 分值 | 必过项 |
| --- | --- |
| 25 | 三段原脚本、事实范围、简繁英字幕、普通话和英文音轨对应，来源和音轨生成可核查。字幕无缺字和溢出。 |
| 25 | 保存场景中的就地入口只在访客确认后启动，可停止及重播。已显示的 Mark V 和展开的演示决定 AIR／VOICE 旁白，其他情况播放操作提示。 |
| 35 | 两类音源在旁白期间互斥。静音停声音并保留剩余字幕，重新开声等待下一次主动播放。语言切换、换档、关闭或变更演示、复位和离开场景停止旧片段。减少动画时仍可播放。 |
| 15 | 保存场景的实际模拟输入与既有旋转、开窗、剖切和演示回归通过，保留被测版本、产物和独立复审。 |

四类均通过、独立复审至少 90 分且无阻断方可结束实施阶段。人工试听、音量舒适度、真实头显清晰度和可达性另行验收。

## 内容、触发与边界

| 片段 | 访客如何启动 | 事实范围 |
| --- | --- | --- |
| 首次操作 | 在固定台面按「播放语音」，当前无可讲解的连接演示时播放。 | 现有实体站的外壳按住移动、松手停止、实际玻璃按下松手开合。 [实体站操作](../diegetic-ui.zh-CN.md) |
| 供气 | Mark V 档先打开 AIR，再按同一入口。 | 解释当前演示中的水面泵、软管和头盔供气。对应 Mark V 标准空气式连接及设备类型资料。 [馆藏 Mark V](https://navalunderseamuseum.org/mk-v/) · [泵和电话史料](../../artifacts/connection-demo-refinement/references/research.zh-CN.md) |
| 通话 | Mark V 档先打开 VOICE，再按同一入口。 | 解释潜水员与水面团队的双向示意。1916 年手册印刷页 37、39 的泵和 41 的电话以及 RMG 同类设备是类型参照。展项使用解释性组合。 [手册及馆藏索引](../../artifacts/connection-demo-refinement/references/research.zh-CN.md) |

原脚本与三语文案按每段匹配。简体与繁体共享普通话，英语使用独立英语音轨。每条短音轨显示一张与该段对应的完整字幕卡。音频停下后，字幕在该段剩余时间内继续可读。结束、停止或切换语言后需要再次主动点击。需要切换供气／通话时，先结束旧片段再提供新片段。

音轨由 eSpeak NG 1.52.0 的原生 formant 合成器生成，采用 `sit/cmn` 与 `gmw/en-US`。原始脚本为本任务编写。公开仓库内的 WAV 为合成输出，程序、安装包、语音数据库与外部馆藏媒体保留在产品之外。 [生成脚本、精确文本及验证](../../artifacts/narration/20261109-issue63/) · [eSpeak NG](https://github.com/espeak-ng/espeak-ng/tree/1.52.0) · [GNU 程序输出许可说明](https://www.gnu.org/licenses/gpl-faq.en.html#WhatCaseIsOutputGPL)

## 工程接入

实体站固定台面提供一个按下松手确认的旁白入口。旁白独立于设备运动状态。旁白音轨播放前停止设备音源，设备动画和线路按原流程继续；片段停止后设备音源按当前演示状态恢复。所有新增文字通过 `MuseumText.Set` 和同一语言通道。五条文案已核对现有静态图集覆盖。

保存场景、Prefab 和公共输入保持既有版本。原工作区仍在 `codex/phase2-technical-note-2026-10-18`，既有 ProjectSettings 改动和未跟踪资料与本任务隔离。Unity 工程仅在新 worktree 验证。

## 实际验证与独立复审

- 保存表同步自然退出 0，1,299 个已有工程文件中仅保存表变化。沿用 Unity 生成的五条新记录，保留 266 条原记录的字节表示。首次同步还发现新音轨 meta 的 GUID 被写成两行，冻结前已把原 GUID 归并到字段并核对六个值互不重复。
- 首轮 `da0da33` 的旁白专项 175/175、本地化模块通过，两个进程自然退出 0，各核对 1,317 个受跟踪工程文件零差。首轮单眼截图中字幕底板遮住既有 Show panel 的部分字母，先修正字幕位置再验收。独立初审也要求将「演示结束」表述明确为「关闭或变更演示」。
- 修复轮一将字幕向台面右移 0.16 m，补字幕底板与 Show panel 标题的九组投影检查和截图。轮二在创建阶段登记默认字幕，再隐藏对象，修正三语集成对隐藏标签的来源审计失败。

| 批处理入口 | 被测提交 | 结果 |
| --- | --- | --- |
| `Museum.Localization.EditorTools.LocalizationValidation.RunBatch` | `da0da33` | 保存本地化模块通过。 [退出记录](../../artifacts/narration/20261109-issue63/qa/localization-process.json) |
| `Museum.Diegetic.Editor.HelmetDiegeticValidation.RunBatch` | `f372c77` | 实体 UI 1043/1043，含既有开窗、旋转、剖切、演示和旁白用例。 [报告](../../artifacts/narration/20261109-issue63/qa/diegetic-report.json) |
| `Museum.OperatorPanel.Editor.OperatorPanelInteractionValidation.RunBatch` | `f372c77` | 面板 271/271。 [报告](../../artifacts/narration/20261109-issue63/qa/operator-report.json) |
| `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch` | `e800b78` | 三语集成 131146/131146。 [摘要与哈希](../../artifacts/narration/20261109-issue63/qa/integration-summary.json) |
| `Museum.Diegetic.Editor.HelmetDiegeticValidation.RunNarrationBatch` | `e800b78` | 旁白专项 184/184。 [报告](../../artifacts/narration/20261109-issue63/qa/narration-report.json) |

环境为 Windows／Unity 2022.3.62f3c1。批处理使用 `-batchmode -projectPath <worktree>/MuseumVR -logFile <独立日志> -executeMethod <入口>`。最终版本 `e800b78` 比完成实体 UI 和面板验证的版本仅增加默认隐藏字幕的一次本地化绑定。保留提交前后的独立进程记录，验证前核对 26 个实际展开的 Unity LFS 文件哈希，其中六份为新 WAV。模拟 Grip 走生产射线选取和松手确认，部分设备展开状态使用现有公开命令夹具。音源互斥、实际 Unity 播放状态、三语字幕、静音保持字幕、减少动画、语言切换和场景卸载均有证据。 [三语字幕截图](../../artifacts/narration/20261109-issue63/qa/trilingual-narration.png) · [英文首段完整视图](../../artifacts/narration/20261109-issue63/qa/orientation-english.png)

所有最终正向批次自然退出 0，各自 1,317 个 tracked 工程文件零差。独立子 Agent 复审 **98/100**，内容和音轨 25/25，入口和情景 24/25，声音与生命周期 34/35，回归证据 15/15。四类必需组通过，无阻断。扣分对应已测输入与单眼布局范围，以及实际播放状态与听感之间的边界。人工试听、发音、响度、目标头显内可懂度、真实控制器与双眼可读性仍待验。本轮验证覆盖保存场景的编辑器，不包含新 Windows Player 构建。
