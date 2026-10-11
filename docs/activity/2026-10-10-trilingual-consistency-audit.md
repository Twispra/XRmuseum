# 2026-10-10：三语文案一致性核对（JSON ↔ Unity 保存表、字形覆盖）

对应 `docs/project-state.md` 待办表中的「繁体语言审校、阅读／布局／声音舒适性」——本轮做的是其中**可以离线自动化的那一半**：源文案与运行时保存表是否一致、文案用到的字符是否都已烘焙进字体。人工术语审校、真机阅读与舒适性仍待成员完成。

基线：`main` @ `270c7b72`。工作分支 `codex/trilingual-consistency-audit-2026-10-10`。

## 做了什么

用纯标准库脚本按 YAML 折叠规则 + Unicode 转义还原，逐一比对
`Tables/MuseumTranslations.json`（作者源）与 `Tables/LocalizationTable.asset`（Unity 实际加载的表），
再核对文案字符与 `Fonts/NotoSansSC-MuseumSubset SDF.asset` 的已烘焙字形、`characters-required.txt`。

脚本与结果：[`artifacts/localization/qa/runs/20261010-trilingual-consistency/`](../../artifacts/localization/qa/runs/20261010-trilingual-consistency)（`audit_trilingual.py`、`report.json`）。

**未修改任何产品文件**：没有改文案、保存表、字体、场景、Prefab 或代码；本 PR 只新增证据与记录。

## 验证

- 环境：Windows 11 · Python 3.13.12 · **未启动 Unity**（纯文本核对）。
- 命令：`python artifacts/localization/qa/runs/20261010-trilingual-consistency/audit_trilingual.py`，**退出码 0**。
- 结果：
  - **保存表与 JSON 完全一致**：205 条 × 3 语 = **615 个字段，0 处不一致**；两侧 key 集合无差异。
    比对前按 YAML flow scalar 折行规则还原换行、并解码 `\uXXXX`，因此**换行符与全角标点也参与逐字符比对**。
  - **字形覆盖无缺口**：文案实际用到 **511** 个不同字符，SDF 已烘焙 **536** 个字形，**缺失 0**。
  - `characters-required.txt` 的 **526** 个字符全部已烘焙。
  - 结构检查：空字段 0、重复 key 0、繁体字段中混入简体字 0、中文字段中半角标点 0。
- 供作者判断的 2 项（**未擅自修改**，记在 `report.json` 的 `openJudgementItems`）：
  1. 繁体字段有 3 处使用「着」（`welcome.body`、`access.seated`、`text.805b59527b6f`）；繁体书面标准通常作「著」。
  2. 语言标签 `text.9937e365aaf3` 三语均为「简体中文」，而 `text.72726d8818f6` 的繁体为「簡體中文」——
     两种写法取决于语言按钮采用「以该语言自身书写」还是「随界面语言转换」的约定，需作者确认。

## 交接

- 本记录由 Codex 在用户授权下辅助完成：脚本、比对与证据整理为 AI 辅助，**未新增任何外部资源**（无新字体、贴图、模型、音频或 package）。
- **未验证**：未启动 Unity，因此没有运行时实例化检查、界面布局／换行效果检查；未做人工术语审校、母语者复核、真实头显阅读与遮挡检查。自动核对通过**不等于**三语体验验收。
- 下一步最小动作：由三语文案作者就上述 2 项判断决定是否统一，再用同一脚本复跑确认；界面层换行／遮挡仍需人工在编辑器或真机上检查。
