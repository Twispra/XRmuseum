# 2026-10-17 操作主面板（可隐藏、随时唤出、出现时朝向用户）

- 任务：认领 [Issue #62](https://github.com/Twispra/XRmuseum/issues/62)（认领评论为该 issue 首条留言）。
- 基准：`main` @ `7011f785`（PR #59 合并后，2026-10-17 15:05 HKT）。开工前核对过 #59 已合并、`DiegeticUI` 模块与 `docs/diegetic-ui.zh-CN.md` 已在 main。
- 认领前核对过撞车：#62 当时 0 条认领；#60 已由他人跟进，本任务不涉及。

## 改了什么，为什么

- 新增 `MuseumVR/Assets/Museum/Features/OperatorPanel/`（Runtime 控制器 + Editor 安装/校验，含 .meta，全部 LF）。
- 设计与范围草图：`docs/operator-panel.zh-CN.md`（认领留言承诺的草图 + 规则表 + 参数说明）。
- 三语：`MuseumTranslations.json` 追加 `operator.panel.show / hide / hint` 三条，不动既有条目；字形缺口由 `TrilingualFontBaker` 烘焙，验收脚本逐字符核对。
- **不修改** `DiegeticUI`、Packages、ProjectSettings、`docs/project-state.md`；共享主场景的改动只有 station 上的一个组件（唤出按钮运行时构建，不进场景对象）。

## 行为要点

- 主面板 = 设置 + 模型说明两组；三个入口（面板/设置/说明）唤出时都按当时视角摆到眼前、仅水平朝向，之后固定不追随。
- 收起不重置任何展品状态、不关讲解卡；隐藏后碰撞体停用不挡射线。
- 拖动中唤出/收起被明确拒绝；设备拖动期间由 station 的 XRI 过滤阻止入口被确认（两层防护，均与既有按钮规则一致）。

## 验证与证据

- 编辑器校验：`Museum/Operator Panel/Validate installed scene`（安装、面板查找、三语登记、保存字体逐字符字形覆盖、入口位置间隙、守卫纯函数）。
- 待补：键盘 `G` 模拟验收截图/录屏（由认领人在本机 Unity 执行后回填）、真实头显走查（需有头显组员）、Player 构建。

## 记录与交接

- 面板摆放参数（0.65 m 距离 / 低 0.06 m / 避让 0.55 m）为可调原型值，待真实头显体验校准。
- 若 station 未来增加新的展台固定按钮，`OperatorPanelController.entryLocalPosition` 可能需要随草图重新核对间隙（校验脚本会报）。

## 2026-10-17 验收回填

- **键盘模拟验收（Zhang Feifan，Unity 编辑器 XR Device Simulator，英文输入，G 键）**：唤出后面板出现在眼前并朝向观察者；转头、走近均不追随；收起后再唤出正常；年代切换、前窗、复位等既有操作不受影响。游戏视图截图与 Console 记录已留存本机。
- **编辑器校验**：`OPERATOR_PANEL_VALIDATION: passed=16 failed=2`。两个 FAIL 均为校验脚本自身设计问题——它在编辑模式下查找 station 运行时（`Start()`）才构建的 `Pedestal edge controls / Fixed settings / Fixed exhibit information`，而保存场景本就不含这些对象。已把这两项改为「按运行模式核对预期状态」：编辑模式断言场景不含运行时内容，Play 模式断言内容存在。其余 16 项全部通过，含三条新文案在简/繁/英下的逐字符字形覆盖（`FONT_COMPLETION_PASSED`，所需字形已在保存字体中，零新增字符，字体资产未改动）。
- **场景 diff**：仅新增 station 上的 `OperatorPanelController` 组件（含序列化引用与参数）及组件列表一行；团结版编辑器保存时的尾随空格重序列化为已知无害噪声。
