# XRmuseum — Phase 2 技术说明草稿

**项目**：COMP5424 Extended Reality · Scenario B「Accessible Museum Experience」
**目标交付物**：可运行的博物馆 VR 体验（Windows PC VR）
**对应版本**：仓库 `main` @ `270c7b72`（2026-10-08）
**编辑器**：Unity 2022.3.62f3c1 · **XR**：OpenXR + XR Interaction Toolkit 2.6.4 · **管线**：URP

本页保留原作者 2026-10-10 的本地构建自述与哈希，并于 2026-10-11 修正范围和限制。本轮未重做构建，原始构建日志、`Player.log` 和完整产物未提供可访问的独立复核入口。现有文件是 Markdown 草稿，尚未排版为 A4 文档并确认不超过四页；正式构建交付、教学团队访问和课程提交也未由本页核实。[本轮修订与验证](activity/2026-10-11-pr38-technical-note-review-fix.md)

---

## 1. 项目与范围

目标流程是观众佩戴 PC VR 头显进入海事展厅，围绕一件**历史潜水头盔**完成「进入 → 理解 → 操作 → 回顾与复位」。现有功能提供拿放、观察、设备探索与复位入口，完整流程仍需在目标头显上走查，不能由桌面模拟结果推断已通过。

头盔以公开馆藏资料（DESCO Mark V、U.S. Naval Undersea Museum、Royal Museums Greenwich 等）为**外观参考**。重建精度为**原型级**：局部尺寸、内部构造、气路／信号与音效属于教学表达参数，**不是历史测量数据**，也不声称某件实物曾配套该设备。

Phase 2 要求完整源码、可运行构建、教学团队可访问的仓库及不超过四页 A4 的技术说明。本 PR 提供技术说明草稿与原作者的本地构建记录；正式交付状态仍需按[课程依据](course-requirements.md)另行核验。

## 2. 系统架构

Unity 工程位于 `MuseumVR/`，功能以独立模块组织在 `Assets/Museum/Features/`，各自持有自己的运行时脚本、Prefab 与自动验证：

| 模块 | 职责 |
| --- | --- |
| `HelmetInspection` | 前窗开合（铰链）、任意方向可调剖面（自定义 shader）、悬停高亮 |
| `HelmetExperience` | 头盔拿放与姿态调整、体验节奏、复位 |
| `HelmetExploration` | AIR / VOICE 自由探索：供气泵与通话设备的简化模型及机械动画 |
| `HelmetHotspots` | 三个观察热点；选择后说明面板保持 30 s 并以 1 s 淡出 |
| `HelmetEras` | **历史年代切换**（1839 标准 / Mark V / SuperLite 17B），材质档案与 `RefinedModels` 按年代切换可见几何 |
| `Localization` | 简 / 繁 / 英三语；静态字绑定 key，动态字走 `MuseumText.Set` |
| `MaritimeGallery` | 展厅环境与展陈组织 |
| `RefinedModels` | 精细 Mark V 头盔与供气／通话设备的运行时资产 |

主场景 `Assets/Museum/Scenes/MuseumStart.unity` 是构建场景列表第 0 位，也是唯一启用场景。共享高冲突文件（主场景、公共输入、Packages、ProjectSettings）由协作规则限定**单一写入者**，避免并行覆盖。

## 3. 观众旅程

1. **进入**：入口提供标识与语言切换；站立／坐姿的可见性和触达须在目标设备上核验。
2. **理解**：展签信息**渐进式披露**——先给一句话，再按需展开；三个热点分别说明窗口、供气与通话。
3. **操作**：拿起头盔并调整姿态；开合前窗；沿任意方向拉动剖面观察内部；切换历史年代；进入 AIR / VOICE 观察设备机械动画。
4. **回顾与复位**：通过复位入口恢复展项，供再次使用；人工走查须确认各功能复位后的状态。

这些是既有功能入口组成的拟验收旅程；完整使用、退出和工作人员复位仍需人工走查。

## 4. 无障碍与多语言设计

- 入口、按钮与热点说明提供文字，AIR / VOICE 演示提供视觉反馈且声音可关闭。热点的可用／悬停／已访问状态目前仍由颜色区分，没有逐热点状态的文字或形状替代，不能据此声称全部关键信息均有三重冗余。
- 说明内容按需展开，旨在减少同时呈现的信息；按钮分组 + 文案保持简短，实际认知负担仍待参与者评价。
- 支持**简体中文 / 繁体中文 / 英文**切换（205 条文案、536 个静态字形）；静态文字绑定 key，动态文字统一走 `MuseumText.Set`，避免每帧覆盖语言切换结果。
- 展项提供复位入口；坐姿触达、单手使用及交互尺寸仍待基于目标设备和访客的设计基准核验，目前没有可引用的坐姿可及范围验收。

## 5. 构建与运行环境

| 项 | 值 |
| --- | --- |
| 编辑器 | Unity 2022.3.62f3c1（Windows） |
| 目标平台 / 架构 | Windows Standalone / x86_64 |
| XR | OpenXR（PC 标签页已启用）+ XR Interaction Toolkit 2.6.4 |
| 场景 | `MuseumStart.unity`（Scenes In Build 第 0 位） |
| 二进制资产 | 走 Git LFS；原构建来源版本记录为 61 个文件（FBX / BLEND / WAV / PSD 等）。打开工程前须另外确认工作区 LFS 文件已展开；`git lfs fsck` 通过不能代替此检查 |

