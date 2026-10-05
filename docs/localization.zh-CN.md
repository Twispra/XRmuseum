# 中英语言切换（Localization）

面向 COMP5424 Scenario B「无障碍博物馆体验」的**界面语言切换**能力。
对应提案里用户旅程的**第一步**（`Welcome and language choice`），
以及需求「不依赖音频或颜色获取核心信息」中的**双语文字**要求。

## 这个模块解决什么

展馆需要同时服务中文与英文访客，而项目原有的字体只有 `LiberationSans`（纯英文），
直接把中文放进去会显示成方块。本模块提供两件事：

1. **一份双语文案表**（key → 中文 / English），文案与摆放解耦；
2. **一套切换链路**：任何文字对象挂上 `LocalizedText` 并填一个 key，
   语言一变就自动刷新，不需要写代码、不需要重新摆位。

## 快速开始

1. 打开 Unity，菜单 **Museum → Localization → Create Language Assets and Showcase**。
2. 再执行 **Museum → Localization → Create Demo Scene**，
   生成 `Scenes/LanguageDemo.unity` —— **打开它直接按 ▶ 就能看效果**。
3. 进入 Play 后：按 **`1`** 切中文、**`2`** 切英文、**`L`** 循环。
   （没有头显也能验证，见下方「无头显怎么验证」。）
4. 可选：**Museum → Localization → Validate Localization System** 跑自动校验。

## 目录结构

```text
Assets/Museum/Features/Localization/
  Runtime/
    MuseumLanguage.cs            语言枚举（数值固定，新语言只在末尾追加）
    LocalizationTable.cs         文案表资产：key → 中文 / English / 备注 + 显示用字体
    LanguageChannel.cs           语言通道资产：当前语言 + 变化事件
    LocalizedText.cs             挂在文字对象上，绑 key 自动刷新
    LanguageButton.cs            可抓取的「中文 / English」按钮
    LanguagePlayModePreview.cs   无头显键盘预览
  Editor/
    LocalizationSetup.cs         一键生成资产 / 展示 Prefab / 演示场景；批处理入口
    LocalizationValidation.cs    自动校验 + JSON 报告
  Tables/     语言通道与文案表（由菜单生成）
  Prefabs/    LanguageShowcase.prefab（由菜单生成）
  Scenes/     LanguageDemo.unity（由菜单生成，打开即可播放）
  Materials/  Board / Button / ButtonPressed（由菜单生成）
  Fonts/      中文字体、子集脚本、字符清单、README
```

## 三条设计决定

**1. 一个文字对象 = 一个 key。** 不把「一个面板 = 一串文字」做成整体，
因为展项会增删、面板会在不同展品之间复用；逐对象绑定改动最小、冲突最少。

**2. 语言状态放在资产里，用事件广播。** `LanguageChannel` 是唯一权威来源，
文字对象只订阅、按钮只调用 `SetLanguage`。加面板、加语言都不需要改已有代码。

**3. 找不到 key 时显示红色 `[[key]]` 标记，不显示空串。**
静默返回空会让「漏了一条翻译」看起来像「设计上就没有这段字」，
排查成本极高。标记只用 ASCII 字符，保证任何字体子集里都有字形。

## 字体：为什么必须单独处理，以及怎么生成

项目原有的 `LiberationSans` 没有任何中文字形。本模块使用 **Noto Sans SC**
（SIL Open Font License 1.1，允许自由使用、修改与再分发）。

### ⚠️ 本工程里必须用 Font Asset Creator 手工生成一次

**不要**指望菜单里的自动生成能跑通。在这台机器所用版本（2022.3.62f3c1）下实测：

| 做法 | 结果 |
| --- | --- |
| 脚本 `TMP_FontAsset.CreateFontAsset`（Dynamic） | 图集 0×0，一个字都烤不进去 |
| 脚本 `TMP_FontAsset.CreateFontAsset`（Static）+ `TryAddCharacters` | 同样 0×0，连拉丁字母都失败 |
| Font Asset Creator 里 **Character Set = `Unicode Range (Hex)`** | **抛 `FormatException`**，点 Generate 毫无反应 |
| Font Asset Creator 里 **Character Set = `Characters from File`** | ✅ **成功，192/193 个字符** |

