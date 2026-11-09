# Issue #61 · 年代演示入口与观察点验收

评估日期为 2026-11-09（北京时间）。本轮从 `origin/main@6088c736e89d05101e09645c1796556012a6fd5d` 开始，使用已保存的 `MuseumStart`。#59 已接入默认实体站，#44 和 #69 已给 SuperLite 17B 与早期头盔绑定三组观察点。按本展项的内容安排，供气和通话演示只在 Mark V 可用。两个时期的实物结构和通信能力仍以史料为准。 [Issue #61](https://github.com/Twispra/XRmuseum/issues/61) · [PR #59](https://github.com/Twispra/XRmuseum/pull/59) · [PR #44](https://github.com/Twispra/XRmuseum/pull/44) · [PR #69](https://github.com/Twispra/XRmuseum/pull/69)

## 开工前评分标准

| 分值 | 必需验收项 | 通过依据 |
| --- | --- | --- |
| 35 | 按实际生效的模型年代限制 AIR／VOICE | 使用 `RefinedHelmetBinding.AppliedEraIndex`。索引 0／2 的图形、提示、碰撞与 Direct／Ray 目标均停用。切回索引 1 后原入口、Mark V 后部锚点和两项演示恢复。保留持窗延迟切档，多次往返无重复控件。 |
| 30 | 演示生命周期与握持安全 | 在 Mark V 分别开启供气与通话并验证切档、握住设备时切档、复位。检查线路、设备、音频、高亮与关闭演示入口收尾。持续握持不重新选中，松开后恢复可选，切回可重新打开、转动并关闭设备。 |
| 20 | 六个观察点、史料与三语 | 核对两档实际网格和锚点，验证正、侧、背视角以及适用的开窗、旋转、剖面和复位。用正常输入证明可见姿态下实际首个命中和选择，附三语与诊断视图。记录看不到部件的视角及其原因。 |
| 15 | 回归、证据与限制 | 既有 Mark V 操作、三语、访问记录、固定剖面轨道与其他交互通过回归。记录基线、被测版本、额外改动、Unity 版本、命令、报告、退出状态及实机限制。 |

四项必需验收通过、无阻断问题、独立评审总分至少 90/100 后才记为工程验收通过。真实头显的可读性、手柄触达、声音表现、舒适度和性能另行记录。后续人工核对以当次环境和实际版本为准。

## 观察点与史料对照

| 年代 | 观察点 ID 与部件 | 绑定网格和锚点 | 依据与边界 |
| --- | --- | --- | --- |
| 0 · 1839 | `front-window` · 正面观察窗 | `FrontPort_Glass`，位于活动虚拟面板。沿当前窗铰父节点的正面方向与网格求交，向外偏 38 mm，再存为玻璃网格局部点。 | 馆藏 1881-47 照片可见带护栏观察窗。展示开合使用虚拟铰链。 |
| 0 · 1839 | `bonnet` · 帽体 | `Bonnet_RoundedCopper`，沿头盔局部右侧与网格求交，外偏 25 mm，随壳体变换。 | 馆藏照片可见圆顶帽体，背部外形和部件尺寸为估算。 |
| 0 · 1839 | `breastplate` · 胸板 | `Breastplate_DishedCopper`，从访客侧与网格求交，外偏 25 mm，随胸板变换。 | 馆藏照片可见宽胸板，背部紧固件和部件尺寸为估算。 |
| 2 · SuperLite 17B | `front-window` · 前窗 | `Glass_Port_520-004`，使用保存场景 `attachments[0]` 的玻璃网格局部点，随展示观察面板转动。 | Kirby Morgan 零件图 #241108001 第 35 项为面窗 P/N 520-004。实物为螺栓固定，展示使用虚拟侧开。 |
| 2 · SuperLite 17B | `bonnet` · 外壳 | `Shell_Yellow_520-065_ESTIMATED`，使用保存场景 `attachments[1]` 的壳体网格局部点。 | 厂家零件图第 13 项为壳体 520-065。网格外形比例依据照片估算。 |
| 2 · SuperLite 17B | `breastplate` · 颈封与周围夹箍 | `NeckDam_Neoprene_510-533`，使用保存场景 `attachments[2]` 的颈封局部点 `(-1.1419615e-12, .0013711446, .00142229)`。#69 沿真实后上侧顶点外偏 35.36 mm，使点脱离台面实体。 | 访客名称为 Neck seal。Kirby Morgan #260319004 印刷页 17BNK-13 对应 510-533 颈封，#241108001 第 66 项为颈箍／轭总成。研究资料采用 500-010 外观参照，照片的通信配置与制造年份仍未知。 |

