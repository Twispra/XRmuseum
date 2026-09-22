# 初始化验证记录

验证日期：2026-09-22。范围是独立 Unity 工程的基础环境与起始场景，不是课程阶段 2 的完整验收。

| 检查 | 结果 |
| --- | --- |
| Unity 版本 | 2022.3.62f3c1，ProjectVersion.txt 已记录 |
| 场景 | Assets/Museum/Scenes/MuseumStart.unity，已加入 Build Settings |
| 渲染 | URP 14.0.12、线性色彩空间、4x MSAA |
| 平台 | StandaloneWindows64 / Direct3D 11 |
| XR | OpenXR Loader 已启用，Single Pass Instanced |
| 控制器配置 | Oculus Touch、Valve Index、HTC Vive、Khronos Simple Controller |
| 资源检查 | 起始场景缺失脚本数 0；未发现重复资源 GUID；计划提交的资源均有 .meta |
| Editor Play Mode | 展厅、XR Device Simulator、左右控制器与射线正常显示；已补齐 Input Action Manager，复测未再出现输入动作禁用警告 |
| Windows Development 构建 | 成功，0 errors，2 warnings，用时约 59 秒 |
| Git 范围 | 忽略 Library、Temp、Logs、UserSettings、Builds 和本地临时文件；FBX 等二进制资源使用 LFS |

构建的两条 warning 都是 OpenXR 包的可选迁移建议：PoseControl 类型及 StickControl 摇杆类型。当前保留已验证包组合的默认行为，二者均不阻止 Play 或构建。

本地构建在 `Builds/Windows/MuseumVR.exe`，与同目录的数据文件一起使用；构建产物没有提交 Git。它是基础场景的开发验证包，不是正式课程交付版本。

本机当前 OpenXR runtime 未找到可用头显，返回 `XR_ERROR_FORM_FACTOR_UNAVAILABLE`。模拟器仍能运行，但尚未验证真实头显、真实手柄输入、传送落点、抓取体验、性能和舒适度。不要将本记录写成硬件验收通过。

六位成员接手后，应在目标设备上验证博物馆用户流程，并将后续原创功能与模板、样例、AI 辅助初始化内容分别记录。
