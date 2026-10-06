# 2026-10-04 中英语言切换模块

用户要求实现界面**中英语言切换**。选它的直接依据：

- **提案里用户旅程的第一步就是它**：`Welcome and language choice → 选择练习 → 物件探索 → 细节解释 → 反思退出 → 复位`。缺了它，Phase 2 要求的「一条完整可测试的旅程」开头就是断的。
- **需求要求不依赖音频或颜色获取核心信息**，方案里对应「双语文字、状态标注、可调字号」。
- **虚拟客户在 Scenario B 的 A10 里明确提问**：「语言支持我们还没想清楚，想听听你们认为什么是现实的」—— 做出来就是正面回答。

本次交付为**独立模块** `MuseumVR/Assets/Museum/Features/Localization/`，分支 `codex/localization-2026-10-04`，基于 `origin/main = 8e75e71`。

## 交付内容

| 关注点 | 放在哪里 | 换文案时要改代码吗 |
| --- | --- | --- |
| 一条界面文字的中英内容 | `LocalizationTable` 资产 | 不用，加一行即可 |
| 当前是哪种语言 | `LanguageChannel` 资产 | 不用 |
| 某个文字对象显示哪一条 | `LocalizedText` 组件的 key | 不用 |
| 哪些字被烤进字体图集 | `Fonts/characters-required.txt` | 改了文案要重跑脚本文档里的两步 |

```text
Assets/Museum/Features/Localization/
  Runtime/  MuseumLanguage.cs            语言枚举（数值固定，新语言只在末尾追加）
            LocalizationTable.cs         文案表：key → 中文 / English / 备注 + 显示字体
            LanguageChannel.cs           语言通道：当前语言 + 变化事件
            LocalizedText.cs             挂在文字对象上，绑 key 自动刷新
            LanguageButton.cs            可抓取的「中文 / English」按钮
            LanguagePlayModePreview.cs   无头显键盘预览
  Editor/   LocalizationSetup.cs         一键生成资产 / 展示 Prefab / 演示场景；批处理入口
            LocalizationValidation.cs    自动校验 + JSON 报告
  Tables/ Prefabs/ Scenes/ Materials/    由 Editor 生成
  Fonts/    中文字体、子集脚本、字符清单、README（含生成步骤与许可）
```

**隔离性**：模块**刻意不修改 `MuseumStart.unity`**。展示 Prefab 与演示场景都不引用主场景任何对象；
接入主场景由整合人执行。完成后 `MuseumStart.unity` 无改动。

## 三条设计决定

1. **一个文字对象 = 一个 key**，而不是「一个面板 = 一串文字」。展项会增删、面板会在不同展品间复用，逐对象绑定改动最小、冲突最少。
2. **语言状态放在资产里、用事件广播**。文字对象只订阅、按钮只调用 `SetLanguage`；加面板、加语言都不需要改已有代码。
3. **找不到 key 时显示红色 `[[key]]` 标记，不显示空串**。静默返回空会让「漏了一条翻译」看起来像「设计上就没有这段字」，排查成本极高。

## 中文字体：本环境的硬骨头（必须记录）

项目原有字体只有 `LiberationSans`（纯英文），**没有任何中文字形** —— 直接放中文会显示成方块。
系统上可用的中文字体里只有 **Noto Sans SC** 是 **SIL OFL 1.1**（可自由再分发），
原始为 **16.95 MB 的变量字体**，仓库的 `.gitattributes` 也未把 `.ttf` 纳入 LFS。

因此先用 fontTools 从 `wght` 轴取出静态 Regular 实例并子集化到 ASCII + GB2312 全集（**7545 个码位，2.32 MB**），
只提交子集结果。源码、字符清单与再生成步骤都在 `Fonts/` 下。

**随后遇到本环境 TMP 的连续故障，逐个排查如下：**

