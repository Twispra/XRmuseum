# 组员如何查看、认领与继续精修

统一入口：[Issue #33](https://github.com/Twispra/XRmuseum/issues/33)。本轮负责人 Fttt1003，工作分支 `codex/connection-demo-assets-2026-10-07`。这项任务服务于既有的头盔观察流程，不改变主场景操作或年代切换。

## 查看与认领

只想看效果：在本目录 README 打开预览图片；下载 `preview/review.html` 原文件，电脑浏览器打开即可旋转和试听。模型不需要 Unity 才能查看。审阅时请说明 AIR / VOICE、步骤／秒数、语言，以及截图中哪里不清楚。

想继续做：先在 Issue 留一条“我认领××；改这些文件；预计交付××”。不要另外为每个零件新开 Issue。按实际任务建立自己的分支和 PR，关联 #33。工作基线应是本工作 PR 的实际最新 commit，**不是默认假设 main 已有这些资产**；开始前 `git fetch origin` 并核对分支／状态。

| 可认领方向 | 文件范围示例 | 有用的交付 |
| --- | --- | --- |
| 历史核查 | `references/` 的独立新增记录 | 同型号尺寸、藏品来源、形状对照；指出哪项估算有新证据 |
| 泵造型 | `sources/parts/Pump_ROOT.blend` 及对应泵导出 | 气路、螺纹、木箱和金属细节；保留运动节点与出气锚点 |
| 电话造型 | 两个电话分件源文件及对应导出 | 线缆、插头、耳机和发话器；保留膜片与连接锚点 |
| 声音评审／精修 | `audio/`、`scripts/synthesize_audio.py` | 三方案比较、可理解度／疲劳反馈、修改后的音轨与来源说明 |
| 演示表现 | `scripts/viewer.js`、模板及时间轴 | 视线引导、色彩／动画节奏、暂停和静态替代，不改变历史事实口径 |
| 后续 Unity 适配 | 另行协调的新功能目录／独立 Prefab | 导入、接口绑定、性能优化与原有功能回归；不提前改公共场景 |

同一个 `.blend`、脚本或导出文件一次只由一个人修改。`equipment.blend` 是共享总源文件，分件精修后由一个整合人组装。认领分件的成员不要同时改总源；源和导出配套提交，别只覆盖 FBX 而丢失可编辑源。先用 `git lfs pull` 确认 `.blend`、FBX、WAV 是实际文件，再打开。GLB 与浏览器 HTML 当前为普通 Git 文件。

## 可保留的接口

- 泵根节点 `Pump_ROOT`；`Anchor_AirOutlet` 为软管出气端。
- 水面电话根节点 `SurfaceTelephone_ROOT`；`Anchor_TelephoneOutlet` 为通话线端。
- 潜水员电话示意根节点 `DiverTelephone_ROOT`；`Anchor_DiverTelephone` 为示意连接端。
- 泵运动节点：`PumpCrankPivot`、`PumpSlider0/1`、`PumpSling0/1`、`HandQuillPivotL/R`、`PumpGaugeNeedle0/1`。
- 发话膜片：`SurfaceDiaphragm`、`DiverDiaphragm`。名称、父子关系、局部坐标改变时在 PR 写明，否则动画或锚点绑定会失效。
- 展示参考头盔的 `Anchor_AirOutlet` 与 `Anchor_TelephoneOutlet` 分别接 AIR / VOICE。将来适配三个时代应逐个核对，不假设所有头盔采用相同位置或相同电话结构。

## 后面接入 Unity 时

现有 `HelmetConnectionDemonstration` 有自己的播放和结束逻辑。本轮的浏览器控制器不是 Unity 可直接挂载脚本，也没有声称动画已经接进运行时。

接入人先协调文件范围，再导入独立设备 Prefab。以明确的 AIR / VOICE 状态绑定线路两端，使用统一时间轴控制模型、粒子和音轨：播放、暂停、重播、跳步、复位、切模式、物体禁用时都要一起处理。不要在旧脚本旁边再启动一个互不知情的 16 秒计时器。浏览器的 [timeline.mjs](scripts/timeline.mjs) 可作流程对照，秒数是可调整的展示参数。

剖开展示目前是浏览器侧的气缸材质裁切，不是 FBX 内置剖切动画。未来需做专用结构件或复用项目已经协调好的剖切方式；不能以关闭整个气缸来冒充完整断面。音轨默认关闭，有视觉替代；接入时沿用项目三语键和近距离可用控制，不把本页 DOM 文本复制成 Unity 硬编码。

之后至少验证：原前窗开合／头盔旋转／剖面／复位、三语、年代切换和键鼠 XR 模拟未被破坏；供气与通话不串线；暂停没有残余声音／粒子；近看仍能操作。当前设备网格较多，先做材质合并、LOD 和性能检查再决定进入主展厅。

## 不要直接重跑生成器覆盖人工工作

`build_equipment.py` 是本轮几何生成依据，要求一个全新的输出目录，已有 `equipment.blend` 时拒绝覆盖。成员后续应正常编辑源文件；不要删除源文件来绕过保护。浏览器与音轨构建可重新生成各自输出，但运行前仍需确认文件由自己认领，保留人工改动。

完成后在原 Issue / 工作 PR 留：改了哪些文件、依据、验证、仍未完成什么和下一步。声誉、用户评价、历史参数及实机通过情况按实际证据写，不把 AI 自动检查当成组员批准。