两档共用 `HelmetEraVisitorContent` 的观察点与说明。早期在应用年代时计算表面局部锚点，SuperLite 使用场景中保存的局部锚点。二者随后都用所属网格的变换跟随。视角检查应区分被实体遮挡、被切面裁去、以及在可见表面上的真实命中。验证开窗后的观察点应相对同一玻璃局部点保持位置，正侧背诊断视图不要求每个点同时可见。 [Science Museum Group 馆藏 1881-47](https://collection.sciencemuseumgroup.org.uk/objects/co40614/first-closed-diving-helmet-and-breastplate) · [早期几何与来源](../../artifacts/helmet-modeling/early-1881-47/delivery/geometry-summary.json) · [厂家总装零件图 #241108001](https://www.kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf) · [厂家颈箍手册 #260319004](https://www.kirbymorgan.com/sites/default/files/2026-03/superlite-17b-neck-clamp-area-including-internal-chin-strap-and-yoke-260319004.pdf) · [SuperLite 来源对照](../../artifacts/helmet-refinement/eras/superlite-17b/references/whole-helmet-source-map-v2.zh-CN.md) · [#69 颈封校准与图文回归](2026-10-18-pr69-early-merge-fix.md)

## 既有验证的覆盖范围

#69 联合年代验证 `7e5b539` 为 959/959。随后 `024d039` 的年代验证为 989/989，Unity 工程树与开工主线相同。原验证用生产射线验证三档观察点的首面、选择和释放，同时覆盖前窗开合、剖切、轨道、旋转、复位和三语。选点步骤在关闭前窗、进入剖切和旋转之前执行。既有测试因此只能证明该姿态的选取，不能直接作为开窗、剖切、旋转后的选取证据。本轮需要补充这些组合状态，并在停用入口和清理设备的新增用例中保留原有操作断言。 [联合验收](2026-10-18-pr65-66-69-fix-and-validation.md) · [主线同树验证](2026-10-18-pr67-review-and-merge.md)

## 本轮结果与独立复审

- 原工作区位于 `codex/phase2-technical-note-2026-10-18` 的 `5a6bc0a`，原有 ProjectSettings 修改及未跟踪资料保留。本轮在 E 盘独立 worktree 的 `codex/issue-61-era-demo` 从 `origin/main@6088c736` 开工，复用关闭的同主线 Library 副本。工程内 20 份 LFS 资产逐项验证展开后的 SHA-256，工作区 LFS 对象检查通过。
- 首轮 Unity 2022.3.62f3c1。实体站 855/855，通过且自然退出 0。年代组合执行至 SuperLite 剖切用例时，夹具在当前切面保留侧没有找到满足 20 mm 净空的候选，结果为失败，退出 1。Direct 执行至首个停用控件的诊断时，诊断访问活动 Collider 集合而抛异常，结果为失败，退出 1。三轮各自 1,299 个 tracked 工程文件零差。
- 独立初审发现台面年代标题仍读取请求值，在旧窗口持握期间可早于模型切换。修复轮将标题改为读取实际显示年代，补请求与显示分离时的标题检查。剖切夹具在保留滑轨生产输入断言后，选择能实际露出观察点的一侧并记录切面距离；Direct 诊断改记录停用、注销与真实模拟 Grip 状态。

- 运行时仅修改实体站。AIR／VOICE 根对象按实际生效的 Mark V 档启停，保留原对象身份。命令入口另检查实际档位，切档沿用取消与停止，台面年代标题也随实际模型变化。没有写入场景、Prefab、模型、字体或公共输入。
- 修复轮二确认 Editor 协程读取的输入缓冲区无法证明玩家 Grip。修复轮三在 Input System 的 Dynamic 更新后采样，四组按下／松开均实际到达；同时保留 Collider 停用、XRI 注销、无悬停选中及无确认断言。
- 最终验证在 Windows／Unity 2022.3.62f3c1，从保存 MuseumStart 运行。命令为 Unity `-batchmode -projectPath <worktree>/MuseumVR -logFile <独立日志> -executeMethod <入口>`，无 `-quit`。额外未提交项为活动记录和其他运行产物，工程已提交。

| 批次与入口 | 实际被测提交 | 结果与证据 |
| --- | --- | --- |
| `Museum.Localization.EditorTools.VisitorEraValidation.RunBatch` | `e7df73bf244b445be17db7f16aab6e9fc878f45c` | 1131/1131。[报告](../../artifacts/helmet-interaction/qa/runs/20261109-issue61-era-gate/visitor-report.json) · [退出](../../artifacts/helmet-interaction/qa/runs/20261109-issue61-era-gate/visitor-process.json) |
| `Museum.Diegetic.Editor.HelmetDiegeticValidation.RunBatch` | `e7df73bf244b445be17db7f16aab6e9fc878f45c` | 855/855。[报告](../../artifacts/helmet-interaction/qa/runs/20261109-issue61-era-gate/diegetic-report.json) · [退出](../../artifacts/helmet-interaction/qa/runs/20261109-issue61-era-gate/diegetic-process.json) |
| `Museum.Diegetic.Editor.HelmetDiegeticDirectValidation.RunBatch` | `4ee09d052fcc6f409b3fbaf18728df2fe472d58a` | 43/43。[报告](../../artifacts/helmet-interaction/qa/runs/20261109-issue61-era-gate/direct-report.json) · [退出](../../artifacts/helmet-interaction/qa/runs/20261109-issue61-era-gate/direct-process.json) |

三组均自然退出 0，1,299 个受 Git 跟踪的工程文件分别前后零差。两个被测提交的工程差异仅为 Direct Editor 验证器。持握切档用实际模拟手柄选中设备后调用同一个实体站切档命令，保留物理交互中正在拖动时互斥其他控件的设计。重新开启两种演示、观察点选择、开窗、滑轨和旋转均有生产模拟输入证据。 [正侧背诊断图](../../artifacts/helmet-interaction/qa/runs/20261109-issue61-era-gate/front-side-rear.png) · [1839 三语卡](../../artifacts/helmet-interaction/qa/runs/20261109-issue61-era-gate/cards-era-0.png) · [SuperLite 三语卡](../../artifacts/helmet-interaction/qa/runs/20261109-issue61-era-gate/cards-era-2.png)

独立子 Agent 复审 **97/100**。年代门控与恢复 35/35，清理与 Grip 29/30，六点与三语 18/20，回归与证据 15/15。四项必需组通过，无阻断。清理组的扣分来自持握时以命令触发切档的验证边界。视觉组的扣分来自单眼近景中既有 SuperLite 台面文字叠到模型，以及后视可见反面标签。近景重叠在 #67 同姿态已存在，列入实机可读性检查。

真实头显／控制器、双眼可读性、舒适度、性能、人工试听和新 Player 构建待验证。实现和评审有 Codex 辅助，未引入可分发第三方素材。独立 AI 复审用于本任务验收，团队合并审查另行进行。
