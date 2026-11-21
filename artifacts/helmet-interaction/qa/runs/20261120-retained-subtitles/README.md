# 旁白字幕保留验证

基线为 `origin/main@5ddaab5a101a153a0d864e7765df31098f64bc49`。被测代码为 `36b7f175c6c2f28450b55e0d82443eb2e0593c8e`，测试开始时工作区干净。测试在 macOS 的 Unity 2022.3.62f3c1 中运行保存的 `MuseumStart`，Metal 渲染，使用模拟 XR 手柄和头部输入。

- 英文首次操作、繁体通话旁白均实际等待音轨结束，核对字幕、释放后的 AudioSource 以及关闭按钮。简体供气在静音状态显示同一字幕，超过原片段时长后仍可读。
- 模拟射线确认可收起字幕、恢复播放按钮，并回归了主动停止、重播和播放中静音。验证语言切换、关闭演示、设置内重置视角和结束参观会收起已保留文字。
- 在正式展台按钮上检查三语按钮文案、字形和溢出。简繁按钮使用「收起文字」，以适配主线字体。三张诊断图取自字幕已保留的状态。用于比较的其他截图留在本机。

| 检查 | 记录 |
| --- | --- |
| 旁白专项 238 项通过 | [报告](narration-report.json) |
| 静态本地化 11 项通过 | [报告](localization-static.json) |
| 主场景本地化集成 132,270 条检查通过 | [验证摘要](regressions.json) |

三个 Unity 进程均正常退出 0，测试前后 1,446 个受跟踪工程文件的 SHA-256 一致。集成计数包含逐字符串和字形检查。

[简体供气](narration-retained-air-Chinese.png) · [繁體通話](narration-retained-voice-TraditionalChinese.png) · [English orientation](narration-retained-orientation-English.png)

## 复跑

用 Unity 2022.3.62f3c1 打开 `MuseumVR`，进入保存的 `MuseumStart`。从中央展台播放语音，等待播完后确认字幕和「收起文字」按钮，再点击收起。将「声音」设为关闭后再播放，可检查纯文字路径。

自动复跑可在仓库根目录使用以下方法，为每次运行指定独立的日志文件。测试自行退出进程。

```sh
Unity -batchmode -projectPath MuseumVR \
  -executeMethod Museum.Diegetic.Editor.HelmetDiegeticValidation.RunNarrationBatch \
  -logFile /绝对路径/narration.log
Unity -batchmode -projectPath MuseumVR \
  -executeMethod Museum.Localization.EditorTools.LocalizationValidation.RunBatch \
  -logFile /绝对路径/localization-static.log
Unity -batchmode -projectPath MuseumVR \
  -executeMethod Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch \
  -logFile /绝对路径/localization-integration.log
```

模拟测试检查 Unity 音频状态和单眼画面。真实头显触达、双眼可读性、人工试听和 Windows Player 仍待验证。