| 尝试 | 结果 |
| --- | --- |
| 脚本 `TMP_FontAsset.CreateFontAsset` 单参数重载 | 默认 1024 图集 + 采样 90；汉字一个约 108×108 像素放不下，TMP 另开第二个图集而材质仍指第一张 → **字形渲染成实心矩形** |
| 脚本 8 参数重载（Dynamic，2048 图集） | **图集恒为 0×0**，一个字形都烤不进去 |
| 脚本 8 参数重载（Static）+ `TryAddCharacters` | 同样 0×0；**连拉丁字母都失败**（缺字表里出现 `AcesiblMumExprn`） |
| 换成系统**原始**变量字体作源 | 依然 0×0 —— 排除「子集化/实例化把字体弄坏了」 |
| 字体体检（fontTools：`OS/2.fsType` 嵌入许可位、必需表齐全性、glyf 空轮廓数） | 子集字体与原始字体 **fsType = 0（完全允许嵌入）**、必需表齐全、与能正常显示的 LiberationSans 同类 → **排除字体文件本身** |
| Font Asset Creator，Character Set = `Unicode Range (Hex)` | **抛 `FormatException`**，点 Generate 毫无反应 |
| Font Asset Creator，Character Set = **`Characters from File`** | ✅ **成功：192/193 个字符**，图集 2048×2048，资产约 8 MB |

**结论**：脚本路径与本环境的 `Unicode Range (Hex)` 解析都不可用；
**唯一可行做法是 Font Asset Creator + `Characters from File`**。
字符清单由 `Fonts/make-charlist.py` 从本模块的种子文案自动收集（约 193 个字符）。
步骤、参数与许可说明已写入 `Fonts/README.md` 与 `docs/localization.zh-CN.md`。

## 验证

**已做到（编辑器内）**：菜单生成资产与演示场景 → 打开 `Scenes/LanguageDemo.unity` → Play →
按 `1` / `2` / `L` 切换语言，**中英文字都清晰可读、切换即时生效**。
这是**单眼渲染 + 键盘输入的编辑器内检查，不是头显实测**。

**字体资产已核对过文件内容**（不是只看界面）：

| 项 | 实测 |
| --- | --- |
| `m_PointSize` / `m_AtlasPadding` | 110 / 6 |
| 图集 | 1 张 2048×2048 |
| 已烤字形 | 190 个 |
| 文案用到的汉字（含「随」「切」「换」） | 全部已烤入 ✅ |
| 图集是否为真正的距离场 | 抽样检查像素分布，边缘有平滑过渡 ✅ |

**未完成**：真实头显上的中文可读性、字号是否合适、射线交互本身；
未做无障碍或参与者评价；未在 PQ604A/B/C 环境构建。
文案为**起步内容**（旅程步骤与无障碍说明），不是最终展陈文案。

## 这一轮踩过的坑（都写进了代码注释与 README，避免重犯）

1. **3D TMP 的 `fontSize` 单位是"点"，不是世界单位**：物体缩放为 1 时世界高度 ≈ `fontSize × 0.1`。
   第一版按世界单位直接写 0.22，实际只有 0.022 米高 —— 文字小到被误判成"没渲染"。
2. **中文 SDF 采样字号不能太小**：用 60 时汉字笔画只有 1~2 像素宽，距离场无法形成实心内部，
   渲染出来又细又断。**改用 110 + padding 6**（中文官方建议采样 90~120）。
3. **不要在本环境用脚本创建 TMP 字体资产**：Dynamic 会得到 0×0 图集、Static 也烤不进字；
   Font Asset Creator 的 `Unicode Range (Hex)` 会抛 `FormatException`。
   **唯一可行路径是 Creator + `Characters from File`**。
4. **Play 模式下执行 Creator 与场景生成会被 Unity 拒绝**（`InvalidOperationException`），
   产物是半成品而界面上不明显报错。生成工具已加护栏，处于 Play 模式直接拒绝执行并提示。
5. **改文案必须同步重跑字符清单**：漏了这一步就会出现缺字方块（实际发生过一次）。
   `LocalizationValidation` 的缺字检查能抓住这种情况。

## 已知限制

- **字体资产需要手工生成一次**（原因见上），这是本环境 TMP 的缺陷，不是设计选择。
- 文案表只覆盖旅程开头；真实内容应在试点评价后替换。
- 每个文字对象是**一个 key**，未做复数/占位符替换等高级本地化。
- 中英文档位不同：为英文调好的字号在中文下可能偏小或换行变化，尚未做逐语言字号微调。
- **本模块尚未接入 `MuseumStart.unity`**，按组规由整合人执行，步骤见模块说明。

## 接入主场景（由整合人执行）

1. 给需要双语的文字对象加 `LocalizedText`，填文案表、语言通道与 key；
2. 在欢迎步骤放两个 `LanguageButton`（或一个 `Toggle` 型按钮）；
3. 确认文字对象的字体已被设成中文字体（`LocalizedText` 会自动套用表里指定的字体）；
4. 复跑主场景既有验收，确认没有回归。
