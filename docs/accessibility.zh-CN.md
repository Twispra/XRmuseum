# 无障碍与舒适性走查

本页把 [课程依据](course-requirements.md) 里"考虑说明、反馈、**舒适性**、**可访问性**和复位"
（Description p2、p6；Scenario p2–4）落成一组**可逐项核查**的条目。

定位（先读）：

- 这是**资产层走查**：所有数字都由仓库里的 `.prefab` / `.unity` / `.mat` / `.cs` 直接读出，
  用脚本复算，**不依赖运行 Unity**，也不需要头显。
- 它**不是**用户研究，**不是**实机验收，**不是**可用性结论。凡是需要戴着头显才能回答的问题，
  一律写在下面第 3 节"明确未验证"里。
- 本页**没有改动任何产品文件**；第 4 节只是给组里的建议清单，供决定后再单独开工。

- 当前修订：2026-10-11，基于 PR #41 head `5fd765d` 正常合并 `origin/main` @ `a8da4e8`，
  合并提交 `5ced5d1`；`MuseumVR/` 与该主线一致。本轮修改脚本和说明后已重跑。
- 当前报告：[20261011-accessibility-review-fix/report.json](../artifacts/accessibility/qa/runs/20261011-accessibility-review-fix/report.json)。
  [修复记录](activity/2026-10-11-accessibility-review-fix.md)记录环境、命令、额外未提交文件与检查结果。
- 2026-10-10 的[旧报告](../artifacts/accessibility/qa/runs/20261010-accessibility-audit/report.json)保持原样；
  其中悬停激活、色彩空间和透明面板的解释已由本页更正，不能继续引用为当前结论。

## 0. 怎么复算

```bash
python artifacts/accessibility/qa/runs/20261010-accessibility-audit/audit_accessibility.py --out artifacts/accessibility/qa/runs/20261011-local-rerun/report.json
```

脚本只读资产，使用 Python 3.10+ 标准库；`--out` 必填，已有文件会被拒绝覆盖。
每次复跑请选一个新的运行目录，不要使用已归档的报告路径。
默认在本 clone 的 `MuseumVR/Library/PackageCache` 核对 manifest 指定版本的包源码；
若包缓存尚未生成，报告会将 shader/XRI 源码标为未核对，资产读数仍会产出。
可用 `--package-cache <现有工程的Library/PackageCache>` 指定已有缓存；不必启动 Unity。
当前归档报告使用本机现有的 URP 14.0.12 / XRI 2.6.4 缓存，并记录相对包路径与文件 SHA-256。
扫描范围：产品资产（`MuseumVR/Assets/Museum/`）8 个 prefab、3 个场景、69 个材质，以及其中 92 个 TMP 文本组件；
`MuseumVR/Assets/Samples/` 下的 XR Interaction Toolkit 官方样例单独统计，不计入下面的结论。

## 1. 结论摘要

| # | 维度 | 观察到的事实（出处见第 2 节） | 判定 |
| --- | --- | --- | --- |
| 1 | 文字对比度（语言板） | 白字 / 板面基色均按 sRGB 的配色估算为 **16.63:1**；板面使用 URP Lit，实际像素还受光照等影响，按钮另有材质 | 配色估算；最终对比度未验证 |
| 2 | 文字对比度（热点面板） | `HotspotPanel` 使用 URP Unlit / Alpha blend，基色 alpha=1、无 BaseMap；选中后 hold 30 s 覆盖后景，接下来的 fade 1 s 才与后景混合 | 分阶段记录；最终对比度未验证 |
| 3 | 最小可读字号 | 92 个 TMP 组件**全部**开启自动缩放；其中 **21 个把 `fontSizeMin` 设为 ≤0.05（20 个是 0.02）**；最大压缩比 **72.5×**（`HelmetInspectionDemo` 的 `Title`：0.02→1.45） | **风险**：资产层等于没有可读性保护 |
| 4 | 状态信息冗余 | 热点标记保留 `#1FCCE6`(α0.75) / `#FFAD26`(α1.0) / `#40F280`(α0.85)，并新增悬停外环和已访问勾形标记 | 代码与自动测试通过；真实头显和色觉用户辨识仍未验证 |
| 5 | 时间限制 | 说明面板 `hold 30 s + fade 1 s` 后淡出；重新选中同一热点会**重启计时**（`RestartExplanationLifetime`） | 有召回路径；窗口长度需真实使用者判断 |
| 6 | 操作负担 | 射线与直触都能选中；`allowHoveredActivate = true` 允许对已悬停但未 Select 的目标发送 Activate 输入，仍需扳机或握持动作；hover 同时换色并显示外环 | 输入路径存在；操作负担未验证 |
| 7 | 运动舒适 | 年代切换对绑定渲染器的材质基色施加最低 **0.35** 乘数；脚本内未找到所列相机运动 API | 实现证据；舒适性未验证 |
| 8 | 复位 | `MuseumStart` 里 `hasConfiguredHome: 1`、`homePosition` (y 1.172, z 3.5)、并存在独立复位脚本 | 通过 |
| 9 | 语言可及性 | 运行时生成的英文串（热点提示语、完成语、画廊标题…）**都在** `MuseumTranslations.json` 里 | 通过 |
| 10 | 文档与流程 | 原基准关键词扫描命中 15 篇文档，以边界声明或课程引用为主；当前统计包含新增走查及活动记录，会随文档变化。`.github/workflows` 不存在 | 当前核对仍需手动运行 |

