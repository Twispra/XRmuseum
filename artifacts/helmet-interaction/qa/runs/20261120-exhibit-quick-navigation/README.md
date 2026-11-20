# 展台快捷导航验证

基线 `origin/main`：`5ddaab5a101a153a0d864e7765df31098f64bc49`。最终运行时代码：`c30b691f2aea081ae3498dcd8c00a1b92254057c`。随后提交仅加入说明和证据，工程源码不变。环境为 Unity **2022.3.62f3c1**、macOS 26.6.2、Apple M5 / Metal / Rosetta。

## 结果与版本

| 验证 | 被测版本 | 结果 | 报告 |
| --- | --- | --- | --- |
| MuseumStart 导航 | c30b691 | 128 项通过，退出 0 | [完整报告](main-navigation.json) |
| DuskWorkshop 导航 | c30b691 | 128 项通过，退出 0 | [完整报告](dusk-navigation.json) |
| 年代、前窗及知识点回归 | c30b691 | 1,131 项通过，0 errors / warnings，退出 0 | [完整报告](era-regression.json) |
| 既有三语输入／生命周期回归 | a34a334 | 138,918 条细粒度检查通过，0 errors / warnings，退出 0 | [摘要](localization-integration-summary.json)、[无损完整 JSON.gz](localization-integration.json.gz) |
| 本地化保存数据 | a34a334 | 11 项通过，303 条目，退出 0 | [报告](localization-static.json) |
| 字体烘焙／持久化 | 首次准备阶段 | 静态图集、所有所需字形可用、身份保持 | [报告](font-bake.json) |

检查条数包括逐标签、逐字形、源码指纹等重复维度，不代表同等数量的独立用户操作。`a34a334` 后唯一的工程修改是 `c30b691` 对导航按钮增加射线专用 hover/select 过滤；三语数据和字体未再改变，最终导航与年代验证另行覆盖该修改。

最终三次 Play 验证开始时的额外 dirty 为尚未提交的 `docs/exhibit-navigation.zh-CN.md`，报告中保留原值。没有额外工程源码修改。活动记录和本目录在测试后整理。每个进程的自然退出结果见 [运行账本](run-ledger.json)。完整日志、开发失败输出和额外截图留在本机 `outputs/navigation-2026-11-20/`，没有覆盖历史报告。

## 覆盖内容

导航验证从保存场景启动，确认自动安装且只有一个三按钮面板；用 0.85、1.2、1.65 米及 1、2 倍缩放夹具检查世界定位、水平朝向、追踪局部姿态、眼高、重复无漂移及无效值拒绝。生产输入部分通过正常 Dynamic Input Actions 排入合成 HMD／左右控制器状态，使用实际 XR 射线与碰撞体，覆盖右手 Grip、左手 Trigger、两键合并、按住不移动、松开一次确认、移开取消。

另查并发请求、实体占位拒绝、地面、过渡时停用及恢复原移动状态、在全黑帧定位、视角重置中止、禁用／重新启用，以及语言、高度偏移、年代、头盔姿态、发现、提示次数和复位计数保持。三语可见标签逐项检查文本、字形与排版边界。

保存场景使用 Device-origin floor offset。合成追踪局部 Y 是 `-0.16144` 米，用于扣除已保存的 `1.36144` 米偏移，最终相机离地确为 **1.2 米**，再由真实的视点高度功能增加 0.1 米。报告保存了计算结果，未把两次身高叠加的画面用于最终验收。相机定位使用公开 API；验证没有手动调用 HoverEnter、SelectEnter、TriggerProgrammatically 或 InputSystem.Update。

首次年代回归在前窗悬停处失败，新导航近手目标与原交互组争夺焦点。增加射线专用过滤后同一验证通过；[失败摘要](era-before-fix-summary.json)保留失败版本、失败断言及后续修复版本。

## 画面与资产核对

以下是从当时追踪相机位置渲染的 **1280×800 单眼诊断截图**，不属于头显画面或可执行程序截图：

| 画面 | 文件 |
| --- | --- |
| 简体 | [navigation-Chinese.png](navigation-Chinese.png) |
| English | [navigation-English.png](navigation-English.png) |
| 繁体 | [navigation-TraditionalChinese.png](navigation-TraditionalChinese.png) |
| 黑场覆盖；整张图每像素 RGB 均 ≤ 2 | [transition-black.png](transition-black.png) |
| 黄昏工房独立场景 | [dusk-navigation-English.png](dusk-navigation-English.png) |

自然退出后 [1,462 个工程文件的 SHA-256 零差](source-integrity.json)；新建的 10 份 meta 无 GUID 冲突。[111 个 LFS 路径](lfs-workspace-check.json)均为已经展开且 SHA-256 匹配的真实资产。没有更改场景、预制件、公共输入、包或工程设置。

静态字体由 649 增至 652 字符，只补入“台、央、往”。原 TTF SHA-256 `616452f955aaa36794b26d5db013a4f9dff529c7df4316bc0d6f0ea28a6a963f`；原字体 GUID 及字体／图集／材质 local file ID 保持；4096²、110 sampling、6 padding、SDFAA，保存图集与烘焙缓冲一致。只提交保存的 SDF 更新，备份和烘焙临时文件不进入仓库。

## 复跑

先退出其他 Unity 实例，使用正确版本及已展开的 LFS 资产。`Unity` 替换为编辑器可执行文件路径，输出使用新的绝对路径：

```sh
Unity -batchmode -force-metal -projectPath MuseumVR \
  -executeMethod Museum.Navigation.Editor.GalleryNavigationValidation.RunBatch \
  -navigationOutput /新的绝对输出目录/main -logFile /绝对路径/main.log

Unity -batchmode -force-metal -projectPath MuseumVR \
  -executeMethod Museum.Navigation.Editor.GalleryNavigationValidation.RunBatch \
  -navigationScene Assets/Museum/Features/DuskWorkshop/DuskWorkshop.unity \
  -navigationOutput /新的绝对输出目录/dusk -logFile /绝对路径/dusk.log

MUSEUM_QA_OUTPUT=/新的绝对输出目录/era Unity -batchmode -force-metal -projectPath MuseumVR \
  -executeMethod Museum.Localization.EditorTools.VisitorEraValidation.RunBatch \
  -logFile /绝对路径/era.log

Unity -batchmode -force-metal -projectPath MuseumVR \
  -executeMethod Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch \
  -logFile /绝对路径/localization-integration.log

Unity -batchmode -force-metal -projectPath MuseumVR \
  -executeMethod Museum.Localization.EditorTools.LocalizationValidation.RunBatch \
  -logFile /绝对路径/localization-static.log
```

Play 验证自己控制退出，不加 `-quit` 或 `-nographics`。原三语验证按原有约定写入新的 `artifacts/localization/qa/runs/`；本轮把生成目录移到本机输出目录后选择本目录内的必要证据。不要运行原型或场景重建工具来复测。

**未验证：** 真实头显／控制器、双眼可读性、触达与舒适度、性能、Windows Player 构建和用户研究。URP 遮罩包含立体实例化宏，但本轮只验证单眼渲染；仍需组员在设备上验收。自动初检不等于另一位成员批准合并。
