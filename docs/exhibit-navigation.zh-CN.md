# 展台快捷导航

在展厅中可以直接前往左侧、中央、右侧展台，减少连续移动和反复转向的操作。打开 `MuseumVR/`，进入 `Assets/Museum/Scenes/MuseumStart.unity` 后按 Play，即可看到“参观展台”面板；不需要运行场景重建工具。黄昏工房独立场景也已做本轮导航验证。

## 访客操作

1. 用任一只手的 XR 射线指向目的地。
2. 按下选择键（Grip）或触发键（Trigger），保持指向，再松开确认。两键同时按下只确认一次；按住后移出按钮再松开会取消。
3. 画面淡出至黑色后，视点移动到展台前方并朝向展台，再淡入。过渡期间暂停连续移动和转向，结束后恢复各自原来的启用状态。

面板沿用已有的跟随、指向时固定和局部避障逻辑，位置会根据周围物体选择。中央入口随时可用来返回主要交互展品。“视角重置”及 F11 仍返回原始入场位置；在导航过渡中重置会取消尚未完成的路线，避免随后再次跳走。

语言、视点高度偏移、所选年代、头盔姿态、探索发现、提示次数和模型复位计数保持原值。离开展台时停止当前讲解、设备演示与拖动手势；这不是开始新一轮参观。模型正在抓取／返回、前窗或剖切正在抓取、结束参观确认页打开时，先完成当前操作再导航。目标位置缺少可用地面或被实体物体占用时，会显示提示并保留当前位置；淡出后移动前会再次检查。

## 接入位置

运行时入口是 `Assets/Museum/Features/GalleryNavigation/Runtime/GalleryNavigator.cs`。场景加载后，识别 `Exhibit Placeholders` 下的 `Exhibit 01 - `、`Exhibit 02 - `、`Exhibit 03 - ` 编号与各自的 `Plinth`，生成唯一面板；没有保存新的主场景或公共 XR 预制件。目的地从展台前沿计算，额外观察距离依次为 0.95、0.75、1.45 米，是本原型的布局参数。

移动的是 XR Origin 的世界位置及水平朝向，保留被追踪相机的局部位置、局部旋转和当前眼高，没有写入固定成人身高。实际头部俯仰仍由追踪输入决定。

`GalleryNavigationButton` 处理按下／松开、同手的 Grip／Trigger 合并和移出取消。`HelmetNavigationHandoff` 调用原有站点的停止演示、讲解与手势逻辑。`VisitorViewRecovery` 新增复位通知以中止过渡；`HelmetInspectionPalette` 新增可选偏移配置，既有面板的默认位置规则保持原样。

八条 `navigation.*` 文案同时保存在 JSON 与 LocalizationTable，显示文字使用 LocalizedText。静态字体仅补入“台、央、往”，源 TTF、字体／材质／图集身份保持。字体维护见原三语接入说明，正常打开工程不需要重新烘焙。

## 复测

使用 Unity **2022.3.62f3c1**、已展开的 LFS 资产。在没有真实 HMD 的编辑器中运行：

```sh
Unity -batchmode -force-metal -projectPath MuseumVR \
  -executeMethod Museum.Navigation.Editor.GalleryNavigationValidation.RunBatch \
  -navigationOutput /绝对路径/新的输出目录 \
  -logFile /绝对路径/navigation.log
```

不要添加 `-quit` 或 `-nographics`；验证入口控制 Play、渲染截图、退出 Play 和自然退出进程。目录必须不存在。测试黄昏工房时，增加 `-navigationScene Assets/Museum/Features/DuskWorkshop/DuskWorkshop.unity`。公共姿态夹具与合成控制器输入只存在于验证过程中，不保存到工程。

本轮报告、三语画面、黑场覆盖及复跑信息见 [独立验证目录](../artifacts/helmet-interaction/qa/runs/20261120-exhibit-quick-navigation/README.md)。编辑器合成输入通过不代表真实头显的双眼可读性、触达、舒适度、性能或 Windows Player 构建验收；合并后仍需组员做设备检查。