## 2. 逐项细节（括号里是 `report.json` 的观测名）

### 2.1 字号与自动缩放（"字号分布（按文件）"、"自动缩放下限低于 0.05 的文本组件"）

按文件看，字号基线**各文件互不相同**：

| 文件 | TMP 组件数 | 字号范围 |
| --- | --- | --- |
| `MuseumStart.unity` | 47 | 0.2 – 36 |
| `MaritimeGallery.prefab` | 28 | 0.76 – 5.0 |
| `HelmetInspectionDemo.unity` | 8 | 0.3 – 1.45 |
| `LanguageShowcase.prefab` | 6 | 0.85 – 2.0 |
| `HelmetHotspotTour.prefab` | 3 | 0.75 – 1.2 |

（`*.unity` 只统计场景自身内联的 TMP 组件；prefab 实例展开的组件不写进场景文件，
它们的实际字号以源 prefab 加场景覆盖为准——主场景共有 10 处字号覆盖，值为 2 / 2.05 / 2.6 / 2.75。）

关键点是**自动缩放的下限**。92 个组件全部开启自动缩放，而：

- 21 个组件的 `fontSizeMin ≤ 0.05`，其中 **20 个就是 0.02**；
- 压缩比最大的几个：`HelmetInspectionDemo/Title` 0.02→1.45（**72.5×**）、`.../Task` 0.02→0.9（45×）、
  `MuseumStart` 的 `SHALLOWER label` / `TILT DOWN label` / `TURN RIGHT label` 0.02→0.85（42.5×）。

也就是说，一旦文案长度超出文本框，字号可以被压到**初始值的约 1/40**（例如 0.85 → 0.02）。
仓库里**没有任何一处**记录"最小可读字号"基线，所以资产层面无从判断压到这一步之后还能不能读。
（是否会真的被压到这一步取决于最长文案，需要在编辑器里逐条看——见第 3 节。）

### 2.2 面板物理尺寸（"面板的物理尺寸（沿 Transform 父链累乘 localScale）"）

沿 Transform 父链把 `localScale` 累乘，可以直接得到物理尺寸（Unity 内置 Quad 为 1×1，故累乘值即米）：

- 热点说明面板**背板** 0.42 m × 0.1725 m（`HelmetHotspotTour → Information Panel ×0.75 → Panel Backing 0.56×0.23`）
- 面板内**文字框** 0.3825 m × 0.09 m，且在面板前面 0.011 m
- 面板组相对**导览根**偏移 (0, 0.01, −0.8) m，即沿轴向 0.8 m
- 语言选择板板面 2.1 m × 1.55 m；语言按钮（English）0.59 m × 0.2 m

导览根在场景中嵌套于头盔展品实例之下，所以**面板到眼睛的实际距离/视角**要打开场景或实机才能定，
本页不给结论（见第 3 节）。上面这些值本身是资产层的确定值，可复算。

### 2.3 对比度（"热点面板文字 / 面板背景 的对比度"、"语言选择板文字 / 板面背景 的对比度"）

本工程为 Linear 色彩空间。材质的 **Color 属性以 sRGB 提供，在传给 shader 时才转换为线性值**；
不能把 `.mat` 里序列化的 `_BaseColor` 直接当作线性光值。
脚本将材质基色与 TMP 序列化文字色均按 sRGB 的条件进行配色估算，字段名为 `conditionalSrgbPaletteRatio`。
这还没有核对 TMP 的完整 shader 路径，也没有采集渲染像素，因此不是实际显示的对比度。

| 配色 | 文字 | 材质基色 | sRGB 条件配色估算 |
| --- | --- | --- | --- |
| 语言选择板 | `#FFFFFF` | `#1A1F26` | 16.63:1 |
| 热点面板正文/进度 | `#EBF5F2` | `#040B0E` | 17.75:1 |
| 热点面板完成提示 | `#4CFF8C` | `#040B0E` | 15.07:1 |

