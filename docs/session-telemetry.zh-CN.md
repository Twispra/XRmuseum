# 会话遥测（Session Telemetry）

一个只旁听、不改交互的记录器：把访客在场景里"指了什么、按了什么、页面走到哪一步"
逐行写成 CSV，供 Phase 3 的定量分析与前后对比使用。

它回答的是**自测回答不了的问题**：作者自己走一遍永远"没问题"，因为作者知道该点哪；
遥测记的是**过程**（几次才成功、在哪一步停住、来回试了几次），而不是结论。

## 1. 它不需要往场景里放任何东西

`SessionTelemetry` 通过 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 自动挂载，
进入 Play 即开始记录，退出 Play 即结束。**不修改任何场景、Prefab、材质或字体。**

- 关闭：启动参数加 `-museumTelemetryOff`
- 打开日志目录：菜单 `Museum/Telemetry/Open session log folder`
- 查看当前文件路径：菜单 `Museum/Telemetry/Show current session path`

日志位置：

```
%USERPROFILE%\AppData\LocalLow\Museum Group\MuseumVR\MuseumSessions\
```

一次 Play 一个文件，命名 `yyyyMMdd-HHmmss-MuseumVR.csv`（UTF-8 带 BOM，Excel 可直接打开）。
同一秒开始的新会话会用 `-2` 等数字后缀，保留原记录。
**该目录在用户目录下，不进 Git。**

## 2. 字段

| 列 | 含义 |
| --- | --- |
| `t_seconds` | 会话内秒数（不受 timeScale 影响） |
| `event` | 事件类型，见第 3 节 |
| `target` | 被操作的物体，取三层路径（如 `HelmetExhibit/HelmetHotspotTour/Front window Hotspot`） |
| `detail` | 事件附加信息（`gap=` 距上一次交互的秒数、`n=` 计数、`first` 首次） |
| `page` / `clue` | 探索面板当前页与已选线索 |
| `hints` | 求助次数（`HelmetExplorationController.HintRequests`） |
| `air_seen` / `voice_seen` | 两条线索是否看过 |
| `era_index` / `era_name` | 当前年代序号与名称 |
| `language` | 当前界面语言 |
| `cutaway` / `floating` / `window_open` | 剖切、悬浮、开窗状态 |
| `resets` | 展品复位次数（`HelmetInspectionController.ResetCount`） |

## 3. 事件类型

| `event` | 何时写入 | 用途 |
| --- | --- | --- |
| `session_start` | 进入 Play | 记录场景名、启动时已订阅的目标数和三个状态源的解析结果 |
| `hover` | XRI 进入可交互目标的悬停范围 | 同一已注册目标的悬停进入间隔至少 1 秒才会再次记录，其他目标独立计时 |
| `activate` | XRI 向目标派发 `activated` | 记录该目标收到的激活事件 |
| `select` | XRI 向目标派发 `selectEntered` | 记录对该目标的选择开始事件 |
| `state` | 订阅回调或定时采样检测到状态变化 | 记录观察到的流程状态 |
| `scene_loaded` | 场景切换 | 边界 |
| `session_end` | 退出 Play | 时长 + 各类计数，附最后一次快照 |

`activate` 与 `select` **分开记**，因为本工程两条路径都存在：`HelmetHotspot` 同时监听
`selectEntered` 与 `activated`，而 `HelmetExplorationButton` 只监听 `selectEntered`。
XRI 事件的目标依赖当前交互条件，物理按钮还会在释放时检查是否确认。分析时应结合后续状态
或观察记录确认结果。启动时的目标计数只反映当时已注册的对象，随后注册的物理控件由 XRI 事件补订阅。

需要说明的是：产品本身**已经教了**用法——`HelmetExplorationPanel` 在编辑器里显示
`Point + G to select`、构建里显示 `Point + grip to select`，Help 面板还有一整套按键说明
（`Click Game to focus. / T / Y: choose a controller. / Point + G: select a button. / Hold G: grab…`）。
分开记能看出哪些目标收到了提示之外的激活事件。要判断按钮操作遇到了什么困难，需要结合
操作时的页面、输入条件和观察记录。

## 4. 能算出什么

