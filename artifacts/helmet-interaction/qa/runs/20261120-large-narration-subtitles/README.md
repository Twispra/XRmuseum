# 旁白字幕大字模式验证

主线基线 `5ddaab5a101a153a0d864e7765df31098f64bc49`。被测代码 `0fc979484489c4f7c2fcbc42e472f2104431feca`，启动 Unity 验证时工作区干净。后续提交增加本目录证据与活动记录。

在 macOS、Apple M5、Metal / Rosetta 上使用 Unity 2022.3.62f3c1 打开保存的 `MuseumStart`。旁白专项通过 274 项检查，进程退出 0，Play Mode 无错误。通过生产 XR 射线操作实际设置和语音按钮，用公开命令切换部分三语及演示状态。检查包括：

- 标准字幕默认启用。未开始旁白时改变预设，字幕与语音保持待播。
- 按 Grip 并松开确认后，已播放的英文音轨继续前进，字幕字号从 0.27 到 0.36。标准预设恢复原有布局。
- 操作提示、供气、双向通话的简体、繁体、英文在大字模式下均有完整字形和足够排版空间。检查字幕与底板边界、固定上沿，以及台面、设置和「显示面板」入口的诊断相机投影。
- 静音时可切换大字模式并启动仅字幕播放。原有重播、自然结束、语言和年代切换、音频互斥和场景卸载行为通过测试。

[旁白完整报告](narration-report.json) · [本地化保存数据](localization-static.json) · [本地化集成与源码核对](regressions.json) · [字体烘焙核对](font-summary.json)

静态本地化 11 项和 Play Mode 本地化集成 134,337 条细粒度检查均通过，两次进程退出 0。集成检查包含逐字符串、逐字形等检查，原始大报告保留在本机输出，摘要记录被测提交和原报告哈希。进程退出后，1,446 个受跟踪工程文件的 SHA-256 与测试前一致。

| 语言 | 标准 | 大字 |
| --- | --- | --- |
| 简体 | [标准](narration-orientation-Chinese.png) | [大字](narration-orientation-Chinese-large.png) |
| 繁體 | [標準](narration-orientation-TraditionalChinese.png) | [大字](narration-orientation-TraditionalChinese-large.png) |
| English | [Standard](narration-orientation-English.png) | [Large](narration-orientation-English-large.png) |

配图由运行中的 Unity 单眼诊断相机渲染。声音验证读取实际 Unity 音源状态。待设备测试真实手柄触达、双眼可读性和旁白可懂度。本次未做 Player 构建或用户研究。

复跑时先关闭其他打开同一工程的 Unity 进程。将 `Unity` 换成 Unity 2022.3.62f3c1 可执行文件，使用已展开的 LFS 资产。在 macOS 可增加 `-force-metal`。

```sh
Unity -batchmode -projectPath MuseumVR \
  -executeMethod Museum.Diegetic.Editor.HelmetDiegeticValidation.RunNarrationBatch \
  -logFile /新的输出路径/narration.log

Unity -batchmode -projectPath MuseumVR \
  -executeMethod Museum.Localization.EditorTools.LocalizationValidation.RunBatch \
  -logFile /新的输出路径/localization-static.log

Unity -batchmode -projectPath MuseumVR \
  -executeMethod Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch \
  -logFile /新的输出路径/localization-integration.log
```

Play Mode 验证入口自行退出编辑器，可保留自动生成的报告目录。复测无需重建保存场景或字体。
