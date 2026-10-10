# XRmuseum — Phase 2 技术说明

**项目**：COMP5424 Extended Reality · Scenario B「Accessible Museum Experience」
**交付物**：可运行的博物馆 VR 体验（Windows PC VR）
**对应版本**：仓库 `main` @ `270c7b72`（2026-10-08）
**编辑器**：Unity 2022.3.62f3c1 · **XR**：OpenXR + XR Interaction Toolkit 2.6.4 · **管线**：URP

---

## 1. 项目与范围

观众佩戴 PC VR 头显进入一处海事展厅，围绕一件**历史潜水头盔**完成「进入 → 理解 → 操作 → 回顾与复位」的完整流程；工作人员可随时把展项复位，供下一位参观者使用。

头盔以公开馆藏资料（DESCO Mark V、U.S. Naval Undersea Museum、Royal Museums Greenwich 等）为**外观参考**。重建精度为**原型级**：局部尺寸、内部构造、气路／信号与音效属于教学表达参数，**不是历史测量数据**，也不声称某件实物曾配套该设备。

本轮（Phase 2）交付：完整源码 + Windows 可运行构建 + 本技术说明。

## 2. 系统架构

Unity 工程位于 `MuseumVR/`，功能以独立模块组织在 `Assets/Museum/Features/`，各自持有自己的运行时脚本、Prefab 与自动验证：

| 模块 | 职责 |
| --- | --- |
| `HelmetInspection` | 前窗开合（铰链）、任意方向可调剖面（自定义 shader）、悬停高亮 |
| `HelmetExperience` | 头盔拿放与姿态调整、体验节奏、复位 |
| `HelmetExploration` | AIR / VOICE 自由探索：供气泵与通话设备的简化模型及机械动画 |
| `HelmetHotspots` | 三个观察热点；选择后说明面板保持 30 s 并以 1 s 淡出 |
| `HelmetEras` | **历史年代切换**（1839 标准 / Mark V / SuperLite 17B），数据驱动材质档案 |
| `Localization` | 简 / 繁 / 英三语；静态字绑定 key，动态字走 `MuseumText.Set` |
| `MaritimeGallery` | 展厅环境与展陈组织 |
| `RefinedModels` | 精细 Mark V 头盔与供气／通话设备的运行时资产 |

主场景 `Assets/Museum/Scenes/MuseumStart.unity` 是构建场景列表第 0 位，也是唯一启用场景。共享高冲突文件（主场景、公共输入、Packages、ProjectSettings）由协作规则限定**单一写入者**，避免并行覆盖。

## 3. 观众旅程

1. **进入**：站立／坐姿进入展厅，入口标识与语言切换可见。
2. **理解**：展签信息**渐进式披露**——先给一句话，再按需展开；三个热点分别说明窗口、供气与通话。
3. **操作**：拿起头盔并调整姿态；开合前窗；沿任意方向拉动剖面观察内部；切换历史年代；进入 AIR / VOICE 观察设备机械动画。
4. **回顾与复位**：随时返回初始状态；工作人员可一键复位，保证展项可重复使用。

每个操作都有明确的进入与退出路径，避免停留在不可恢复的状态。

## 4. 无障碍与多语言设计

- 关键信息**不依赖单一通道**：颜色、文字、位置三重冗余，不要求音频。
- 说明内容按需展开，降低一次性认知负担；按钮分组 + 文案保持简短。
- 支持**简体中文 / 繁体中文 / 英文**切换（205 条文案、536 个静态字形）；静态文字绑定 key，动态文字统一走 `MuseumText.Set`，避免每帧覆盖语言切换结果。
- 交互尺寸与取放位置按坐姿可及范围设计；复位入口对工作人员公开。

## 5. 构建与运行环境

| 项 | 值 |
| --- | --- |
| 编辑器 | Unity 2022.3.62f3c1（Windows） |
| 目标平台 / 架构 | Windows Standalone / x86_64 |
| XR | OpenXR（PC 标签页已启用）+ XR Interaction Toolkit 2.6.4 |
| 场景 | `MuseumStart.unity`（Scenes In Build 第 0 位） |
| 二进制资产 | 走 Git LFS（FBX / BLEND / WAV / PSD 等，共 61 个文件）；打开工程前须确保 LFS 已展开 |

**构建方式**：`File → Build Settings`（或 Build Profiles）→ 平台选 Windows / x86_64 → Build；
亦可命令行批处理（`-batchmode -quit -buildWindows64Player <输出>`）。

### 构建证据