`Unicode Range (Hex)` 的解析在本环境有 bug，**改用「从文件读取字符」即可绕过**。

### 生成步骤（一次即可，之后菜单会复用）

> ⚠️ **必须先退出 Play 模式**（点 ▶ 停止运行）。Unity 在播放状态下会拒绝 TMP 的字体生成
> 与场景保存（`InvalidOperationException: This cannot be used during play mode.`），
> 产物会不完整、界面上却不明显报错 —— 这个坑实际踩过一次（字体资产半成品 + 演示场景没更新）。
> 生成工具里已加护栏：处于 Play 模式时直接拒绝执行并提示。

1. 菜单 **Window → TextMeshPro → Font Asset Creator**
2. **Source Font File**：`Fonts/NotoSansSC-MuseumSubset.ttf`
3. **Sampling Point Size**：`Custom Size` **110** ｜ **Padding**：**6**
   （中文官方建议 Sampling 用 90~120；采样太小时汉字笔画只有 1~2 像素宽，距离场无法形成
   "实心"内部，渲染出来**又细又断**。最初用 60 + padding 5，文字几乎无法辨认。）
4. **Atlas Resolution**：**2048 × 2048** ｜ **Render Mode**：`SDFAA`
5. **Character Set**：**`Characters from File`**
   → 出现 **Character File** 格子后，拖入 `Fonts/characters-required.txt`
6. 点 **Generate Font Atlas** → 预览区应出现汉字
7. 点 **Save as...** → 存到 `Fonts/` 目录，文件名**保持默认**
   `NotoSansSC-MuseumSubset SDF`（**不要自己加 `.asset`**）

生成后的资产约 8 MB、图集 2048×2048、`m_PointSize = 110`、`m_AtlasPadding = 6`、含 192 个字形。
校验工具会检查图集是否有效。

### 文案改了之后

`characters-required.txt` 由 `Fonts/check-glyph-coverage.py` 与
`Fonts/subset-noto-sans-sc.py` 同源的思路维护；实际做法：

```bash
python Fonts/make-charlist.py          # 依据 LocalizationSetup 的种子文案重新收集字符
python Fonts/check-glyph-coverage.py   # 核对字符在字体里都有字形
```

然后按上面的步骤**重新生成一次字体资产**（Character Set 仍选 Characters from File）。
如果只用常用简体字（GB2312 范围内），字体文件本身不需要重新生成。

## 无头显怎么验证

组内有人没有 Quest。搭一套 XR Origin + 模拟器只为看一眼文字切换并不划算，
因此模块自带键盘预览：**`1` 中文 / `2` 英文 / `L` 循环**。
键盘路径与 XR 按钮走的是同一个语言通道，所以它能验证「切换 → 刷新」这条链路；
**它验证不了射线交互本身**，那需要真机。

## 接入主场景（由整合人执行）

本模块**刻意不修改 `MuseumStart.unity`**。接入时：

1. 给需要双语的文字对象加 `LocalizedText`，填文案表、语言通道与 key；
2. 在欢迎步骤放两个 `LanguageButton`（或一个 `Toggle` 型按钮）；
3. 确认这些文字对象的字体已被设成中文字体（`LocalizedText` 会自动套用表里指定的字体）；
4. 复跑主场景既有验收，确认没有回归。

## 已知限制

- **字体资产需要手工生成一次**（原因见上）。这是本环境 TMP 的缺陷，不是设计选择。
- 文案表只覆盖旅程开头与无障碍说明的**起步内容**，不是最终展陈文案；
  真实内容应在试点评价后替换（对应负责人 B 的内容工作包）。
- 每个文字对象是**一个 key**，没有做复数/占位符替换等高级本地化能力。
- 中英文档位不同：英文为主的文字对象在切换成中文后可能偏小或换行位置变化，
  尚未做逐语言的字号微调。文字大小可调属于后续工作。
- 未在真实头显上验证可读性；未做无障碍或参与者评价。
