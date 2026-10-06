# 新功能的文字怎么写

本说明适用于 MuseumStart 里的访客文字。新增功能时，把三语文案和交互代码一起交接，避免新面板又出现只能显示英文的情况。现有入口、展厅标识、观察菜单、剖面工具、帮助、供气／通话字幕和回顾共用一个语言通道。

## 先改文案表，再接文字

唯一维护源是 `MuseumVR/Assets/Museum/Features/Localization/Tables/MuseumTranslations.json`。每条填写稳定的 `key`、`english`、`chinese`、`traditionalChinese` 和用途 `note`。新条目建议用 `inspection.action.xxx` 等有意义的 key；现有 `text.<hash>` 是这一轮整理旧英文时生成的标识，保留即可。

JSON 更新后，在退出 Play 的编辑状态，把保存的 LocalizationTable.asset 同步更新；可以在 Editor 脚本中调用 `TrilingualMuseumSetup.ApplyTable(table, table.FontAsset)` 并明确保存，或在 Inspector 同步修改相应条目。不要把更新文案变成重建整个展厅的理由。未来若修改保存资产，应先核对共享文件认领和本地改动。

同一英文原文不能有两种不同的译文：动态文字根据完整英文查表。空格、换行、标点和 TMP 标签都是原文的一部分。改英文时，同时改代码中的原文和三语条目；只改代码会导致查不到译文、回退英文。对同一原文有不同翻译需求时，改用独立 key 的 LocalizedText，不依赖英文查找。

## 静态标识

给 TMP 文字对象添加 `LocalizedText`，指定保存的 LocalizationTable、LanguageChannel 和 key，启用表中字体。标题、墙面标识等适合这种方式。不要同时给同一对象添加 LocalizedSourceText，也不要让另一个脚本每帧覆盖它的 text。

## 动态字幕、状态和按钮

在主场景运行期间，使用明确的文字写入接口：

```csharp
using Museum.Localization;
MuseumText.Set(caption, "Air from the surface");
```

它给对象绑定 LocalizedSourceText，保留英文来源，在语言切换后重取译文。下一帧继续写同一句英文也不会把中文盖回去。不要直接写 `caption.text = ...`，不要拿已经翻译的 `caption.text` 再次当英文来源，也不要用字符串替换去拼出另一种语言。

MuseumStart 的 Language Selection 上有 MuseumLocalizationRoot，负责在动态组件运行前提供表和通道。独立测试场景若需要动态三语，应配置同一根组件与引用；仅打开不带根组件的旧独立演示，并不能证明主场景的三语接线有效。

当前接口没有复数、可变数值占位符或复杂语序格式化系统。少量固定状态应各自登记完整句子，包括组合状态、动画各阶段和换行。今后需要任意数值时，应单独设计并测试三语格式化接口，不要假设当前查表支持 `"Progress: " + value`。

## 字体和布局

1. 执行 `python MuseumVR/Assets/Museum/Features/Localization/Fonts/make-charlist.py`。它读取 JSON 中三种语言，只生成字符清单，不改字体。
2. 检查保存的 TMP 静态图集是否已有新增字形；TTF 有某字不等于图集已烤入。缺字时做明确的字体更新任务，保留原 `.meta`、GUID 和子资源引用；操作方法见 Fonts/README.md。如果源 TTF 子集也没有该字，要先从许可明确的完整字体扩展源子集，再烤图集；当前 7651 个码位不代表所有繁体字都已支持。
3. 三种语言分别看长标题、按钮、帮助正文和动画字幕。优先调整换行、文字长度或容器；自动字号要有可读的下限，不能用无限缩小来掩盖溢出。当前按钮下限为 .28，最大 .4，属于世界空间 TMP 的工程数值，不是头显可读性的实测结论。
4. 正文高度根据翻译后的内容计算。修改页面内容后要检查下面的按钮是否被挤到面板外，以及面板跟随和避障是否仍正常。

## 提交前检查

- 在保存的 MuseumStart 里，用三个实体语言按钮检查射线选择。无头显可先用键盘 F6／F7／F8；L 按英文→简体→繁体循环。数字键 1／2 在主场景已禁用，避免与模拟器冲突。
- 启动功能后再切语言，翻页、重播、停止、复位、重新参观和隐藏后再开启都要保留当前语言。切语言不应清除发现记录、重启动画或开启原本隐藏的对象。
- 运行 LocalizationValidation 与 LocalizationIntegrationValidation，并运行该功能自己的回归验收。三语测试会检查未登记文字、译文、字形和可见标签的溢出；新增状态也应补进测试流程。
- 交接写明新增 key、动态状态、修改的资产与脚本、测试结果，以及尚未做的头显或用户测试。保留中英事实边界，不能因翻译而把示意结构写成历史实测结构。

测试入口和限制见 [三语模块说明](localization.zh-CN.md)。Unity 官方 XR 模拟器的开发叠层、编辑器菜单及尚未接入主场景的其他开发演示不属于展厅访客文案；不要修改 package 来翻译开发叠层。
