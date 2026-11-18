# 可读性下限（Readability floor）

本页记录一条**排版约束**及其依据，并把 [无障碍与舒适性走查](accessibility.zh-CN.md) 中
"自动缩放下限形同虚设"这条发现落实为可核查的改动。

## 1. 问题

Unity 的 TextMeshPro 开启自动缩放（`m_enableAutoSizing`）后，文案放不下时会把字号压小，
下限由 `m_fontSizeMin` 决定。本工程大量组件把它设成 `0.02`，等于**允许把字压到几乎看不见**。

2026-11-17 在 `main@9e9f989` 上扫描 `MuseumVR/Assets/Museum/` 下的场景和 Prefab：
**34 个**组件的 `m_fontSizeMin ≤ 0.05`（其中 32 个是 `0.02`），
最坏压缩比 **72.5×**（`HelmetInspectionDemo` 的 `Title`，最大字号 `1.45`，最小字号 `0.02`）。
一旦某条文案超长（例如繁体或英文整句），字号可以被压到初始值的 1/40 以下。

## 2. 规则

> **`m_fontSizeMin := max(原值, 0.35 × m_fontSizeMax)`**

在应用该规则的场景中，自动缩放最小可到设计字号的 **35%**，压缩比上限为 `1 / 0.35 ≈ 2.86×`。

### 为什么用"相对自身设计尺寸"而不是绝对字号

场景里的 TMP 组件处在不同的缩放空间：同一个 `0.02` 在不同父级 `localScale` 下对应的物理尺寸完全不同。
要用绝对门槛（例如"不小于 6 mm"），必须先把每个组件的累乘 scale 算清并统一单位；
而"相对自身 `m_fontSizeMax`"与单位无关，可以直接比较、逐条复核，也不会因为将来有人调整面板尺寸而失效。

### 为什么是 35%

- 不是通用无障碍标准，是本项目的**设计参数**（同 `exhibit-information.zh-CN.md` 对距离与字号的处理口径）。
- 35% 限制了两个开发场景的最大压缩幅度，并给长文案保留自动缩小的空间。
- `--ratio` 越高，最小字号越大，文字可缩小的空间越少。若发现裁切，优先扩大文本框、调整布局、
  缩短文案或分页，再用简体、繁体、英文复核。若文字读起来仍偏小，可试验提高比例，并重新检查布局。
  试验较低的比例时应从调整前的资产重新试算，因为脚本只会提高原有下限。

## 3. 本次改动范围

| 文件 | 改动处数 | 压缩比（改前 → 改后） |
| --- | --- | --- |
| `HelmetInspection/Scenes/HelmetInspectionDemo.unity` | 8 | 15.0–72.5× → 2.86× |
| `PhysicalHands/Scenes/PhysicalHandsLab.unity` | 18 | 4.5–42.5× → 2.86× |
| **合计** | **26** | 最坏 **72.5× → 2.86×** |

- **只改 `m_fontSizeMin` 一个字段**，26 行 diff，未动 `m_fontSize`、`m_fontSizeMax`、文案、Prefab、字体或任何组件引用。
- 两个目标场景中，`m_fontSizeMin ≤ 0.05` 的组件由 **21 个降到 0 个**。
- 产品资产扫描范围内，该数量由 **34 个降到 13 个**，最坏压缩比由 **72.5× 降到 42.5×**。

### 未覆盖：主场景

剩下的 **13 处全部在 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`**
（`SHALLOWER label`、`TILT DOWN label`、`TURN RIGHT label`、`EDIT CUT label` 等移动提示标签）。

主场景是共享文件，需按 `AGENTS.md` 在 Issue 协调单一写入者。可先把该场景加入脚本的 `TARGETS` 列表试算，
再结合主场景的实际布局和三语文案确认调整范围。

## 4. 怎么复算

```bash
# 1) 干跑：只打印将要修改的字段（不写文件）
python artifacts/accessibility/qa/runs/20261117-readability-floor/fix_readability_floor.py

# 2) 实际写入
python artifacts/accessibility/qa/runs/20261117-readability-floor/fix_readability_floor.py --apply

# 3) 用同一套审计脚本核对结果
python artifacts/accessibility/qa/runs/20261010-accessibility-audit/audit_accessibility.py --out readability-recheck/report.json
```

- 脚本仅依赖 Python 标准库。默认干跑并输出变更明细，`--apply` 才会写入场景的 `m_fontSizeMin`。
- 改前/改后对照见 `readability-floor-report.json`。`productAssets` 和 `targetScenes` 分别记录产品资产扫描范围
  和两个目标场景的统计，`changes` 列出每处改动。
- 审计脚本输出的 `report.json` 可用来看全局：`自动缩放下限低于 0.05 的文本组件` 与
  `自动缩放压缩比最大的几个文本组件` 两项。
- 建议把复算产物保存在新的运行目录，以保留原始证据。`--apply` 会在修正脚本所在目录写入明细，
  重跑写入操作前可先把脚本复制到新的运行目录，并用 `--repo` 指定要验证的工程副本。

## 5. 未验证

- **未在 Unity 里目视检查**：本轮不运行编辑器，因此**没有确认改后文案是否出现裁切或溢出**。
  需要有人打开 `HelmetInspectionDemo` 与 `PhysicalHandsLab`（或进入对应流程）逐屏确认。
- 未验证真实头显下的可读性、观看距离或物理字号。
- 未验证繁体/英文最长文案在该下限下的实际排版表现。

## 6. 与评价的关系

本条对应评价方案中的 **EQ1（坐姿、实际观看距离下能否读懂）**。评价方案在
`docs/evaluation/README.md`，由 PR #78 引入；该 PR 合入前此路径在 `main` 上尚不存在，
因此这里只做文字引用、不给链接。

- **改前**：最坏可压到 `1.45 → 0.02`，理论上存在"文字几乎消失"的可能。
- **改后**：这处 `Title` 的最小字号提高到 `0.5075`，两个目标场景的压缩比封顶约 2.86×。

这属于**资产层的确定性改动**，不是用户证据。真实观感与"是否仍然读不清"必须由评价里的真人会话回答；
若评价显示仍有文字读不清，可按具体原因调整字号与布局，例如使用固定字号配合分页或滚动。
