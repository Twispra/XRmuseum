# 潜水头盔：第一版抓取原型

场景为 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`。中间展台现已放入 v02 头盔简模，原抓取测试方块的 Prefab 仍保留作参考。

## 现在可以怎样玩

1. 在 Unity Hub 打开本仓库的 `MuseumVR`，使用 **2022.3.62f3c1**。打开 `MuseumStart`，点击 Play，再点击 Game 画面。
2. 初始位置已靠近中间展台。按 **Y** 切换为操作右控制器，**W/A/S/D** 前后左右移动，**Q/E** 降低／抬高。按住鼠标右键可调整头部视角。
3. 将控制器靠近头盔，按住 **G** 抓住；保持 G 并移动控制器即可拿起。**R** 切换鼠标控制平移／旋转，移动鼠标可以调整所选控制器姿态。松开 G，头盔恢复重力和碰撞。以模拟器屏幕提示为准。
4. 放回展台时缓慢降低，松手后观察是否稳定。物体保留抓取位置，不会强制跳到某个固定握点。
5. 按 **Home** 把头盔放回起始位置；也可将控制器靠近展台的金色 **RESET** 按钮并按抓握键。头盔未被抓住、掉到场景下方或远离展台时会自动回到展台。

Home 在抓取时也可使用。复位后先松开 G，再按住 G 抓取，避免头盔瞬间吸回原来的手上。Home 与模拟器的 R 键用途不同。

## 实现范围

- 模型保持米制尺度，外包尺寸约 **宽 31.75 × 高 49.53 × 深 41.91 厘米**。
- 使用 4 个简化碰撞体、带重力的刚体和 XRI 抓取组件；单手抓取，支持松手换手。质量参数 1.5 kg 是本轮交互调试值，不是历史实物重量。
- 抓取通过物理速度跟踪，配有接触摩擦与刚体插值；控制器不能提供真实重量的力反馈。
- 复位按钮与 Home 恢复位置、角度并清除运动。未持有时低于世界高度 -2 m 或距离起始位置超过 12 m，持续 0.5 秒后自动复位。
- 本轮保留简模材质。手模型、手指抓握姿态、双手承托、前窗开合和精修材质尚未接入。

## 验证与实机检查

自动验证结果见 [Play Mode 检查记录](../artifacts/helmet-interaction/qa/playmode-validation.json)，资产尺寸及导入检查见 [场景建立记录](../artifacts/helmet-interaction/qa/setup.json)。自动测试通过模拟 XR 设备状态驱动工程现有的输入动作、控制器与抓取器，没有直接调用抓取选择 API 来代替输入。

2026-09-23 验收通过：连续十次拿起、旋转、放回和松手；十轮均无自动复位介入。抓取中 Home、松开后再次抓取、自由状态 Home、清除旧速度、实体 RESET 按钮和掉落恢复均通过。测试运行阶段未记录 Error/Exception。后台测试的帧率节奏与虚拟键盘修正见[验收说明](../artifacts/helmet-interaction/qa/README.zh-CN.md)；启动日志中未连接头显的 OpenXR 提示不等于实机已验证。

场景静态预览：[初始视角](../artifacts/helmet-interaction/preview/01-player-start.jpg)、[头盔与复位按钮](../artifacts/helmet-interaction/preview/02-helmet-and-reset.jpg)。这两张图由 Unity 渲染，不代表头显录屏。

模拟测试不能代替头显验收：接入设备前按[现有操作指南](getting-started.zh-CN.md#接真实-pc-vr-头显)关闭自动模拟器并启用对应 OpenXR runtime。需要实际检查握持距离、物体大小、运动延迟、放置手感及舒适度。

## 项目入口

- [头盔 Prefab](../MuseumVR/Assets/Museum/Prefabs/HelmetExhibit.prefab)
- [运行时复位逻辑](../MuseumVR/Assets/Museum/Scripts/HelmetExhibitReset.cs)
- [场景与 Prefab 生成工具](../MuseumVR/Assets/Museum/Editor/HelmetPrototypeSetup.cs)：Unity 菜单 `Museum > Build Helmet Interaction Prototype`。会重建本轮头盔站点，先保留你手动修改的站点设置。
- [自动验证工具](../MuseumVR/Assets/Museum/Editor/HelmetPrototypeValidation.cs)：Unity 菜单 `Museum > Validate Helmet Interaction (Play Mode)`。
- [模型原型及参考依据](../artifacts/helmet-prototype/README.zh-CN.md)

本轮模型与新增脚本由 Codex 辅助制作；XR 基础与模拟器沿用 Unity 官方 XRI 样例。团队提交时保留这些来源说明，并记录成员后续实测、设计决策和修改贡献。