| 指标 | 从日志怎么算 |
| --- | --- |
| 首次交互耗时 | 第一条 `hover`/`activate`/`select` 的 `t_seconds` |
| 每步耗时 | 相邻 `state` 的 `t_seconds` 差 |
| 求助次数 | `hints` 列的最大值 |
| 短间隔换目标 | `detail` 含 `rapid_reselect` 的行数（2 秒内换目标），具体原因由观察确认 |
| 犹豫 / 徘徊 | 同一 `target` 的 `hover` 重复次数与来回轮数 |
| 结束时的页和求助次数 | 读取 `session_end` 的 `page` 和 `hints`，其中 `page=-` 表示页面未知。是否中途放弃需核对完整轨迹和观察记录 |

**一个真实例子**（2026-11-17 22:45 那一次，测试者=组员，**不是评价参与者**）：
24 秒内 7 次 `hover` 只在两个热点之间来回（Front window ↔ Bonnet shell），
`activates=0`、`selects=0`，结束时 `page` 仍是 `Welcome`。
单看"有没有问题"，答案会是"没问题"；看日志才知道**他指了 7 次、一次都没按下去**。

## 5. 设备模拟器的按键（重要，否则会记出假数据）

在 **Unity 编辑器 + XR Device Simulator** 下：

| 操作 | 效果 |
| --- | --- |
| 鼠标移动 | 射线跟着动 → 有 `hover` |
| **单独左键** | **没有任何控制器信号**（不产生 `activate`） |
| **Shift + 左键** | 接管左手 → 扳机 → `activate` |
| **T / Y** | 锁定左手 / 右手（比一直按住 Shift 省力） |
| **G** | 抓握 → `select` |

原因：模拟器的 `ProcessButtonControlInput` **只把扳机/抓握写进"正在被接管的那只手"**
（XRI 2.6.4 源码注释：*update the state of manipulated controller device(s)*）。
真头显上不存在"接管"这回事，扳机由手柄自己上报，不受影响。

> 这一条必须写进主持人脚本。否则用模拟器做的评价，会把"能指不能点"记成产品缺陷 —— 那是假阴性。

## 6. 边界

- **它不能替代真人评价。** 记录的是行为，不是原因；为什么犹豫必须靠 think-aloud 与访谈。
- 记录器订阅公开事件和读取状态。若日志无法创建、写入或关闭，会结束本次记录、释放订阅并在 Unity 控制台写警告。写入中断的 CSV 可能缺少 `session_end`，分析前请核对是否完整。
- **未验证**：以上结论均来自编辑器内模拟器会话；真实头显下的按键路径**未经实机确认**。
- 编辑器退出 Play 时若出现 `Some objects were not cleaned up…XRInteractionManager`，
  与记录器无关：该对象由 XRI 的 `XRDeviceSimulatorLoader` 通过 `DontDestroyOnLoad` 留在跨场景层级，
  记录器全程只创建了一个名为 `SessionTelemetry` 的对象。

## 7. 实测记录

2026-11-17 当晚共 5 次编辑器会话，用于验证记录器本身（**不是评价数据**）：

| 时间 | 时长 | hover | activate | select | 说明 |
| --- | --- | --- | --- | --- | --- |
| 22:09 | 59.4s | 0 | 0 | 0 | 只验证挂载与状态源 |
| 22:26 | 26.4s | 4 | 0 | 0 | 只有 hover，暴露"左键无信号" |
| 22:28 | 21.7s | 3 | 0 | 0 | 同上 |
| 22:45 | 24.3s | 7 | 0 | 0 | 两热点间来回 7 次，一次没按 |
| 22:58 | 13.5s | 4 | **2** | 0 | Shift+左键生效，其中一次间隔 1.60s 被判 `rapid_reselect` |

`session_start` 自检结果：`interactables=49; exploration=1; eras=1; inspection=1`。

## 8. 与评价方案的关系

本记录器服务于 `docs/evaluation/` 里的评价方案（该目录见同批提交的另一条分支），
定位是**客观行为日志**，与 think-aloud 的主观陈述互补：
主观说"我觉得挺清楚"，客观能看到"他在这一步来回试了 7 次"。
