# Fonts — 中文字体来源与生成方式

## 这里的文件是什么

| 文件 | 说明 |
| --- | --- |
| `NotoSansSC-MuseumSubset.ttf` | **提交进仓库的字体**，2.32 MB。由 `subset-noto-sans-sc.py` 生成 |
| `NotoSansSC-MuseumSource.ttf` | 系统原始变量字体（16.95 MB），**不提交**，仅保留作对照与重新生成用 |
| `subset-noto-sans-sc.py` | 从系统字体生成子集字体的脚本，保证可复现 |
| `check-glyph-coverage.py` | 核对源码里用到的每个字符在字体里都有字形 |
| `make-charlist.py` | 从 `LocalizationSetup.cs` 的种子文案收集字符，生成下面的清单 |
| `characters-required.txt` | 需要烤进 TMP 字体资产的字符清单（约 193 个），供 Font Asset Creator 读取 |
| `NotoSansSC-MuseumSubset SDF.asset` | TMP 字体资产，**必须手工生成一次**，见下 |

## 为什么要做子集

项目原有字体只有 `LiberationSans`（纯英文），**没有任何中文字形**，
直接把中文放进场景会显示成方块。

系统上可用的中文字体里，只有 **Noto Sans SC** 是 **SIL Open Font License 1.1**，
允许自由使用、修改与再分发（`msyh.ttc` / `simhei.ttf` / `simsun.ttc` 等均为微软专有，未采用）。

原始字体是**变量字体**（`wght` 轴 100–900）且体积 **16.95 MB**；
仓库的 `.gitattributes` 也没有把 `.ttf` 纳入 LFS。因此先取静态实例、再子集化：

```
NotoSansSC-VF.ttf                    16.95 MB   ← 系统字体
   ↓ 从 wght 轴取出 Regular (400) 静态实例
   ↓ 子集化：ASCII + GB2312 全集 + 常用标点
NotoSansSC-MuseumSubset.ttf           2.32 MB   ← 提交
```

覆盖范围：ASCII、GB2312 全部汉字（7445 个）与符号，共 **7545 个码位**。
不包含 GB18030 扩展区汉字、生僻字、日文假名/韩文。**只要用常用简体字，就不用重新生成字体文件。**

---

## ⚠️ TMP 字体资产必须手工生成一次

**不要指望菜单里的自动生成能跑通。** 在本机所用版本（Unity 2022.3.62f3c1，国内发行版）下实测：

| 做法 | 结果 |
| --- | --- |
| 脚本 `TMP_FontAsset.CreateFontAsset`（Dynamic） | 图集 **0×0**，一个字都烤不进去 |
| 脚本 `TMP_FontAsset.CreateFontAsset`（Static）+ `TryAddCharacters` | 同样 0×0，**连拉丁字母都失败** |
| Font Asset Creator，Character Set = `Unicode Range (Hex)` | **抛 `FormatException`**，点 Generate 毫无反应 |
| Font Asset Creator，Character Set = **`Characters from File`** | ✅ **成功，192/193 个字符** |

`Unicode Range (Hex)` 的解析在本环境有 bug；**改用「从文件读取字符」即可绕过**。

### 生成步骤

> ⚠️ **必须先退出 Play 模式**（点 ▶ 停止运行）。Unity 在播放状态下会拒绝 TMP 的字体生成
> （`InvalidOperationException: This cannot be used during play mode.`），
> 产物会不完整而界面上不会明显报错。

1. 菜单 **Window → TextMeshPro → Font Asset Creator**
2. **Source Font File**：拖入 `NotoSansSC-MuseumSubset.ttf`
3. **Sampling Point Size**：`Custom Size` **110** ｜ **Padding**：**6**
   （中文字体官方建议 Sampling 用 90~120：采样太小的话汉字笔画只有 1~2 像素宽，
   距离场无法形成"实心"内部，渲染出来又细又断。）
4. **Atlas Resolution**：**2048 × 2048** ｜ **Render Mode**：`SDFAA`
5. **Character Set**：选 **`Characters from File`**
   → 出现 **Character File** 格子后，拖入本目录的 `characters-required.txt`
6. 点 **Generate Font Atlas** —— 预览区应出现汉字；窗口下方会显示
   `Characters included: 192/193`（唯一缺的那一个是长破折号 `–`，用不到，可以忽略）
7. 点 **Save as...** → 保存到本目录，文件名**保持默认**
   `NotoSansSC-MuseumSubset SDF` —— **不要自己加 `.asset` 后缀**

生成后：约 8 MB、图集 2048×2048、`m_PointSize = 110`、`m_AtlasPadding = 6`、含 192 个字形。
之后菜单 **Museum → Localization → Create Language Assets and Showcase** 会**复用**它。

### 文案改了之后

1. 跑一次 `make-charlist.py` 更新 `characters-required.txt`；
2. 跑一次 `check-glyph-coverage.py` 确认字符都有字形（缺字会在 Unity 里显示成方块）；
3. 按上面的步骤**重新生成字体资产**（Character Set 仍选 `Characters from File`）。

## 许可与致谢

- **Noto Sans SC**，作者 Google，许可 **SIL Open Font License 1.1**（允许商用、修改与再分发）。
- 本仓库内的副本是**修改过的版本**：已实例化为静态 Regular 并子集化。
  该字体没有 Reserved Font Name，OFL 允许如此处理。
- 来源与修改记录同时登记在 `docs/external-resources.md`。
