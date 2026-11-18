# #77 右侧展台供气与通话示意图

- 基线为 `adc06875103cc47a555dd251e3cf80d836a09558`。开发分支为 `codex/issue-77-right-exhibit-panel`。
- [#77](https://github.com/Twispra/XRmuseum/issues/77) 交付独立展板。共享场景接入等待 [#64](https://github.com/Twispra/XRmuseum/issues/64) 记录方案决定。

## 展板

展板用一条朝右的箭头表示水面泵向潜水头盔供气，用一条带两个箭头的路线表示水面团队与潜水员双向通话。每行写出用途和两端名称。颜色帮助区分两行，单向和双向箭头同时提供形状提示。底部箭头指向中央展台的 Mark V 演示。

展板是教学示意。路线依据现有 Mark V 展项的单向供气和双向通话演示。现有展项的解释性取舍见 [实体站说明](../diegetic-ui.zh-CN.md)、[线索与演示说明](../helmet-exploration.zh-CN.md)和[设备资料与模型](../refined-model-integration.zh-CN.md)。

展板沿用展厅材质、现有语言通道和静态字体图集。三语表新增七个 `exhibit.right_connection.*` 键，并复用既有的标题和头盔名称键。

## 预览与接入

1. 在 Unity 2022.3.62f3c1 中打开 `Assets/Museum/Scenes/MuseumStart.unity`，进入 Play Mode。
2. 选择 **Museum → Right Exhibit → Preview in MuseumStart (Play Mode)**。工具会在右台前侧临时挂载 `Assets/Museum/Features/RightExhibit/RightExhibitPanel.prefab`，并隐藏右台原有的两行前侧展签。
3. 从右台正面查看展板。可以用入口的语言按钮切换简体中文、繁体中文和英文。退出 Play Mode 即移除预览。

方案获确认后，将 Prefab 放在 `Exhibit Placeholders/Exhibit 03 - Placeholder` 下，局部位置为 `(3.10, 0.74, 2.801)` 米。停用 `Plinth title 3.1` 与 `Plinth caption 3.1` 两个旧标签。保存前复查原 AIR／VOICE 管线与顶部标识。

## 验证

Windows / Unity 2022.3.62f3c1，Windows64 Editor 批处理。测试时临时挂载展板，不保存 `MuseumStart`。编辑器自动写入的 XR 和项目设置在验证结束后恢复到基线。

| 批处理入口 | 结果 |
| --- | --- |
| `Museum.RightExhibit.Editor.RightExhibitPanelValidation.RunBatch` | 1,096 项通过，退出码为 0。检查三语文字和字形、排版、四个坐姿诊断眼位、箭头方向、碰撞和通行。用 `-rightExhibitOutput <目录>` 保存报告和三语截图。 |
| `Museum.Localization.EditorTools.VisitorEraValidation.RunBatch` | 1,131 项通过，0 项错误、0 项检查警告，退出码为 0。检查保存主场景的年代切换和 Mark V 演示入口。该回归使用现有保存场景，独立展板由上一行的专项测试挂载。 |

真实头显双眼可读性、控制器和 Windows Player 构建尚未验证。
