# 2026-10-03 供气与通话模型动画

本页保留开发当时的状态。当前 16 秒设备模型版本、发布前回归和提交信息以[发布交接](2026-10-03-exploration-release.md)为准；下文“仅本地/未推送”和初版 5 秒演示均为历史记录。

用户认为第一版的方向箭头和移动光点不够直观，希望在演示时出现相关设备模型，并让模型参与动画。本次继续 Issue #6、`codex/helmet-exploration-2026-10-03` 的本地开发，基于 `39bebc3` 加尚未提交的自由探索代码；环境 PR #5 仍未合并。没有把新增功能上传到环境 PR。

## 修改范围

将原简图扩展为两组可辨认的教学模型。供气以手摇泵、双缸、软管和头盔接头展示传输关系；通话以水面电话箱、接收装置与电缆展示发话及回复。模型随演示出现，停止或复位后隐藏；仍用原 AIR / VOICE 按钮启动，不要求游客持续摇柄、双手配合或走到展台后方。

- AIR：先展示装置，随后双轮转动、活塞往复，五组流动标记沿软管前往真实灰模接头，最后保留模型关系。软管随头盔移动/旋转更新端点。
- VOICE：电话箱有双耳接收器，潜水员一端用独立示意单元表现。发话端的振膜与扩散环配合电缆波列，先发送，再交替，最后回复；阶段文字说明谁在发话。
- 两组均为 16 秒，首次选择自动演示，后续用 REPLAY 主动重播。模型不参与碰撞、抓取或按钮射线，不另建演示场景。物体、网格和材质创建一次后复用，释放时清理自有资源。

动画结束后保留静止模型供观察，文字标明演示暂停。ReduceMotion 直接显示静止关系，不让机构、流动标记或波形继续运动。真实潜水供气需要持续维持，演示暂停不能理解为潜水中可停止供气。

本次只调整探索模块的演示、简短文案与验收代码，新增 `Runtime/HelmetDemonstrationModels.cs` 负责几何与机构。主场景、原头盔资产、抓取、开窗、剖切、输入、Packages 和 ProjectSettings 均未改动。子任务分别负责演示代码、验证器、资料及专项说明，主任务统一整合与执行 Unity。

## 资料依据

- [Royal Museums Greenwich，EQA0617](https://www.rmg.co.uk/collections/objects/rmgc-object-256836)：Siebe Gorman 手动潜水供气泵与两只手摇轮。
- [Science Museum Group，1963-71](https://collection.sciencemuseumgroup.org.uk/objects/co41152/two-cylinder-double-acting-divers-air-pump)：手动双缸双作用潜水泵，馆藏本身有剖切展示。
- [Royal Museums Greenwich，ZBA6671](https://www.rmg.co.uk/collections/objects/rmgc-object-561120)：潜水电话木箱、头带接收器及发话器电池。
- [U.S. Naval Undersea Museum，MK V Diving Rig](https://navalunderseamuseum.org/mk-v/)：标准空气型头盔的供气、电话与水面连接关系。

2026-10-03 查阅。以上支撑设备类型与系统关系，不能证明这些具体馆藏就是当前 Mark V 的配套型号。模型外形、机构相位、动画速度、流动标记和声音波形是解释性设计，未声称复原精确制造结构、流量、压力或电信号。没有导入来源图片，也没有把可见气流标记解释成真实气泡或气体颗粒。

## 验证状态

上一版自由探索的 282 项检查、旧抓取 90 项通过和批处理退出问题见[上一轮记录](2026-10-03-helmet-exploration.md)，不能直接作为本轮新增模型的验收。实机文字与深度感、舒适性和设备性能仍需真实头显验证。

本轮初次渲染发现电话两端标签重叠，已缩小标签宽度并将潜水员端简写为 DIVER UNIT。前三次验收的失败分别暴露了测试夹具问题：同帧复位后立即播放被下帧的复位观察逻辑停止；以文字启用时的网格数量比较停用后的数量；用组件枚举数量代替实际激活状态判断隐藏。修正方法是等待复位传播、比较同样的停止状态、检查根及全部渲染器的 activeInHierarchy/enabled。未删除验收目标或改写旧失败报告；TextMeshPro 3.0.7 的 OnDisable 确实会清空 MeshFilter.sharedMesh，不能把该正常行为当成模型泄漏。

最终使用 Unity 2022.3.62f3c1、Windows Editor 执行 `Museum.Exploration.Editor.HelmetExplorationValidation.RunBatch`，351/351 项通过，记录错误 0，实际进程退出码 0。[完整报告](../../artifacts/helmet-exploration/qa/runs/20261002T173923086Z-exploration-b8e73f03e90f491b92e5322124384704/report.json)记录未提交源文件哈希；[进程结果](../../artifacts/helmet-exploration/qa/runs/20261002T173923086Z-exploration-b8e73f03e90f491b92e5322124384704/process-results.json)保留前三次失败及最终成功，不覆盖历史。

验证覆盖：左右手及远/近按钮、两种线索顺序、提示/回顾、热点与整盔抓取互斥、复位保留记录、原开窗/剖面、模块停用回退；新增泵轮与活塞位姿变化、气流移动、通话波列前进/回复方向、结束静态、STOP 全隐藏、ReduceMotion 机构静止、无新增有效碰撞/交互体。六次重播后示意子树的数量保持一致：118 个 Transform、102 个 Renderer、7 个 LineRenderer、4 个停用态 Mesh 和9种引用材质；这不是设备帧时间测量或全局内存泄漏证明。

[静态核对](../../artifacts/helmet-exploration/qa/runs/20261002T173923086Z-exploration-b8e73f03e90f491b92e5322124384704/static-audit.json)确认本轮前后635个受保护文件哈希相同、新脚本GUID唯一。已检查最终[供气动作](../../artifacts/helmet-exploration/qa/runs/20261002T173923086Z-exploration-b8e73f03e90f491b92e5322124384704/07-air-pump-after-diagram-closeup.jpg)、[通话回复](../../artifacts/helmet-exploration/qa/runs/20261002T173923086Z-exploration-b8e73f03e90f491b92e5322124384704/13-voice-reply-after-diagram-closeup.jpg)和[静止模式](../../artifacts/helmet-exploration/qa/runs/20261002T173923086Z-exploration-b8e73f03e90f491b92e5322124384704/16-voice-reduced-motion-diagram-closeup.jpg)的真实Unity渲染；近景文件使用临时QA摄像机，不修改产品摄像机。这些是自动模拟输入和单眼渲染，没有冒充完整官方键鼠手测、头显或参与者测试。

## 接手试用

直接打开原 `MuseumStart` 并进入 Play，G 选择 EXPLORE → AIR / VOICE；需要重看用 REPLAY，STOP 隐藏演示，INSPECT 回原观察工具。模型与动画都由运行时脚本生成，无需运行旧Builder或再次安装探索模块。资料和代码入口见[功能说明](../helmet-exploration.zh-CN.md)。本轮未提交或推送新探索分支；环境PR #5仍是原来的独立交付。
