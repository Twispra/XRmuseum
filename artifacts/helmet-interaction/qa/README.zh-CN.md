# 头盔交互验收记录

最终结果见 `playmode-validation.json`，对应完整日志 `playmode-editor-final.log` 留在本地，重跑可重新生成；JSON 结果已随源码保存。场景和资产导入尺寸见 `setup.json`。实际场景静态渲染位于 `../preview/`，与交互自动测试分开记录。

测试使用项目中已有的输入动作、ActionBasedController、直接抓取器、XRGrabInteractable 和物理系统。合成 XR 设备事件经过正常玩家输入更新，不直接调用选择 API。Home 使用独立虚拟键盘；RESET 按钮通过控制器抓握选中，调用场景序列化事件。测试不会保存 Play Mode 中的场景变化。

验收包括十次拿起、旋转、放回、松手后的稳定性和穿透检查；每轮还要求自动复位次数为零，防止复位掩盖放置失败。另检查抓取中 Home、松开后的再次抓取、自由状态 Home、清除旧速度、RESET 按钮和掉落复位。

## 测试环境修正

- 无 Game 画面的批处理可达到每秒数千帧。本项目 XRI 2.6.4 在帧间隔小于 1 ms 时跳过投掷速度采样，间歇采样会产生异常松手速度。批处理测试现按约 90 Hz 节奏运行；没有因此关闭产品投掷或放宽通过标准。`diagnostic-uncapped-input.json` 保留原始异常样本。
- 后台窗口曾禁用预先创建的虚拟键盘。测试先设置后台输入行为、显式启用测试设备，再在玩家 Dynamic 输入更新中排队事件。`diagnostic-editor-keyboard.json` 和 `input-probe.json` 是定位过程记录。
- 这是模拟交互验收，不是 VR 帧率测试。真实头显追踪、延迟、控制器按键手感，以及官方模拟器的整套键鼠导航仍需人工检查。

## 重跑

关闭项目的其他 Unity Editor 实例后，用 Unity 2022.3.62f3c1 启动：

```text
Unity.exe -batchmode -projectPath E:\XRmuseum\MuseumVR -executeMethod Museum.Editor.HelmetPrototypeValidation.RunBatch -logFile E:\XRmuseum\artifacts\helmet-interaction\qa\playmode-editor-final.log
```

不要添加 `-quit`：脚本在 Play Mode 验收完成、返回 Edit Mode 并导出静态预览后自行退出。不要添加 `-nographics`，预览需要图形设备。也可在 Unity 菜单运行 `Museum > Validate Helmet Interaction (Play Mode)`；交互验收结束后回到 Edit Mode。
