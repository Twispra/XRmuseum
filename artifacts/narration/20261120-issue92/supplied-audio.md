# #92 本次提交的旁白

2026-11-20 用户指定以下六个 MP3 作为本次 Unity 旁白来源，并确认已人工验证这六条录音，授权用于项目及随 PR 提交。随附信息未注明录音人、原始生成工具或其他许可条款。Codex 辅助核对片段、解码、检查响度与字幕对应，并执行 Unity 回归。

| 片段 | 普通话 MP3 | 英语 MP3 |
| --- | --- | --- |
| 首次操作 | `对准外壳按住握持键并.mp3` | `Aim at the.mp3` |
| 供气 | `这个演示中水面团队用.mp3` | `In this demo.mp3` |
| 通话 | `这个演示中潜水员通过.mp3` | `In this demo (1).mp3` |

FFmpeg 7.1 将原始 24 kHz 单声道 MP3 解码为 24 kHz、16 位单声道 PCM WAV，保留原音量、语速和停顿。六个输出沿用 Unity `HelmetNarration` 目录下已有资源名、`.meta` 与 GUID，通过 Git LFS 提交。输入和输出的 SHA-256、逐轨时长、响度与核对记录见 [supplied-verification.json](supplied-verification.json)。

```powershell
ffmpeg -nostdin -hide_banner -loglevel error -xerror -n -i "<源 MP3>" -map 0:a:0 -map_metadata -1 -ac 1 -ar 24000 -c:a pcm_s16le "<新目录中的目标 WAV>"
```

采用本机 SenseVoice 识别与同语言参考音轨的声学对照，确认两条同名英文音轨分别是供气、通话，并核对六条对应的[原稿](../20261109-issue63/scripts.json)。首次操作普通话的首处“握持键”在整段识别中漏一个字，截取前 4.2 秒重识别得到完整词。英文供气的 `through` 被识别为同音的 `threw`，仍对应原字幕。离线识别用于内容核对，人工音质与实机可懂度单独记录。

Unity 测试覆盖保存主场景中的音轨、字幕及停止行为，记录见[本次回归摘要](supplied-unity-verification.json)。[任务 #92](https://github.com/Twispra/XRmuseum/issues/92)
