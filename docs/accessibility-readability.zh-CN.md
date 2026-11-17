# 可读性下限（Readability floor）

本页记录一条**排版约束**及其依据，并把 [无障碍与舒适性走查](accessibility.zh-CN.md) 中
"自动缩放下限形同虚设"这条发现落实为可核查的改动。

## 1. 问题

Unity 的 TextMeshPro 开启自动缩放（`m_enableAutoSizing`）后，文案放不下时会把字号压小，
下限由 `m_fontSizeMin` 决定。本工程大量组件把它设成 `0.02`，等于**允许把字压到几乎看不见**。

2026-11-17 在 `main@9e9f989` 上复测：**34 个**组件的 `m_fontSizeMin ≤ 0.05`（其中 32 个是 `0.02`），
最坏压缩比 **72.5×**（`HelmetInspectionDemo` 的 `Title`：`0.02 → 1.45`）。
一旦某条文案超长（例如繁体或英文整句），字号可以被压到初始值的 1/40 以下。

## 2. 规则

> **`m_fontSizeMin := max(原值, 0.35 × m_fontSizeMax)`**

即**自动缩放最多把字压到设计尺寸的 35%**，最坏压缩比收敛到 `1 / 0.35 ≈ 2.86×`。

### 为什么用"相对自身设计尺寸"而不是绝对字号

场景里的 TMP 组件处在不同的缩放空间：同一个 `0.02` 在不同父级 `localScale` 下对应的物理尺寸完全不同。
要用绝对门槛（例如"不小于 6 mm"），必须先把每个组件的累乘 scale 算清并统一单位；
而"相对自身 `m_fontSizeMax`"与单位无关，可以直接比较、逐条复核，也不会因为将来有人调整面板尺寸而失效。

### 为什么是 35%

- 不是通用无障碍标准，是本项目的**设计参数**（同 `exhibit-information.zh-CN.md` 对距离与字号的处理口径）。
- 35% 是保守取法：它把 72.5× 这种明显失控的情形收敛掉，同时给长文案留出压缩余地，
  避免直接卡到"一个字都放不下"而大面积裁切。
- 该比例是**可调参数**（脚本 `--ratio`），若在编辑器里发现仍有裁切，可上调；若发现压得不够，可下调。

## 3. 本次改动范围

| 文件 | 改动处数 | 压缩比（改前 → 改后） |
| --- | --- | --- |
| `HelmetInspection/Scenes/HelmetInspectionDemo.unity` | 8 | 15.0–72.5× → 2.86× |
| `PhysicalHands/Scenes/PhysicalHandsLab.unity` | 18 | 4.5–42.5× → 2.86× |
| **合计** | **26** | 最坏 **72.5× → 2.86×** |

- **只改 `m_fontSizeMin` 一个字段**，26 行 diff，未动 `m_fontSize`、`m_fontSizeMax`、文案、Prefab、字体或任何组件引用。
- 复测结果：`m_fontSizeMin ≤ 0.05` 的组件从 **34 个降到 13 个**。

### 未覆盖：主场景

剩下的 **13 处全部在 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`**
（`SHALLOWER label`、`TILT DOWN label`、`TURN RIGHT label`、`EDIT CUT label` 等移动提示标签）。

主场景是共享文件，`AGENTS.md` 规定场景/Prefab/字体需按 Issue 协调单一写入者，
因此本轮**有意不动**它。协调后处理方式与上面完全相同：同一条规则、同一个脚本，
只需把该文件加进 `TARGETS` 列表。

## 4. 怎么复算

```bash
# 1) 干跑：只打印将要修改的字段（不写文件）
python artifacts/accessibility/qa/runs/20261117-readability-floor/fix_readability_floor.py

# 2) 实际写入
python artifacts/accessibility/qa/runs/20261117-readability-floor/fix_readability_floor.py --apply

# 3) 用同一套审计脚本核对结果
python artifacts/accessibility/qa/runs/20261010-accessibility-audit/audit_accessibility.py
```

- 脚本纯标准库、只读/只改 `m_fontSizeMin` 一行，默认干跑。
- 改前/改后对照见 `readability-floor-report.json`（含每一处组件的名字、改前改后值与压缩比）。
- 审计脚本输出的 `report.json` 可用来看全局：`自动缩放下限低于 0.05 的文本组件` 与
  `自动缩放压缩比最大的几个文本组件` 两项。

## 5. 未验证

- **未在 Unity 里目视检查**：本轮不运行编辑器，因此**没有确认改后文案是否出现裁切或溢出**。
  需要有人打开 `HelmetInspectionDemo` 与 `PhysicalHandsLab`（或进入对应流程）逐屏确认。
- 未验证真实头显下的可读性；本页只保证"字号不会被压到 3% 以下"这一机械约束。
- 未验证繁体/英文最长文案在该下限下的实际排版表现。

## 6. 与评价的关系

本条对应评价方案中的 **EQ1（坐姿、实际观看距离下能否读懂）**。评价方案在
`docs/evaluation/README.md`，由 PR #78 引入；该 PR 合入前此路径在 `main` 上尚不存在，
因此这里只做文字引用、不给链接。

- **改前**：最坏可压到 `1.45 → 0.02`，理论上存在"文字几乎消失"的可能。
- **改后**：最坏 `1.45 → 0.5075`，压缩比封顶 2.86。

这属于**资产层的确定性改动**，不是用户证据。真实观感与"是否仍然读不清"必须由评价里的真人会话回答；
若评价显示问题仍在，下一步应调整 `0.35` 这个参数或改用不依赖缩放的方案（例如固定字号 + 分页/滚动），
而不是继续下调下限。
