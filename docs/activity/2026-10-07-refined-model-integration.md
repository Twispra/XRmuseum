# 2026-10-07：Mark V 与供气／通话精细资产接入

Fttt1003 发起并负责本轮工作，Codex 辅助实现、资料边界复核和验证。用户要求先拉取最新小组项目，再替换原粗糙模型，保留现有功能与操作；通过测试才提交，并在提交后调整对应 Issue。本记录是整合候选快照，不代替 PR 合并结果或其他成员审批。

## 基准与范围

本轮先同步 `origin/main` **`afc3a9e56734d3c7230adb5b81a5dcdd7ddd016e`**（含 PR #27 主场景年代接入），在独立分支 `codex/refined-model-integration-2026-10-07` 工作。原本机 checkout 的既有改动未移入整合分支。源资产提交按原有顺序 cherry-pick 到 **`e2ee58fc42f79fc9d230d99682eeb8240aa25508`**，保留 Mark V、设备的可编辑源、历史导出、依据及旧 QA；不能把复制进入分支写成源模型重新制作。

原工作入口为 [Issue #25](https://github.com/Twispra/XRmuseum/issues/25) 和 [#33](https://github.com/Twispra/XRmuseum/issues/33)；Mark V 原资产 PR 为 [#32](https://github.com/Twispra/XRmuseum/pull/32)。本轮产品提交为 `a9d624a4a89e4bf0152792e3cab9fc8a9694f927`，已上传至 [整合 PR #35](https://github.com/Twispra/XRmuseum/pull/35)，等待小组审查，未自动合并 main。源阶段的[Mark V R2 记录](2026-10-07-mark-v-refinement-r2.md)和[设备精修记录](2026-10-07-connection-demo-refinement.md)中的“未接 Unity”是当时状态，本轮另记接入结果，不覆盖历史。

本轮修改 saved 主场景、原头盔 Prefab 与相关绑定，新增 `Assets/Museum/Features/RefinedModels/` 运行时资源、工具和验证，更新兼容材质与截面处理。编辑器、packages、公共输入与平台方向沿用 Unity 2022.3.62f3c1／Windows PC VR；不迁移为 Quest 独立 APK。个人姓名映射、其他成员本机进展和课程提交状态不在本轮推断范围。

## 完成的整合内容

- 只在 Mark V 年代索引 1 显示精细头盔，并设为默认。年代 0／2 保留原几何和占位材质；没有宣称另外两个精细模型已制作或已接入。
- 原头盔根对象、XR 抓取、Rigidbody、前窗与剖面控制、悬停和复位继续使用。绑定随年代重设前窗触发区、截面网格和两个背部接口，并保留剖面方向及归一化深度。
- 精细泵、水面电话和潜水员电话示意件替换演示设备的可见几何，沿用原 16 秒演示时钟、输入入口、重播／停止、声音开关与减少动画选项。机械动画整体采样，线路端点跟随新接口；年代切换重绑连接，不另启动第二套演示时钟。
- 壳体材质增加原创基色／法线／粗糙度贴图支持，继续提供剖切、悬停与收发强调属性。玻璃使用既有透明剖切着色器；不是把静态 PBR 外观直接套入后丢掉交互效果。

接手步骤、文件关系、节点命名和历史边界见[整合说明](../refined-model-integration.zh-CN.md)。本轮没有下载第三方模型、贴图或历史录音，资源与 AI 辅助范围见[资源记录](../external-resources.md)。

## 导出与制作中发现的问题

运行时导出保存在 `artifacts/refined-model-integration/`，不覆盖历史资产。Mark V Blender 三角形由 **409,524 → 141,502**，255 网格及 8 种材质角色保留；设备维持原几何、动画和锚点路径。四个 FBX 的源哈希／层级／边界／五时刻运动回读均通过，[汇总报告](../../artifacts/refined-model-integration/qa/runtime-preparation.json)及同目录逐资产报告可查。Unity 实际导入日志观察到 Mark V **141,262** 三角形，不能混写为 Blender 计数。

首次 Unity 安装暴露了 `MaterialPropertyBlock` 在 MonoBehaviour 字段初始化阶段创建引发的异常，以及 FBX 原始坐标轴使模型侧躺的问题。相应代码改为运行时延迟创建原生对象；当前 Mark V 显示根使用 `Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 180, 0)`，先修正原始高度方向，再转到 Unity 向上。电话膜片／锁紧件的动画轴也按实际导入层级核对。初次失败不作为通过证据，修复后的整合须重新运行。本轮未通过减少网格数量来声称 draw call 已优化，保留设备完整运动轨道优先。

保存与回归检查中还修正了三处接入问题：

- 导入 FBX 的前窗子节点直接改父级后不能可靠保存在现有 Prefab。现在独立复制 `FrontWindowPivot` 的 23 个网格，放到原交互铰链下；原导入窗门留在原层级并停用。可见窗门、玻璃和绑定均对应副本，窗座上的锁紧件仍在原模型中。
- 换模型后的几何中心与原球柄缓存偏移不一致。新增 `RefreshGeometryCenter()`，只在未握持工具时清除旧偏移，继续保留手腕方向框架。新的精细前窗／热点可能与球柄重叠，因此只在 Mark V、剖面球柄已显示、直接手 attach 点进入球柄碰撞体且球柄能接受该手时，优先让球柄接收选择。球柄被另一只手独占时不生效；射线、已持前窗及其他年代保持原规则。
- 电话膜片用相隔 0.5 秒的两个时刻比较，可能采到相同振动相位，近零差值不能代表动画没有运动，也不能据此认定压缩是原因。独立[膜片读回报告](../../artifacts/refined-model-integration/qa/diaphragm-motion-readback.json)在源和 FBX 中各采 257 个时刻（241 个密集样本加 16 个错开周期的时刻），对齐各自 take 起点，并核对局部、根相对与世界米制位置。两套电话的世界坐标采样峰峰位移约 1.591 mm，采样位置差均小于 0.00001 m；输入文件未改变。Unity 检查另外改为多帧取最大位移，按完整层级换算世界单位并补偿显示父节点。Blender 有限采样通过仍不等同 Unity 导入或连续动画完全一致。

[资产一致性报告](../../artifacts/refined-model-integration/qa/asset-parity.json)通过：四个 FBX、贴图和音轨的 Unity 副本与对应源／导出 SHA-256 一致，LFS 已展开；原 Prefab 的 386 个既有 fileID 全部保留，没有新增 GUID 冲突。它证明资源副本与对象身份关系，功能结果另见下方最终验证。

## 验证

本轮最终验证通过：Unity **2022.3.62f3c1**，六组检查退出码均为 0。报告的被测 HEAD 为 `e2ee58fc42f79fc9d230d99682eeb8240aa25508`，加本次尚未提交的整合代码／保存资产；报告保留 dirty 清单。提交前逐一复核精细整合报告的 291 个输入 SHA-256：290 个相同；唯一后续变化是剖面连续性验收脚本自身的初始化／证据记录修正，该脚本已由最终连续性报告核对并通过。运行时代码与保存资源没有后续变化。[运行索引与进程结果](../../artifacts/refined-model-integration/qa/integration-validation.json)。

| 检查 | 最终结果与证据 |
| --- | --- |
| 精细模型整合 | 113 项检查通过；[报告](../../artifacts/helmet-interaction/qa/runs/20261007T143905782Z-refined-integration-47150ed10070449b891e373727eacb46/report.json) |
| 原观察／模拟 XR 输入 | 206 项检查通过；[报告](../../artifacts/helmet-interaction/qa/runs/20261007T144141393Z-inspection-main-3ed28223123746c29ebd307dae52e291/report.json) |
| 前窗、剖面结构与体验 | 134 项检查通过；[报告](../../artifacts/helmet-experience/qa/runs/20261007T144334012Z/report.json) |
| 探索与演示 | 1486 项检查通过；[报告](../../artifacts/helmet-exploration/qa/runs/20261007T144433357Z-exploration-a766c8e5297e47e88ea72d5b3973317d/report.json) |
| 三语及热点 | 三语、逐字符字形与热点断言通过；[完整计数与报告](../../artifacts/localization/qa/runs/20261007T144721141Z-integration-8137c8f29a38481b9a972c6bd2d8d5b7/report.json) |
| 剖面连续性 | 40 组案例、49784 个数值断言通过；[报告](../../artifacts/helmet-interaction/qa/runs/20261007T144919256Z-section-continuity-6da193658b9145b1b572d63b7ed2d1bf/report.json) |

精细整合报告明确区分真实排队到 Input System／XRI 的模拟输入与公开 API 操作。旧演示回归保留原节点检查，新报告另行核对当前可见 FBX 的机械运动、膜片世界位移、实际接口与声音启停；不同套件的重复断言不累加成单一覆盖率。剖面连续性检查在临时 PrefabContents 中先调用生产绑定初始化，保留原 40 组案例及全部阈值；没有把早期失败记为通过。

原生 OpenXR 日志提示本机没有活动头显运行时，桌面验收使用模拟设备；这不是实机通过证据。各最终报告未记录产品运行时异常。源／导出／Unity 副本、386 个原 Prefab fileID、新 GUID 及 LFS 对象检查通过。

暂存后再次核对了上述 291 个输入：[暂存内容核对](../../artifacts/refined-model-integration/qa/staged-input-parity.json)全部通过。Git 保存时仅允许换行标准化和指向相同测试字节的 LFS 指针，不允许代码或资源内容发生其他变化；复核脚本保存在同目录上级 `scripts/check_staged_inputs.py`。

当前验证条件是 Windows 编辑器桌面模拟及单眼临时渲染。没有真实头显、参与者学习评价、人工试听、坐姿单手使用验收、设备性能或当前可执行构建证据。声音是原创合成方案，尺寸／旧化／内部路径延续源依据中的估算；1916 设备与 1944 DESCO 头盔的具体实物配对仍无已核实证据。

## 后续交接

测试后已上传整合分支。#25／#33 更新本轮接入状态、PR、查看步骤及待办，继续开放供小组审阅和后续合作。下一位可以继续在原资产目录补两时期模型、史料和部件，在明确的 Issue 认领后更新运行时派生资源；同一源文件、Prefab 和公共场景继续协调单一写入者。

优先补人工外观／历史审阅、音效选择、网格批处理／LOD 与目标设备性能，再按课程要求验证完整访问流程。旧 QA、坏候选的本机诊断及原源保留各自边界；可复现的提交证据使用仓库相对路径，不依赖成员机器绝对目录。
