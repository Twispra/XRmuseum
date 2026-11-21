# 结束参观小结验证

基线为 `origin/main@5ddaab5a101a153a0d864e7765df31098f64bc49`。被测代码为 `a40e369335aa19ac8008c9f11729abed402f3b46`。测试使用 Unity 2022.3.62f3c1、macOS、Apple M5、Metal / Rosetta。主场景来自保存的 `MuseumStart`。

## 验证范围

- 在正式 XR 射线上打开「结束参观」、选择「继续参观」和「开始新参观」。合成手柄事件经正常输入更新排入生产交互路径。
- 以公开接口为夹具，检查空记录、部分记录、全部记录和开始新参观后的状态。两段演示和全部三个部位均有覆盖。
- 核对跨年代的同编号重复观察。先查看 Mark V 胸甲，再切换到 SuperLite 17B 查看颈部密封。小结保留第一次记录的「胸甲」，进度仍按已有的同编号计数机制计算。小结会把已有完整译文转换为该语言的部件标题。
- 同一小结切换简体、繁体和英文，检查字形、换行及状态。三张最终画面为 Unity 诊断相机的单眼渲染。最初版本的透明确认牌底板会盖过距离相机较远的文字。修正绘制顺序后复查画面，三种语言的供气、通话和清空提醒均可见。

| 检查 | 记录 |
| --- | --- |
| 小结专项 88 项通过，进程退出 0 | [完整报告](recap-report.json) |
| 静态本地化 11 项通过 | [报告](localization-static.json) |
| Play Mode 本地化集成 136,749 条检查通过，0 错误及警告 | [摘要、进程状态与项目文件哈希](regressions.json) |

本地化集成计数包括逐字符串、逐字形等检查。三个进程退出码均为 0，测试前后 1,448 个受跟踪工程文件的 SHA-256 一致。

[简体预览](visit-recap-Chinese.png) · [繁體預覽](visit-recap-TraditionalChinese.png) · [English preview](visit-recap-English.png)

## 复跑

先退出其他打开同一工程的 Unity 实例。下面的 `Unity` 代表对应版本的 Unity 可执行文件，使用独立的日志路径。Play Mode 测试自行退出进程。

```sh
Unity -batchmode -projectPath MuseumVR \
  -executeMethod Museum.Diegetic.Editor.HelmetDiegeticValidation.RunVisitRecapBatch \
  -logFile /绝对路径/recap.log

Unity -batchmode -projectPath MuseumVR \
  -executeMethod Museum.Localization.EditorTools.LocalizationValidation.RunBatch \
  -logFile /绝对路径/localization-static.log

Unity -batchmode -projectPath MuseumVR \
  -executeMethod Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch \
  -logFile /绝对路径/localization-integration.log
```

真实头显双眼可读性、实际控制器触达、Windows Player 和用户评价待验证。调试日志、早期截图及本地化集成原始大报告保存在本机输出。
