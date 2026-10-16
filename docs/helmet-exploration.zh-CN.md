# 头盔自由探索：供气、通话与按需提示

探索与展厅均已进入本页核对的 `main` @ `411524e`（2026-10-16），Mark V、SuperLite 与精细 AIR / VOICE 设备沿用同一探索流程。本轮 [Issue #57](https://github.com/Twispra/XRmuseum/issues/57) 补齐 NEW VISIT 对观察热点的清理；最终被测 commit 与检查入口：**〔整合后填写，未取得结果前不得写通过〕**。最初 [Issue #6](https://github.com/Twispra/XRmuseum/issues/6) / [PR #7](https://github.com/Twispra/XRmuseum/pull/7) 的 `c2bf63c` 发布与复测保留为[历史记录](activity/2026-10-03-exploration-release.md)，不再作为待合并依赖。

这次把“戴上这顶头盔，就能独自在水下工作了吗？”作为观察问题。游客可以先看供气，也可以先看通话；不设倒计时、必经路线或强制答题。目标是让已有的转动、前窗和剖面帮助理解展品，而不是另做一套参观场景。

## 怎样试用

打开保存好的 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`。原观察面板新增 **INSPECT / EXPLORE** 两个页签，共用跟随位置和交互期间暂停跟随的逻辑。进入 Play 后默认保留 INSPECT；选择 EXPLORE 才显示开场问题和探索内容。

电脑模拟器仍是：点击 Game、切英文输入、T/Y 选左右手柄，射线指向后按 **G 并松开**。这是 Grip → Select；鼠标左键模拟扳机，不是这组实体按钮的选择键。实际手柄使用握持选择，实机效果仍需设备确认。

| 入口 | 当前行为 |
| --- | --- |
| INSPECT | 回到原观察工具；可以继续开窗、剖面、旋转、悬停与模型复位 |
| EXPLORE | 打开探索页，保留上次查看的内容 |
| AIR | 查看手动供气泵、软管和头盔接头的三维演示；也可选择后部接头旁的提示标记 |
| VOICE | 查看水面电话箱、耳机及独立潜水员端换能器的三维演示；不把示意零件塞进原灰模内部 |
| HINT | 主动索取一条未查看线索的提示；不会因为游客停留太久而自动催促 |
| REVIEW | 随时回顾已查看内容；未查看的线索会如实标记，不要求先完成两项 |
| REPLAY / STOP | 重播最近选择的线索，或立即停止并隐藏配套模型与演示图 |
| BACK | 回到探索页的开场问题；返回原工具请用 INSPECT |
| NEW VISIT | 停止演示，清 AIR / VOICE、提示与三个观察热点的查看记录及旧说明，回到探索开场；保留语言、年代、声音 / 减少动画偏好、展品位姿与视角 |
| RESET VIEW / F11 | 将访客送回保存的起始位置与朝向，面板回到身前；保留展品状态与本轮发现 |

首次主动选择 AIR 或 VOICE 时播放约 **16 秒**的分段演示，不需要一直握住按键。结束后保留配套模型、连接路线和文字，便于慢慢观察。开启 ReduceMotion 时直接显示静态模型与双向/单向关系，不播放手轮、活塞、振膜或沿线信号动画。再次选择同一线索会停止并隐藏原演示，只打开说明；按 REPLAY 可重新显示，是否播放动画仍由 ReduceMotion 决定。演示期间可随时 STOP 或切换线索。

| 演示 | 看见的模型与动作 |
| --- | --- |
| AIR | 先看手动泵和软管；随后两个手轮转动、活塞与连杆上下运动，方向标记沿软管到达现有 `GB_AirConnector`；最后用静态关系总结水面供气 |
| VOICE | 先看木色电话箱、弧形头带双耳机与独立换能器；先由水面端发话，振膜和声形标记响应，信号图形沿电缆前进；提示换人后，潜水员端响应并向水面返回，最后保留双向关系 |

供气演示分为 0–2 秒设备预览、2–13 秒泵与输送演示、13–16 秒总结；通话分为 0–2 秒预览、2–7 秒发出、7–9 秒换人提示、9–14 秒回复、14–16 秒总结。这是教学节奏，不代表真实设备转速、信号延迟或通话协议。沿电缆的波列是通信方向的图形表达，不表示声音以空气振动的方式在电线中传播。

配套模型显示在头盔侧方，当前主场景已用[精细设备资产](refined-model-integration.zh-CN.md)替代最初的程序几何，仍由原演示时钟驱动；它们用于观察，不增加必须抓取、持续摇柄或双手操作的任务。现有头盔仍可按原方式观察与复位。标记和文字用于教学，不是历史装备上的实物标签。

模型的 **RESET / Home** 会恢复原观察状态并停止演示，保留本轮探索与热点发现；**RESET VIEW** 只恢复观看位置，也保留发现。**NEW VISIT** 才清空 AIR / VOICE、提示数、三个观察热点的勾形 / 进度 / 完成状态、当前解释及说明计时，不把上位观众的 3/3 留给下一位。它不改变当前语言、年代、声音 / 减少动画偏好，也不移动头盔或访客。记录仅代表“打开过这条内容”，不代表已理解、看完整段动画或通过学习评估；当前也不保存跨次运行的学习档案。

## 资料与表达边界

1. [U.S. Naval Undersea Museum：MK V Diving Rig](https://navalunderseamuseum.org/mk-v/)：标准空气型后部有电话和供气连接，正文说明它们与电话线路、供气软管的关系。本功能据此表达“头盔与水面系统相连”，不把空气型和氦氧混合气型结构混用。
2. [Royal Museums Greenwich：Air pump，EQA0617](https://www.rmg.co.uk/collections/objects/rmgc-object-256836)：Siebe Gorman 潜水泵，馆藏说明明确有两只手摇轮，潜水员在水下时需要持续转动。用于参考水面泵的外部形态与人工驱动方式。
3. [Science Museum Group：Two-cylinder double-acting Diver's air pump，1963-71](https://collection.sciencemuseumgroup.org.uk/objects/co41152/two-cylinder-double-acting-divers-air-pump)：剖切展示的手动双缸双作用潜水泵，用于参考双缸机构类型，不据此推算精确连杆、转速或气流参数。
4. [Royal Museums Greenwich：Telephone box，ZBA6671](https://www.rmg.co.uk/collections/objects/rmgc-object-561120)：Siebe Gorman 潜水电话箱，馆藏说明列出木箱、头带式双接收器与发话器电池，支持增加可辨认的水面通话设备示意。

以上来源于 2026-10-03 核验，原 DESCO 头盔来源仍见[资源记录](external-resources.md)。**泵和电话箱是历史设备类型的参考，不是已经证明与这顶 Mark V 在某年配套使用的精确复原。** 模型造型、尺寸、动作和布局经过教学简化，不能据此认定具体型号、年代或操作规程；现代在售产品参数也不直接用作历史实物参数。

本轮没有下载、导入或再分发馆藏照片；电话箱页面的图片明确标有不得复制。当前头盔灰模不是扫描或制造图纸复原，`GB_AirConnector` 只是简化接头，不包含完整阀门或内部气路。新增配套模型是独立教学示意，不把未经考证的设备塞入原头盔内部。通话仍不含录制语音或实时通话。动画时长、信号速度、线宽、热点大小都是原型设计参数，不是历史数据，也不是流体、声学或潜水安全模拟。动画暂停只是展览演示暂停，不代表真实水下供气可以停止。

## 接手代码

新功能位于 `MuseumVR/Assets/Museum/Features/HelmetExploration/`：

| 文件 | 负责什么 |
| --- | --- |
| `Runtime/HelmetExplorationController.cs` | 两条线索、按需提示、回顾、重播与参观记录 |
| `Runtime/HelmetExplorationPanel.cs` | 同一面板的两页切换、文本刷新，以及探索模块停用时回到原观察工具 |
| `Runtime/HelmetExplorationButton.cs` | 沿用 Select 的按钮分发和悬停反馈 |
| `Runtime/HelmetExplorationHotspot.cs` | 接头旁的可选线索入口，协调它与原模型抓取、复位的选择关系 |
| `Runtime/HelmetConnectionDemonstration.cs` | 16 秒分段演示、软管/电缆、沿线信号与阶段文字；结束后保留静态模型，ReduceMotion 直接显示静态关系 |
| `Runtime/HelmetDemonstrationModels.cs` | 原程序几何与回退路径；主场景的精细设备改由 `RefinedConnectionEquipment` 驱动，继续复用演示时钟 |
| `Editor/HelmetExplorationSetup.cs` | 首次整合到保存场景的工具；日常打开与试用不需要重跑 |

原 `HelmetInspectionPalette.cs` 仅增加附加页的显示范围和布局支持；页签继续共用原面板的放置、避障与交互冻结机制。编辑器版本、Packages、共享输入和 XR Rig 不属于本轮改动目标。修改主场景仍须按仓库协作规则协调单一写入者；不要运行旧头盔 Builder 来“刷新”本功能。

## 验证与下一步

本轮 NEW VISIT 的实际按钮入口、观察 / 探索进度清理、两种 RESET 保留发现、三语与偏好保留需要绑定 #57 的组合源码检查，结果待本页首段补齐。近期完整主场景与 Windows 构建分别有版本证据，见[2026-10-16 普通壳体修复](activity/2026-10-16-issue50-shell-grab.md)；观察入口正常退出修复见[后续记录](activity/2026-10-16-pr56-editor-exit-and-merge.md)。它们均不能代替本轮新访客专项或修订版设备验收。

以下为最初设备动画发布的历史证据；后续精细设备、三语、物理手等改动分别在对应活动中记录，不能把旧 351 项结果写成当前版本重新测试。

初版设备模型动画完成 351/351 项检查，记录错误 0，实际进程退出码 0；包含机构运动、双向波列、静止/停止、连续重播资源复用和原操作兼容性检查。[模型动画记录](activity/2026-10-03-exploration-model-animation.md)列出报告、渲染、修正过程和边界，该历史运行的 635 个受保护文件哈希保持一致。

发布前已重新运行原观察功能 207/207 项、原拿放 90/90 项检查，均错误 0、实际进程退出码 0；351 项报告的源文件哈希也全部仍匹配提交内容。详细版本映射和报告见[发布交接](activity/2026-10-03-exploration-release.md)。更早的 5 秒箭头版、282 项测试和旧批处理超时保留在[初版历史记录](activity/2026-10-03-helmet-exploration.md)，不代表当前版本。

接手试用时优先检查：AIR / VOICE 两种顺序；未查看、只查看一项和两项均查看时的 REVIEW；重复选择不自动重播；STOP、模型复位和 NEW VISIT 的区别；接头热点与普通抓取是否互相抢选择；近距离、背面、坐姿下能否切回 INSPECT。当前仍待真实头显的可读性、触达、舒适性和性能检查，以及目标访客的理解效果验证。
