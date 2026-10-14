# 虚拟手与双手头盔协作

日期：2026-10-14（北京时间）。任务 [#50](https://github.com/Twispra/XRmuseum/issues/50)，用户在当前对话明确授权按已讨论的分阶段流程开发。负责人 Twispra，Codex 辅助实现。工作分支 `codex/physical-hands-2026-10-14`，基于实际 `origin/main e5eebe0527383abdc6b1bbb3e62d0504ecfdf5f6`（#49 已合入），无未合并运行时依赖。

## 范围与实现

目标为虚拟手、近距拿放、碰撞阻挡及双手协作。引用聊天仅提供体验思想；未采用其 Unity 6 原型代码，也未升级 Unity 2022.3.62f3c1 / XRI 2.6.4 / OpenXR 或改变 PC VR 平台。

- 新增 `Assets/Museum/Features/PhysicalHands/`：左右原创手套 Prefab、五指预设姿势、独立跟踪目标与世界空间碰撞代理、双手目标转换器、开发接入工具与定向验收。
- 使用带扫掠、滑动和旋转可达性检查的简化手套包围体，按 Grip 收缩；不是逐指动力学。Direct/Poke 查询在控制器、移动和渲染前更新之后重新对齐代理；追踪目标独立读取。
- XRI 继续独占物体刚体运动。抓取目标对每次 `1→2→1` 重新建相对基线，保留握点与比例，限制物理追逐误差。
- 头盔通过 opt-in 开关允许不同手分别拿本体、开窗或操作剖面。同手的 Direct/Ray 仍不能同时抓本体与子部件。子部件按相对头盔的手姿态驱动；两手随头盔共动保留局部状态。
- 跟踪丢失、原点明显瞬移/45°快速转向及持有握点偏差过大时取消抓取，避免变成窗口短点击或投掷；恢复后需松 Grip。既有 Home/RESET/FLOAT 保留。
- 共享 `MuseumStart.unity` 与 `InspectableHelmet.prefab` 由本对话单一整合；另有 `PhysicalHandsLab.unity` 独立保存实验入口。既有 GUID、XR 摄像头/Origin、Samples、Input Actions、Packages、ProjectSettings 与模型源/导出不改。

原 `E:/XRmuseum` 位于 `main 4578e66`，已有场景、设置、RefinedModelValidation 及文档改动全部保留。本轮使用新 managed worktree；未把原目录未知改动纳入分支，也未把旧原型生成器用于初始化。

## 验证与设备反馈

本记录当前是候选实现的开发记录。实际 Unity 2022.3.62f3c1 已完成编译和保存资产接入，并生成张手/握持静态预览。初次编译与工具调试日志保留本地，不作为验收通过证据。

定向 Play Mode、原功能回归及当前 Windows 构建的结果将在实际运行后补充，绑定被测 commit、独立运行目录和 source 指纹。真实头显的手掌对齐、延迟、碰撞感、握持舒适性和设备性能尚未验证；静态预览不代表设备录屏或用户研究。

第一次设备反馈按[五分钟步骤](../physical-hands.zh-CN.md)进行。后续将根据反馈调整手感，并在 PR 中记录完成、验证、限制与下一步；本任务未授权自动合并。

## 来源与交接

手套几何通过 Unity 内置 primitive 和本项目脚本生成，材料原创；无新第三方模型、贴图或手部 SDK。既有 XRI 样例与来源沿用[资源记录](../external-resources.md)。Codex 辅助设计、代码、几何、验证和文档，不冒充成员实机结果或人工批准。

当前最小下一步：完成定向与受影响回归，提供可运行候选供用户头显反馈。日常只打开已保存资产；Install 是本轮开发工具，不重建已有手套资产，也不调用历史 HelmetPrototypeSetup.Build。
