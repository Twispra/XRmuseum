# 2026-11-20 展台导航审查修正

在独立工作树从 [PR #88](https://github.com/Twispra/XRmuseum/pull/88) 的原提交 `2817ae8` 接手，根据审查修正导航释放与过渡保护。
XRI 2.6.4 在同一帧先发送选择退出，再清除已离开的悬停。按钮现在保留待确认射线，等待该帧交互处理完成，再核对射线是否仍指向按钮、Grip 与 Trigger 是否都已松开。
导航在请求时及实际移动前检查抓取、拖动、返回与结束参观确认状态。淡出期间新开始操作并持续到移动时，保留原视点并恢复移动与转向。
Windows 复跑在最后返回中央展台时查到两个占位检测命中都来自当前访客右手。实体手模型为独立场景根级对象的子级，原先按 XR Origin 父级关系无法排除它。现在按实体手的控制器归属识别当前访客，并保留对展台及其他实体障碍的检查。

生产输入验证新增一条合成控制器状态，同时移出射线并松开 Grip。另一条通过现有公开手势夹具在淡出请求后启动旋转，验证移动取消、视点不变、移动状态恢复，随后结束夹具手势。最后用合成右手实际占用中央站位，验证自身实体手不会阻断返回。
修正范围为 GalleryNavigation 的运行时、验证与 10 个新 meta 的行尾空白，以及导航说明。meta 的 GUID 保持原值。

## 已验证

- 以相同的 XRI 2.6.4 事件顺序在 Windows .NET 10 夹具编译原提交按钮代码，重现同帧移出／松开仍发起 1 次导航。修正按钮代码通过同帧取消、提前一帧移出、正常 Grip、正常 Trigger、双键分次释放、仍持键但 XRI 结束选择六项测试。
- 夹具用于事件边界测试。实际 XR 射线与淡出检查使用保存场景的 Unity 验证入口 `Museum.Navigation.Editor.GalleryNavigationValidation.RunBatch`。
- 启动验证前，独立工作树 43 个 Unity 工程 LFS 文件已展开，字节数和 SHA-256 都与对应指针一致。
- Windows 首轮 `4efd47e` 以及附诊断的 `2b5871f` 均发现同一既有返回失败，进程自然退出 1。附诊断的运行完成 88/89 项，场景地面有效，无抓取、返回、拖动或确认页，两处命中均为 `Museum Physical Hands/Right Physical Hand`。两个进程退出后各 1,462 个工程文件 SHA-256 零差。

最终工程版本 `78f384d0b170cec69d75b6dee2355427b352259e` 在干净的独立工作树使用 Unity **2022.3.62f3c1**、Windows、Direct3D 11 复跑。

| 保存场景 | 检查 | 进程 |
| --- | --- | --- |
| `Assets/Museum/Scenes/MuseumStart.unity` | 137/137，报告错误 0 | 自然退出 0 |
| `Assets/Museum/Features/DuskWorkshop/DuskWorkshop.unity` | 137/137，报告错误 0 | 自然退出 0 |

两次均验证同帧取消、淡出期间开始拖动、占位实体拦截和自身实体手占位后返回成功。原 Grip、Trigger、双键、复位、禁用／重新启用、追踪局部姿态和三语检查通过。每次退出后 1,462 个工程文件的 SHA-256 前后相同。原始报告、进程记录和诊断截图保存在本地独立运行目录，原 PR 的 macOS 证据保留。

复跑使用 `-batchmode -force-d3d11 -buildTarget Win64 -executeMethod Museum.Navigation.Editor.GalleryNavigationValidation.RunBatch`，指定 `-projectPath`、新的 `-navigationOutput` 和 `-logFile`。在黄昏工房另传 `-navigationScene Assets/Museum/Features/DuskWorkshop/DuskWorkshop.unity`。

真实头显、控制器触达、双眼可读性、舒适度、Windows Player 构建和用户研究仍待验证。
