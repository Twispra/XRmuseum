# 中英语言切换（Localization）

本模块提供双语文案表、语言事件通道、LocalizedText 和语言按钮，对应访客进入展厅时的语言选择。当前覆盖入口标识和语言面板；其余展陈、探索说明尚未全部翻译，不能称为全馆双语。

## 使用已保存的成果

1. 用 Unity **2022.3.62f3c1** 打开 MuseumVR，直接打开 `Assets/Museum/Scenes/MuseumStart.unity`。
2. 进入 Play，默认英文；在语言面板选择「中文」或「English」。按钮使用 XRI 射线与选择输入；键盘预览也可用 `1` / `2` / `L`。
3. 独立演示位于 `Assets/Museum/Features/Localization/Scenes/LanguageDemo.unity`，可直接打开运行。
4. 日常打开、预览、验收均**不需要 Create/Setup/Integrate**；这些是明确修改任务的工具，不是修复工程按钮。

## 保存场景里的绑定

| 对象 | key | 英文 | 中文 | 显示规则 |
| --- | --- | --- | --- | --- |
| Gallery masthead | `museum.sign.masthead` | Beneath the surface | 海面之下 | 平时可见，探索演示期间隐藏，结束后恢复 |
| Arrival brand | `museum.sign.arrival_brand` | The diving gallery | 潜水展厅 | 入口可见 |
| Gallery subtitle | `museum.sign.gallery` | THE DIVING GALLERY | 潜水展厅 | 保持主线默认隐藏；将来启用时读取当前语言 |
| Arrival introduction | `museum.sign.intro` | One object. Many ways to explore. | 一件展品，多种看法。 | 保持主线默认隐藏；将来启用时读取当前语言 |

两处展厅名称使用不同 key，保留原英文大小写。Museum Title、Museum Subtitle、Welcome 是隐藏旧标识，已移除它们的旧本地化绑定，不重新打开 Renderer。入口文案、位置和显隐遵循 PR #9；双语标识使用同一保存的 Noto Sans SC 字体。

现有 Language Selection 位于 `(2.6, 0, 6.2)`，本轮保留位置与 Prefab 实例。默认语言仍为 English。语言切换不会启用被展示流程隐藏的对象；对象重新启用时 LocalizedText.OnEnable 会应用最新语言。

## 模块结构与扩展

- `Runtime/LocalizationTable.cs`：key 对应中英文本；缺 key 返回红色标记。
- `Runtime/LanguageChannel.cs`：当前语言及变化事件，默认状态在每次 Play 初始化。
- `Runtime/LocalizedText.cs`：每个 TMP 文字对象绑定一个 key，并应用表中指定的双语字体。
- `Runtime/LanguageButton.cs`：保存的 XRI 选择事件调用语言通道。
- `Editor/LocalizationValidation.cs`：已保存表、字体、展示 Prefab 校验。
- `Editor/LocalizationIntegrationValidation.cs`：已保存主场景接线、模拟 XR 输入、显隐生命周期与固定视角截图校验。
- `Editor/MuseumIntegration.cs`：仅在已授权修改主场景时接入或移除本工具负责的绑定。
- `Editor/LocalizationSetup.cs`：完整资产/演示生成工具；正常使用不运行。

新增翻译时，给目标对象单独加 LocalizedText，提供表、通道和 key；同步保存表与 Setup 种子，英文保留现有展陈原文，核对新字形。共享场景必须先在 Issue 协调写入者。没有复数或占位符替换功能。

## 字体与字符覆盖

提交的源字体为 **Noto Sans SC Regular 子集**，SIL OFL 1.1；来源、子集范围与许可见 [Fonts README](../MuseumVR/Assets/Museum/Features/Localization/Fonts/README.md) 和 [资源记录](external-resources.md)。本轮没有下载新的字体或重建 .meta。

保存的静态 SDF 图集为 **2048×2048、Sampling 110、Padding 6、SDFAA、198 个字符**。补齐「海、之、潜、水、厅、多、种、法」时采用 TMP Font Asset Creator 使用的底层 FontEngine 打包/渲染流程，原位更新图集，保留字体、图集、材质的 GUID/localFileID，并检查持久化像素和字符覆盖。

早期高层 CreateFontAsset/TryAddCharacters 曾产生 0×0 图集；这段历史不表示成员现在必须重新生成字体。使用已保存的资产即可。以后改文案时运行 Fonts/make-charlist.py（现在根据脚本自身位置定位），再明确重烤并验证新字形。不能只更新字符列表或内存字形表。

## 校验入口

菜单 `Museum → Localization → Validate Localization System` 检查保存的模块资产；批处理入口为 `Museum.Localization.EditorTools.LocalizationValidation.RunBatch`。

菜单 `Museum → Localization → Validate saved main scene (Play Mode)` 检查保存主场景；批处理入口为 `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch`，使用 `-batchmode -projectPath <MuseumVR> -executeMethod <入口> -logFile <独立日志>`，不要加 -quit 或 -nographics。它控制 Play 与退出，并写独立 `artifacts/localization/qa/runs/` 目录。

场景校验使用生产 Input Actions 和射线交互器，输入为合成控制器姿态及握持键状态；演示开始/停止单独标记为公共 API 调用。隐藏标签仅在明确的临时 Play 夹具中启用后恢复，不保存。报告核对资产哈希并输出同视角画面。这些都不是物理头显/控制器验收。

## 接入工具的边界

`Integrate Into MuseumStart` / `MuseumIntegration.IntegrateBatch` 在修改前确认保存字体与四组文案、目标唯一性、当前英文和显隐。缺资产、缺字形、对象不符或存在陌生绑定时中止，不调用全量生成器。已有语言面板位置不动；只清理精确名称和旧 key 都吻合的三个旧绑定，然后保存场景。备份位于 Unity `Application.temporaryCachePath/MuseumLocalizationIntegration/` 的唯一文件。

工具拒绝 Play 模式和未保存场景。Remove From MuseumStart 只移除语言面板与精确名称/key 匹配的本工具绑定，保留其他绑定；它不是场景完整回滚，也不会还原字体样式，需要完整回退时使用本轮备份或 Git 中已审查版本。

## 验证限制

本轮范围是入口语言接线和保存字形，不新增学习内容或改变项目目标。没有真实头显可读性、舒适性、可访问性/参与者评价、全馆翻译或当前可执行构建结论。英文和中文共用布局参数，后续字号及换行优化应依据实际设备检查。版本、运行结果和可复查截图见 [2026-10-06 整合记录](activity/2026-10-06-pr14-completion.md)。