| 项 | 值 |
| --- | --- |
| 源码版本 | `main` @ `270c7b72`（2026-10-08） |
| 构建时间 | 2026-10-10 15:48–15:51（GMT+8） |
| 构建方式 | 编辑器批处理：`Unity.exe -batchmode -quit -projectPath "…\MuseumVR" -logFile <log> -buildWindows64Player "<out>\MuseumVR.exe"` |
| 编辑器 | Unity 2022.3.62f3c1（`1623fc0bbb97`，`2022.3/china_unity/release`） |
| 目标平台 | Windows x86_64（StandaloneWindows64） |
| 退出码 | **0**（日志结尾：`Exiting without the bug reporter. Application will terminate with return code 0`） |
| 编译错误 | 0（`error CS` 0 条；仅 2 条 OpenXR 官方*可选*弃用建议） |
| 产物 | `MuseumVR.exe` + `MuseumVR_Data\` + `MonoBleedingEdge\` + `UnityPlayer.dll` |
| 产物 SHA-256 | `MuseumVR.exe` = `059c313352892718258a120b87bdc6fc7c23ce67181084abe14f86f204f5dddb` |
| 构建体积 | 完整构建 **170.0 MB**；用户资产 95.4 MB（贴图 74.1%、网格 13.2%、动画 6.4%） |
| 二次构建（GUI） | Unity 图形界面构建：`Build completed with a result of 'Succeeded' in 19 seconds`；因输入未变属增量构建，产物与上表一致 |

构建产物按仓库约定不进 Git；证据与运行日志见
[`docs/activity/2026-10-10-phase2-build-and-note.md`](activity/2026-10-10-phase2-build-and-note.md)。

### 产物运行证据（目标机器实测）

| 检查项 | 结果 |
| --- | --- |
| 双击 `MuseumVR.exe` | **可启动**，渲染出主场景（展厅 + 中央头盔 + 各交互面板），`Player.log` 正常产生 |
| OpenXR 在播放器中的装配 | 加载到 `UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json`，匹配 **1 个 displays + 1 个 inputs** 描述符 |
| XR 会话 | **未建立**：`xrGetSystem: XR_ERROR_FORM_FACTOR_UNAVAILABLE`（本机无头显）→ 因此无 VR 输入，画面静止、无法操作 |
| 本地 OpenXR 运行时 | 存在（诊断报告列出 41 项运行时扩展），但没有已连接的头显 |
| 键盘／鼠标 | 播放器已初始化输入与触摸支持 |

> **结论**：构建产物**能运行、能渲染主场景、OpenXR 已在播放器中装载**；
> 无法操作是**缺少头显**所致（`FORM_FACTOR_UNAVAILABLE`），属于「已知限制」中声明的未验证项，
> **不是构建或工程缺陷**。

## 6. 验证证据与已知限制

**可核验证据**（来源：仓库 `docs/activity/`、`artifacts/**/qa/`，均在 Unity 2022.3.62f3c1 / Windows 下实际运行）：

- 精细模型整合回归：**135 项通过**，失败 0，Unity 进程退出 0。
- 三语主场景整合：**80,999 项通过**（其中热点交互 768 项），失败 0，退出 0。
- PR #24 主线整合六组回归：76 / 206 / 1486 / 84,346 / 11 / 12 次断言，六进程均退出 0。
- 年代模块：淡变取消与旧切换覆盖修复 **23 项**定向验证；三年代目视差异与剖切兼容探针通过。
- 热点生命周期：EditMode `Museum.Hotspots.Tests` **18/18 通过**。

**已知限制（重要，不做夸大）**

1. **未做真实头显验证**：本机装有 OpenXR 运行时但**没有头显**；构建产物启动后 XR 会话因
   `XR_ERROR_FORM_FACTOR_UNAVAILABLE` 未建立，**交互流程未在真机验证**。自动测试使用模拟输入设备 ——
   模拟输入不等于真机，也不等于用户研究。
2. **未做参与用户研究**：按课程安排留待 Phase 3。
3. **未测目标设备性能**：帧率、draw call、LOD 均未验收。
4. **重建精度为原型级**：几何、材质与音效为设计参数，非历史复原或实测数据。
5. **年代造型**：三个年代目前仅切换材质；精细几何目前只有 Mark V，其余两年代仍为占位模型。
6. **分件较多**，未声明完成 LOD 或 draw call 优化。

## 7. AI 辅助声明

本项目的代码、Editor 工具、运行时脚本、文档、自动测试脚本与证据整理，均在**成员明确授权下由 AI（Codex）辅助**完成；素材来源、许可与 AI 使用范围逐条记录于 `docs/external-resources.md`。

AI 辅助**不替代**成员人工审批、真实头显测试与用户研究；个人反思短文由成员本人撰写。AI 使用遵循课程 Canvas 最新政策——该政策由具备课程权限的成员另行核对确认。

---

_本说明为 Phase 2 交付的一部分，与上述 commit 绑定。_