**构建方式**：`File → Build Settings` → 平台选 Windows / x86_64 → Build；
亦可命令行批处理（`-batchmode -quit -buildWindows64Player <输出>`）。

### 原作者的构建记录（本轮未独立复核）

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

构建产物按仓库约定不进 Git；上述自述及原作者所报本地保存位置见
[`docs/activity/2026-10-10-phase2-build-and-note.md`](activity/2026-10-10-phase2-build-and-note.md)。该记录没有提供可访问的原始构建／播放器日志或完整构建数据指纹；单个 exe 的哈希不能独立绑定其同目录的数据与输入版本。本轮保留原记录，不将其写成新的独立构建验收。

### 原作者的产物运行记录（无头显本机）

| 检查项 | 结果 |
| --- | --- |
| 双击 `MuseumVR.exe` | **可启动**，渲染出主场景（展厅 + 中央头盔 + 各交互面板），`Player.log` 正常产生 |
| OpenXR 在播放器中的装配 | 加载到 `UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json`，匹配 **1 个 displays + 1 个 inputs** 描述符 |
| XR 会话 | **未建立**：`xrGetSystem: XR_ERROR_FORM_FACTOR_UNAVAILABLE`（本机无头显）→ 因此无 VR 输入，画面静止、无法操作 |
| 本地 OpenXR 运行时 | 存在（诊断报告列出 41 项运行时扩展），但没有已连接的头显 |
| 键盘／鼠标 | 播放器已初始化输入与触摸支持 |

> **原记录结论**：作者报告产物可启动并渲染主场景，OpenXR 子系统描述符已匹配；
> `FORM_FACTOR_UNAVAILABLE` 与本机没有头显的情况一致。XR 会话和 VR 交互未通过该次运行验证，
> 也不能据此排除目标设备上的其他工程或构建问题。本轮未重跑或独立复核该产物。

## 6. 验证证据与已知限制

**历史自动验证记录**（来源：仓库 `docs/activity/`、`artifacts/**/qa/`，记录环境为 Unity 2022.3.62f3c1 / Windows；本轮没有重新运行这些测试）：

- 精细模型整合回归：**135 项通过**，失败 0，Unity 进程退出 0。
- 三语主场景整合：**80,999 项通过**（其中热点交互 768 项），失败 0，退出 0。
- PR #24 主线整合六组回归：76 / 206 / 1486 / 84,346 / 11 / 12 次断言，六进程均退出 0。
- 年代模块：淡变取消与旧切换覆盖修复 **23 项**定向验证；三年代目视差异与剖切兼容探针通过。
- 热点生命周期：EditMode `Museum.Hotspots.Tests` **18/18 通过**。

其中 135 项／80,999 次断言及 18 项生命周期结果见[组合验收与报告入口](activity/2026-10-08-pr35-pr36-combined-acceptance.md)及[热点修复记录](activity/2026-10-08-pr36-expiry-fix.md)；PR #24 六组回归见[原整合记录](activity/2026-10-06-pr19-main-integration.md)。重复文本与数值断言不能当作独立用户用例数量，历史模拟通过也不能替代当前可执行构建或实机验证。

**已知限制（重要，不做夸大）**

1. **未做真实头显验证**：本机装有 OpenXR 运行时但**没有头显**；构建产物启动后 XR 会话因
   `XR_ERROR_FORM_FACTOR_UNAVAILABLE` 未建立，**交互流程未在真机验证**。自动测试使用模拟输入设备 ——
   模拟输入不等于真机，也不等于用户研究。
2. **未做参与用户研究**：按课程安排留待 Phase 3。
3. **未测目标设备性能**：帧率、draw call、LOD 均未验收。
4. **重建精度为原型级**：几何、材质与音效为设计参数，非历史复原或实测数据。
5. **年代造型**：年代切换会切换可见几何及相关绑定。Mark V（索引 1）显示精细模型；年代 0／2 显示原占位几何与各自材质，其余两年代的精细模型仍未接入。详见[精细资产整合说明](refined-model-integration.zh-CN.md)。
6. **分件较多**，未声明完成 LOD 或 draw call 优化。
7. **正式交付仍待核验**：原始构建／播放器日志和产物未供本轮独立复核，Markdown 的 A4 输出页数未确认；完整交付与教学团队访问不能由本 PR 的文档合并推断完成。

## 7. AI 辅助声明

本项目已记录的 AI（Codex）辅助范围包括代码、Editor 工具、运行时脚本、文档、自动测试与证据整理；各次授权、素材来源、许可与具体辅助范围见 `docs/external-resources.md` 及对应活动记录，本说明不据此推断未记录的成员工作。

AI 辅助**不替代**成员人工审批、真实头显测试与用户研究；个人反思短文由成员本人撰写。课程 Canvas 最新 AI 政策尚未在本轮核验，须由具备课程权限的成员确认，不能由本说明声明已满足全部条款。

---

_本说明为 Phase 2 准备稿，原构建自述绑定上述 commit；正式排版、交付与设备验收另行核验。_
