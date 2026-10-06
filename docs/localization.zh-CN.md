# 展厅三语切换

MuseumStart 的访客文字支持 **简体中文、繁体中文、英文**，默认仍为英文。覆盖入口和展厅标识、展台说明、观察与剖面工具、帮助、提示／回顾，以及供气／通话演示的动态标签和各阶段字幕。数字展品编号、Mark V、输入键名称等保留其名称。

## 直接使用

用 Unity **2022.3.62f3c1** 打开 MuseumVR 的 `Assets/Museum/Scenes/MuseumStart.unity`，进入 Play。语言面板有「简体中文」「繁體中文」「English」三个按钮，沿用现有 XR 射线 Select 输入，模拟器中指向后按 G 并松开。

先点击 Game 画面后，**F6 简体、F7 繁体、F8 英文，L 循环切换**。主场景不使用 1／2 语言快捷键，以免与 XR 模拟器冲突。打开、预览和验收都不需要运行 Setup／Build／Integrate。

语言切换立即刷新文字，不重启动画，不清空发现记录，也不会开启原本隐藏的标识。帮助页按翻译后的正文重新计算高度；隐藏后再开启的标签读取当前语言。

## 维护与结构

详见 [新增功能的文字规范](localization-authoring.zh-CN.md)。三语文案唯一维护源为 `Features/Localization/Tables/MuseumTranslations.json`，目前 193 条；运行时使用保存的 LocalizationTable.asset，不读取 JSON 文件。

| 文件／组件 | 职责 |
| --- | --- |
| MuseumLanguage / LanguageChannel | 保留旧序列值 Chinese=0、English=1，新增 TraditionalChinese=2；广播变化 |
| LocalizedText | 固定标识按 key 翻译 |
| LocalizedSourceText / MuseumText.Set | 动态文字保留完整英文来源后查表，避免每帧被英文覆盖 |
| MuseumLocalizationRoot | 主场景提供同一个表和通道，早于动态文字组件初始化 |
| LanguageButton / LanguagePlayModePreview | 三个 XR 按钮及 F6／F7／F8／L 键盘预览 |
| TrilingualMuseumSetup | 明确的保存资产迁移与布局适配工具；不是日常启动步骤 |
| TrilingualMuseumValidation | 集成验收额外检查三语页面、动态字幕及语言切换后的状态 |

主场景有 70 个保存的英文来源绑定；原有 key 绑定继续保留。运行时新建的字幕通过 MuseumText.Set 接入。没有每帧扫描全场景或改 Unity package。

## 字体

沿用 Noto Sans SC Regular 的 OFL 字体，扩展源子集中的繁体字，原位重烤静态 SDF：**4096×4096、Sampling 110、Padding 6、SDFAA、507 个字符**。当前三语文案所需显示字符清单为 497，图集还保留额外已烤字符。原字体 GUID、图集和材质 localFileID 保留，并核对保存像素与渲染缓冲一致。

Alpha8 原始图集从 2048² 的 4 MiB 增为 4096² 的 16 MiB；这是图集数据量，不是整机 GPU 占用或帧率测量。现有内容需要完整简繁字形，未来优化应以实际设备数据为依据。详见 [字体说明](../MuseumVR/Assets/Museum/Features/Localization/Fonts/README.md) 和 [资源声明](external-resources.md)。

## 验收与边界

模块入口：`Museum.Localization.EditorTools.LocalizationValidation.RunBatch`。
主场景入口：`Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch`，先做保存资产与合成 XR 输入检查，再运行三语页面／动画检查。命令用 `-batchmode -projectPath <MuseumVR> -executeMethod <入口> -logFile <独立日志>`，不要加 -quit 或 -nographics；测试自行控制 Play 和退出，不保存产品资产。

新功能继续运行自己的回归验收。报告位于独立 `artifacts/localization/qa/runs/`；三语诊断截图暂存 `tmp/trilingual-qa/`，有用截图由活动记录单独交接。测试范围包括已登记译文和静态字形、三个语言按钮、帮助与剖面菜单、提示／回顾状态、动态演示各阶段、隐藏后开启、复位、停止和重播。当前场景内可见访客文字检查溢出；自动字号和截图不能证明头显可读性。

Unity 官方 XR 模拟器叠层、编辑器菜单、未接入主场景的年代／旧开发演示不属于本轮访客界面范围。未做真实头显、参与者评价、舒适性或可执行构建验证；没有把自动验收写成人工测试。

本轮仅为本地实现，见 [活动与交接](activity/2026-10-06-trilingual-museum.md)。上一轮入口接线及 198 字符字体是历史版本，见 [原整合记录](activity/2026-10-06-pr14-completion.md)。
