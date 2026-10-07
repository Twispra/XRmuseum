# 2026-10-06/07 年代切换装备到中央展品头盔（含复审修复）

## 任务来源

PR #10（年代模块）与 #14（本地化接入）合并后，年代模块仍游离在展厅外。经组长确认方向：**不做独立年代展台小部件，直接把年代切换装备到展厅中央的展品头盔上**，保留材质/颜色差异，不做造型（与 Group Component「一个简化 Mark V 头盔」的承诺一致；客户回复 A3 也只要求一件展品）。

## 首轮（PR #27，2026-10-06）

- 中央头盔（实例名 `HelmetExhibit`）挂 `HelmetEraSwitcher` + `HelmetEraPlayModePreview`，按部件名自动绑定渲染器
- 9 个年代材质改写为 `Museum/Inspection Shell`（只换 `_BaseColor`），文件 guid 不变
- 新增 `HelmetEras/Editor/HelmetEraIntegration.cs`（Integrate / Remove / Verify Section Compatibility）
- 验证：三年代目视差异明显、队友功能回归正常、剖切兼容性探针 3 年代 × 249 渲染器通过

**组长复审（7 项）**：①[P1] 两个年代按钮埋在 Plinth 体内（中心 (±0.45,1.06,2.95)，Plinth 边界 x±0.675、y0..1.1、z2.825..4.175），射线先命中 Plinth 无法点击；②[P2] 三个 Window 材质用不透明 Shell 覆盖了 PR #24 的透明玻璃（`Museum/Inspection Glass`）；③[P2] 场景回退导致 `HelmetConnectionDemonstration.rearConnections` 引用丢失；④[P2] AIR 热点锚点回退（`anchorReady=1` 但 `localAnchor.x` 停留旧值，偏差 11.3cm 超出 3cm 触发半径）；⑤[P2] RESET 回调从 `RequestResetToDisplay` 回退成 `ResetToDisplay`，丢了 0.30s 归位动画与音效；⑥[P2] N/P 与数字键和 XR 模拟器冲突；⑦[P2] project-state.md 覆盖了 main 的最新整合记录。

③④⑤⑦ 的共同根因：首轮场景 blob 基于旧 main（`d84c93f`），把组长 22:03 合入的 PR #24（helmet experience 整合，主场景 +428 行）回退了。

## 复审修复（本轮）

| 项 | 修复 |
| --- | --- |
| ① 按钮位置 | 移到**展台顶面上方** `(±0.30, 1.155, 3.05)`（按钮底 1.12 > Plinth 顶 1.1，脱离边界），生成器默认值同步修改 |
| ② 窗口材质 | 三个 Window 年代材质改用 `Museum/Inspection Glass`（透明 `_BaseColor.a=0.35`，保留年代色调）；该 shader 同样支持剖切/悬停 |
| ③④⑤ 场景回退 | 放弃旧场景基准，**在 main 最新场景（`0c0aeee`，含 PR #24 全部改动）上重新执行 Integrate**，rear connections / 热点锚点 / 复位回调自动恢复为 main 状态 |
| ⑥ 键位冲突 | `HelmetEraPlayModePreview` 默认键改为 **F9 / F10**，数字键默认关闭；生成器报告文案同步 |
| ⑦ 文档 | 放弃覆盖版 project-state.md，**在 main 版本之上仅追加**本轮条目；PR 描述基准信息同步更新 |
| 探针 | `Verify Section Compatibility` 按绑定槽位分别核对：Window 渲染器须为 `Museum/Inspection Glass`，其余须为 `Museum/Inspection Shell`，均须带 `_CutEnabled`/`_CutPlane`/`_Hover` |

## 验证

- Unity 重载最新场景 → 重新执行 Integrate → Console 报告绑定与按钮就位
- `Verify Section Compatibility` 探针按新规则通过（结果以最新一轮 Console 为准，见 PR 描述）
- Play 目视：按钮在展台顶面可见可点、窗口保持透明、年代切换正常、菜单/热点/三语/抓取回归正常
- 未做真实头显、参与者评价与构建

## 交接

- 中央头盔实例名 `HelmetExhibit`（队友改名，工具双候选名匹配）
- `Remove Era Switching From MuseumStart` 一键还原本工具改动
- 年代按钮后续可并入组员的交互面板 UI（本轮刻意避开共享面板）
- Play 启动时 XR Starter Assets 的报错（line visual / XRPokeInteractable）为 main 既有问题，另反馈
