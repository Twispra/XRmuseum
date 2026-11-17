# 2026-11-17：会话遥测记录器（Phase 3 定量证据）

用户要求"多做代码类、有分量的贡献"，并授权我在本仓库新增运行时记录组件。
本轮**新增运行时与编辑器代码，未改动任何场景、Prefab、材质、字体、Packages 或 ProjectSettings**。

## 为什么做这个

Phase 3 评分标准把作业定义为「evaluation, analysis, prioritisation, improved build and verification
as one evidence-to-action cycle」，组内 2 分给"分析与证据质量"。
而现阶段最大的障碍不是缺功能，是**缺过程数据**：作者自测永远"没问题"，因为作者知道该点哪里。

记录器把"感觉"换成"过程"：几次才对、在哪一步停住、来回试了几次。

## 本篇交付

| 文件 | 说明 |
| --- | --- |
| `MuseumVR/Assets/Museum/Features/Telemetry/Runtime/SessionTelemetry.cs` | 运行时记录器，425 行，`RuntimeInitializeOnLoadMethod(AfterSceneLoad)` 自动挂载 |
| `MuseumVR/Assets/Museum/Features/Telemetry/Editor/SessionTelemetryMenu.cs` | 菜单 `Museum/Telemetry/*`：打开目录 / 查看当前文件 / 清空 |
| `docs/session-telemetry.zh-CN.md` | 字段、事件、指标换算、模拟器按键与边界 |
| 5 个 `.meta` | 按仓库惯例补齐，避免 Unity 自动生成导致工作区变脏 |

设计上只订阅 `XRBaseInteractable` 的公开事件（`selectEntered` / `activated` / `hoverEntered`）
并读取现成的公开属性（`HintRequests`、`CurrentIndex`、`ResetCount`、`MuseumText.Channel.Current`），
**不侵入任何交互逻辑**，因此不可能改变访客体验。

## 验证过程（编辑器实测，非评价数据）

1. 首次提交后编译：`Assembly-CSharp` 与 `Assembly-CSharp-Editor` 均重新构建，**0 error**，
   仅保留原有的 2 条 `CS0618`（与本篇无关）。
2. 连续 5 次 Play 会话，逐步补齐记录面：

| 时间 | 时长 | hover | activate | select | 本次暴露的问题 |
| --- | --- | --- | --- | --- | --- |
| 22:09 | 59.4s | 0 | 0 | 0 | 挂载与状态源正常，但没抓到任何交互 |
| 22:26 | 26.4s | 4 | 0 | 0 | 仍是 0 交互 |
| 22:45 | 24.3s | 7 | 0 | 0 | 7 次 hover、0 次按下 |
| 22:58 | 13.5s | 4 | **2** | 0 | 补齐 `activated` 后成功记录 |

3. 期间修正了三处实现问题：
   - 只订阅 `selectEntered`，漏掉 `activated` —— 本工程热点同时监听两者，第一版会漏掉"按扳机"的访客；
   - `session_end` 用退出时的实时快照，此时场景对象已销毁，会把终态记成 `page=-`；改用最后一次采样；
   - 首次交互的 `gap` 从 `-1000` 起算，写出了 `gap=1008.86` 这种无意义值；改为标记 `first`。

## 由本次验证产生的两条观察（不是评价结论）

1. **模拟器的扳机是"有条件"的。** XRI 2.6.4 的 `ProcessButtonControlInput` 只把扳机/抓握写进
   "正在被接管的那只手"，所以编辑器里**单独按左键不产生任何交互信号**，必须 `Shift`+左键（接管左手）
   或 `T`/`Y` 锁定某只手；抓握则对应 `G` 键。真头显没有这一层。
   → 已写进 `docs/session-telemetry.zh-CN.md` 第 5 节。**若不写清，用模拟器做的评价会把
   "能指不能点"记成产品缺陷，属于假阴性。**
2. **按压语义在场景内不一致，但产品已有提示。** `HelmetHotspot` 同时监听 `selectEntered` 与 `activated`，
   而 17 个 `HelmetExplorationButton`（Inspect / Explore / More tools / Help 等）**只监听 `selectEntered`**；
   XRI 默认绑定是 `Select = 抓握`、`Activate = 扳机`。即真机上"对热点抠扳机有效、对面板按钮抠扳机无反应"。
   但 `HelmetExplorationPanel` 在编辑器里就显示 `Point + G to select`（构建里是 `Point + grip to select`），
   Help 面板另有完整按键说明 —— **所以这是有说明的设计，不是隐性陷阱**。
   → 本轮**不改**：该文件属共享交互逻辑，且现有提示已覆盖；是否需要统一留给组内决定。

## 未验证

- 真实头显下的按键路径未确认（本机无头显，OpenXR 会话报 `XR_RUNTIME_UNAVAILABLE`）。
- 记录器在长时间会话（>10 分钟）下的文件体积与刷新开销未测。
- 上述"按压语义不一致"是否影响真实访客，需要真人会话才能判断；本轮只有组员自测的间接证据。