语言板使用 **URP Lit**：不透明只能说明不与后景作 Alpha 混合，不能保证受光照影响后的板面颜色。
语言按钮使用另一个 `Language-Button` 材质，不能把上表的板面估算套用于整个入口。

热点背板的 shader GUID 经包 `.meta` 核对为 **URP Unlit**，`_BaseMap` 引用为 0，shader 默认白纹理。
材质 `_BaseColor.a=1`，`_SrcBlend=5`（SrcAlpha）、`_DstBlend=10`（OneMinusSrcAlpha），
fragment 的 alpha 为 `texColor.a * _BaseColor.a`。因此在重新选中后 **30 秒 hold** 期间，
背板 alpha 为 1，在背板覆盖的像素上不会透出后景；`_Surface=1` 只是透明渲染模式。
随后 **1 秒 fade** 才由 `SetExplanationOpacity` 同时降低背板和文字 alpha，后景开始参与混合。
两个阶段都还受到文字 shader、雾、后处理及遮挡等因素影响，本轮均未验证最终像素对比度。

### 2.4 状态的颜色与形状冗余（"热点标记的状态配色"）

`HelmetHotspot.cs` 里三个状态常量：

| 状态 | 颜色 | 不透明度 |
| --- | --- | --- |
| 可选 | `#1FCCE6` | 0.75 |
| 悬停 | `#FFAD26` | 1.0 |
| 已访问 | `#40F280` | 0.85 |

三种颜色仍保留，但状态不再只靠色相：悬停时出现围绕圆点的外环，访问后出现勾形标记；热点整体隐藏时两者同步隐藏。
该行为由 `HelmetHotspotTourTests.HotspotStatesUseNonColorVisualCues` 和保存主场景回归覆盖，证据见
[2026-10-13 活动记录](activity/2026-10-13-hotspot-state-accessibility.md)。本轮没有做色彩仿真、真实头显或色觉用户测试，
所以只确认代码提供了非颜色冗余，不宣称实际可辨性已经通过。

### 2.5 时间限制与操作负担（"说明面板自动淡出的默认时长"、"交互输入开关"）

- 说明面板在 `hold 30 s` 后开始淡出、`1 s` 淡完。对阅读慢的使用者这是时间限制，
  但代码里重新选中同一热点会重启计时，因此**存在召回路径**（这一点是正面的）。
- `enableTriggerQueries = true` 将触发器碰撞体纳入交互检测。
  `allowHoveredActivate = true` 允许在没有 Select 目标、但有 Hover 目标时向该目标发送 Activate 事件。
  XRI 2.6.4 的 `shouldActivate` 仍要求 `activateInteractionState.activatedThisFrame`；
  本工程 Activate 对应扳机，Select 对应握持，两条路径都可调用热点的 `Activate()`。
  `OnHoverEntered` 更新颜色并显示外环，单纯悬停仍不会访问热点。
  这些是输入路径证据，不能据此宣称免按键、操作负担降低或误触增加；本轮未发现 dwell / 凝视激活路径。

### 2.6 运动舒适（"年代切换的运动方式"）

`HelmetEraSwitcher.cs` 在切换时淡变绑定渲染器的材质基色，常量 `MinFadeBrightness = 0.35`
是对象颜色的乘数下限，不能解释为全画面亮度。脚本内未找到 `Teleport` / `Camera.main` /
`MoveTowards` / `SetPositionAndRotation` 等所列相机运动 API。本轮不据此判定晕动症或舒适性通过。

### 2.7 复位（"复位（回到起始位姿）的资产证据"）

课程把"复位"单列为要求。资产层有对应实现：`MuseumStart.unity` 中 `hasConfiguredHome: 1`、
`homePosition.y = 1.1719999`、`homePosition.z = 3.5`（旋转为 0），并且存在
`MuseumVR/Assets/Museum/Scripts/HelmetExhibitReset.cs`。

### 2.8 语言可及性（"运行时字符串是否都在三语表里"）

热点提示语与完成语在代码里是英文常量（`HelmetHotspotTour.cs` 的 `Prompt`、
`WriteText(..., "Exploration complete")`），但经 `HelmetHotspotLocalization` 桥接后
**以英文串为 key 查表**。脚本逐条确认这些 key（连同画廊标题等）都已存在于
`MuseumVR/Assets/Museum/Features/Localization/Tables/MuseumTranslations.json`，
因此**不构成"英文写死"缺陷**——这一点特意核过，避免误报。

### 2.9 入口的位置（"主场景中的 prefab 实例位置"）

脚本从主场景里读出各实例根对象的坐标覆盖：**XR 起点** (0, 0, 2.2)、
**语言选择板**（场景名 `Language Selection`）(2.6, 0, 6.2)、**头盔展品** (0, 1.172, 3.5)。
板面本身再在局部抬 1.45 m、前移 0.6 m（见 2.2）。

