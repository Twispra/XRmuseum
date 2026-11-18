# 2026-11-17：可读性下限修正（两个开发/演示场景）

用户明确授权按"方案 A"执行：先覆盖两个**开发/演示场景**，主场景留给组内协调。
本轮改了 26 个 `m_fontSizeMin` 字段，**未改任何文案、字号上限、Prefab、字体或组件引用**。

## 依据

2026-10-11 合入的[无障碍与舒适性走查](../accessibility.zh-CN.md)记录了「自动缩放下限形同虚设」：
`m_fontSizeMin` 被大量设为 `0.02`。2026-11-17 在 `main@9e9f989` 上复测为 **34 个组件 ≤ 0.05**
（32 个 `0.02`、2 个 `0.03`），最坏压缩比 **72.5×**。

该发现已转为 Phase 3 评价方案的 **EQ1**（`docs/evaluation/README.md`，由 PR #78 引入；
该 PR 合入前此路径在 main 上尚不存在）——但本轮改动**依据的是资产层读数，
不是用户证据**；真人会话尚未执行，不得把"已修好可读性"写成已验证。

## 规则

`m_fontSizeMin := max(原值, 0.35 × m_fontSizeMax)`，即自动缩放最多压到设计尺寸的 35%，
两个目标场景的最坏压缩比降到 `1 / 0.35 ≈ 2.86×`。理由与参数取法见[可读性下限](../accessibility-readability.zh-CN.md)。

## 本轮实际改动

| 文件 | 改动 | 压缩比 |
| --- | --- | --- |
| `MuseumVR/Assets/Museum/Features/HelmetInspection/Scenes/HelmetInspectionDemo.unity` | 8 处 | 15.0–72.5× → 2.86× |
| `MuseumVR/Assets/Museum/Features/PhysicalHands/Scenes/PhysicalHandsLab.unity` | 18 处 | 4.5–42.5× → 2.86× |

- diff 共 **26 行增 / 26 行删**，逐行核对后确认**只动 `m_fontSizeMin`**，无其它字段变化。
- 两个文件保持 **LF** 换行（符合 `.gitattributes` 的 `*.unity text eol=lf`）。
- 用同一套审计范围复测，产品场景和 Prefab 中 `m_fontSizeMin ≤ 0.05` 的组件 **34 → 13**，最坏压缩比 **72.5× → 42.5×**。
- 两个目标场景中，`m_fontSizeMin ≤ 0.05` 的组件 **21 → 0**，最坏压缩比 **72.5× → 约 2.86×**。

## 未覆盖

剩余 13 处**全部在 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`**（`SHALLOWER` / `TILT DOWN` /
`TURN RIGHT` / `EDIT CUT` 等移动提示标签）。该文件属共享场景，按 `AGENTS.md` 需先协调单一写入者，
本轮**有意不改**。协调后可把该文件加入脚本的 `TARGETS` 试算，再按主场景的实际布局和三语文案确定调整范围。

## 验证与限制

- **未在本环境运行 Unity**（执行环境跑不起编辑器）。因此：
  - **未目视确认**改后是否出现裁切/溢出——需要有人打开两个场景逐屏检查；
  - 未验证真实头显可读性、繁体/英文最长文案的排版表现；
  - 未构建 Player。
- 已做：diff 范围核对（只 26 行、只一个字段）、复测审计数值、换行与链接检查、脚本干跑/实跑一致。
- 改前/改后逐条明细见 `artifacts/accessibility/qa/runs/20261117-readability-floor/readability-floor-report.json`。

## 下一步

1. 在 Unity 里检查两个场景的简体、繁体和英文布局。若发现裁切，先扩大文本框、调整布局、缩短文案或分页，再复核。提高 `--ratio` 会减少自动缩小的空间。
2. 与组内协调后，在 `MuseumStart.unity` 试算并核对剩余 13 处的三语布局。
3. 真人评价（EQ1）若发现阅读困难，根据具体原因调整字号与布局，可试验固定字号配合分页或滚动。

## 2026-11-18 审查修正

在独立工作树用 Python 3.12 从 Git 读取基线 `9e9f989` 和原 PR 提交 `d114624` 的产品场景与 Prefab。
逐组件按 Unity fileID 比较，确认两场景的差异为 8 + 18 个启用自动缩放的 `m_fontSizeMin` 字段，
全部符合 35% 下限公式，其他场景、Prefab 及两个场景的其他字段逐字节一致。复算保留原逐项明细，
并在汇总报告中标明来源提交，分别记录产品资产和目标场景的统计。将基线场景和原脚本复制到独立运行目录复跑，
干跑保持基线字节一致，写入后与 PR 的两个场景和 26 项变更明细完全一致，再次干跑为 0 项。此审查修正更新文档与汇总报告。

Windows / Unity 2022.3.62f3c1。独立工作树在 `d114624` 加临时 Editor 检查脚本上核对全部 26 项工程 LFS 文件的大小与 SHA-256，
再加载保存的两个场景。在内存中对照原下限和新下限，检查 26 个 TMP 组件的 62 个文本组合，包括
`PhysicalHandsLab` 从保存文案表解析的简体、繁体和英文。两组均无 TMP 溢出、截断或缺字，实际字号一致。
进程自然退出 0，检查前后工程追踪文件 Git 差异为空。完整运行流程、头显和 Player 构建待验证。
