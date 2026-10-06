# 前窗、剖面与设备演示的细节完善

本轮接在原 MuseumStart 主展厅上，分支 `codex/helmet-experience-polish-2026-10-06`，基准 `d432a37`，依赖尚待审查的三语 PR #18。不是独立的另一套参观场景。打开保存的 MuseumStart 即可使用，不重跑旧头盔或展厅 Builder。

后续已补齐背部电话鹅颈：从背面看左侧电话、右侧供气。电话电缆和收发反馈跟随实体电话接头；供气仍使用独立供气出口。实现、来源和核对记录见 [背部接头交接](activity/2026-10-06-rear-telephone-connection.md)。这次是保存 Prefab 的追加结构，原 FBX 保留。

## 怎么看效果

1. Game 获得焦点后，T／Y 选择手柄，射线对准 Window 后短按 G 并松开。前窗从静止起步、缓慢停到末端；玻璃保留轻微色调、边缘反射，能看到已有的空心壳体。近处仍可以握住前窗拖动。
2. 打开 Section，再用 Align view、Top cut 或原剖切球柄调整。被切到的金属网格会出现截面，空腔保持空着；原头盔几何和操作方式不变。关闭剖面时这些切口面一起隐藏。
3. Explore → Air：泵先动作，方向标记从泵端逐个发出，再到头盔供气接头；接头收到后得到视觉强调。Voice：电话箱发出 → 潜水员端收到 → 潜水员端回复 → 电话箱收到；两端强调随着消息方向改变。原动画时长、停止／重播／回顾流程保留。
4. 头盔拿离展台后，按原 Reset 按钮或 Home。短暂返回动画让访客看见它回到原位置和朝向，返回期间不能重新抓取；必须松开原握持后才能再抓。没有移动访客摄像头。自动跌落／越界恢复仍直接复位。
5. Help 页的 Sound 按钮可关闭或开启声音，与 Motion 开关并排。文字支持简体、繁体和英文；减少动画时前窗和主动复位直接完成，设备演示继续使用既有静态形式。

声音是程序合成的轻微铰链、末端接触和返回确认反馈，不是历史设备录音。静音不影响任何动作或视觉确认。不要求连续摇柄、双手操作或实际步行。

## 实现与接手

- `Features/HelmetExperience/Runtime/HelmetMechanicalFeedback.cs`：合成音频、空间音源、静音与生命周期清理。声级和距离是原型参数，没有宣称头显实测结果。
- `HelmetSectionMeshData.asset`：Editor 从原 78 个不同金属网格捕获的位置和索引，供运行时切口生成使用。没有改原 FBX、模型 `.meta` 或 Read/Write 导入设置。以后模型更新，应把这个数据的同步作为明确资产任务，不能让旧副本悄悄对应新网格。
- `SectionCapGeometry.cs`、`HelmetSectionSurface.cs`：平面相交及嵌套轮廓填充，空腔保留；开放／非流形边界不猜测填补。缓存截面网格，整盔位姿变化直接随根对象变换，切面或活动前窗变化才重建。
- `InspectionGlass.shader`、`SectionFace.shader`：玻璃与真实网格截面的显示。原 InspectionShell 只增加设备强调和已有内表面的辨认，默认值不改变其他使用它的物件。
- 原控制器保留即时 `ResetInspection()` 与 `ResetToDisplay()`，主动按钮改走 `ReturnInspectionToDisplay()`／`RequestResetToDisplay()`。约 0.30 秒、小幅抬升的返回是展示动画；近乎已归位、减少动画、越界或距展台超过 2 米时直接恢复。不是避障规划或真实物理搬运。
- 原探索代码保留控制逻辑，只增加发出／到达顺序与局部视觉强调。暂停展陈动画不表示真实潜水供气可停止，通信图形也不是电线里的空气声波。

接手前先确认 #18 是否合并；有共享场景或 Prefab 改动时逐项整合，保留 GUID，不整份覆盖。`HelmetExperienceSetup` 是本轮明确迁移工具，操作前备份指定资源，不属于日常开场步骤。

## 核对入口

新增功能：`Museum.Experience.EditorTools.HelmetExperienceValidation.RunBatch`。它在保存的主场景通过组件 API 检查切口、动作、声音设置和方向强调，截图用临时诊断相机，不冒充真实 XR 输入。原生产输入仍由 HelmetExplorationValidation 与 TrilingualInspectionRegression 检查；语言由 LocalizationIntegrationValidation 检查。各入口从退出 Play 的状态单独执行，不共用一个正在打开的 Unity 工程。

本轮运行结果、实际截图、修复过程和未验证内容见 [活动交接](activity/2026-10-06-helmet-experience-polish.md)。还需真实头显、声音人工试听、目标用户和性能／构建检查，不能把桌面自动化检查写成这些结果。