也就是说语言切换入口距起始位置约 5–6 m，访客可能需要先移动才能改语言
（Editor 里有 F6/F7/F8 快捷键，但那不是带头显的路径）。
**是否构成障碍要看设计意图**（"改语言"是否必须发生在入场前），本页只记录数值，不定性。

注意：热点导览实例在场景中**被嵌套**（`parentIsSceneRoot=false`），其绝对位置要叠加父级，
所以上面只给它的相对偏移，不给绝对视距。

### 2.10 文档现状（"docs/ 中提到可访问性的文件"）

原基准按关键词命中统计出 `docs/` 下 15 个文件提到可访问性，命中内容以限制和课程引用为主：

- 活动记录里的收尾句，如"未做无障碍或参与者评价""不声明舒适性或无障碍已获验证"；
- 文档里的限制段，如"距离和字号是本项目设计参数，不是通用 VR 无障碍标准"；
- `course-requirements.md` 与 `project-state.md` 对课程要求本身的引用。

关键词命中不是整个仓库是否已有设计工作的证明；例如现有展厅文档已经记录坐姿、单手、成年新手的设计方向。
本页提供集中走查入口；当前脚本也会扫描本页及新增活动记录，不能要求结果始终为 15。

（关键词统计由脚本自动产出；"命中处是基线还是声明"这一步需要人读上下文，脚本不代替该判断。）

## 3. 明确未验证

以下都**没有**在本轮确认，不应被引用为已验证：

1. 真实视高/眼高与坐姿可达范围——需要头显；坐姿人体测量分布也不在仓库里。
2. 文字的实际角尺寸与毫米高度——需要 Unity 打开工程或实机测量（本机无头显，本轮也未运行 Unity）。
3. 所有文字的**最终像素对比度**——包含语言板 Lit 光照、TMP shader、热点背板 hold/fade 两阶段，需渲染或实机取样。
4. 色觉障碍下的真实可辨性——需色彩仿真或真人测试，本轮脚本不做仿真。
5. 参与者可用性研究——属 Phase 3；仓库已明示"自动 XR 测试不等于参与者研究"。
6. 主场景中**内联** TMP 组件的字号是否已经真的压到 `fontSizeMin`——需要在编辑器里逐条核对最长文案。

## 4. 建议的最小改动（仅建议，本页不改产品文件）

1. 给那 21 个 `fontSizeMin ≤ 0.05` 的组件设一个有意义的下限。可用的锚点已经有：
   热点面板高 0.1725 m、文字框高 0.09 m——据此反推出一个"最小 em 高"再回填，
   比继续留 0.02（等于没有下限）可核查得多。
2. 热点三态的**非颜色**冗余已在 Issue #45 实现；下一步是用真实头显和目标用户检查外环／勾形在不同角度、距离与背景下是否足够清晰。
3. 先分别检查热点背板 hold 与 fade 的可读性；hold 已是 alpha=1，不再建议因 `_Surface=1` 而重复加不透明底。
   若淡出影响阅读，可另行评估延长、暂停或关闭计时；若渲染检查发现实际对比度不足，再评估文字描边或配色。
4. 若要长期守住以上几条，可把本页的脚本接进 CI——当前仓库没有 CI，所有核对都是手动的。

## 5. 依据

- 复现脚本与当前数据：[`audit_accessibility.py`](../artifacts/accessibility/qa/runs/20261010-accessibility-audit/audit_accessibility.py)、
  [`report.json`](../artifacts/accessibility/qa/runs/20261011-accessibility-review-fix/report.json)
- Unity 2022.3 [材质 Color / shader 色彩空间](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-PropertiesInPrograms.html)、
  [MaterialPropertyBlock.SetColor](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MaterialPropertyBlock.SetColor.html)
- XRI 2.6 [allowHoveredActivate](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.6/api/UnityEngine.XR.Interaction.Toolkit.XRBaseControllerInteractor.html#UnityEngine_XR_Interaction_Toolkit_XRBaseControllerInteractor_allowHoveredActivate)
- 课程依据：[course-requirements.md](course-requirements.md)
- 相关实现：`MuseumVR/Assets/Museum/Features/HelmetHotspots/Runtime/HelmetHotspot.cs`、
  `.../HelmetHotspotTour.cs`、`MuseumVR/Assets/Museum/Features/HelmetEras/Runtime/HelmetEraSwitcher.cs`、
  `MuseumVR/Assets/Museum/Scripts/HelmetExhibitReset.cs`
- 工作记录：[2026-10-11 审查修复](activity/2026-10-11-accessibility-review-fix.md)、
  [2026-10-10 历史走查](activity/2026-10-10-accessibility-audit.md)
