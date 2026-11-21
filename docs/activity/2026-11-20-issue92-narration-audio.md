# 2026-11-20 #92 替换六条展览旁白

[Issue #92](https://github.com/Twispra/XRmuseum/issues/92) 由 Ereshkigal1224 认领，Codex 辅助。用户提供并确认已人工验证六个 MP3，覆盖首次操作、供气、通话的普通话和英语。核对 `origin/main` 为 `5ddaab5`，提交分支为 `codex/issue-92-supplied-audio`，此前本地试听候选已作废。

## 音轨与 Unity 接入

在本机将六个 MP3 解码为 24 kHz、16 位单声道 PCM WAV，保留原音量和停顿。沿用原资源名、`.meta` / GUID 与 Git LFS。两条同名英文文件通过声学对照与本机识别区分。实测响度为 −21.10 至 −18.58 LUFS，估算真峰值为 −5.27 至 −3.14 dBTP。对照试听页内嵌的六条 WAV 与 Unity 资源哈希一致。 [来源、转换与逐轨校验](../../artifacts/narration/20261120-issue92/supplied-audio.md)

## 验证与交接

Windows / Unity 2022.3.62f3c1，展开 LFS 资源后运行 `-batchmode -projectPath <worktree>/MuseumVR -logFile <独立日志> -executeMethod Museum.Diegetic.Editor.HelmetDiegeticValidation.RunNarrationBatch`。被测本地提交为 `968202f9d6565cc4137e92d49cb6c0fd6a441d27`。测试时 tracked 文件干净，另有上一轮本地诊断目录的十个未跟踪文件。提交分支的完整 `MuseumVR` 文件树与被测版本一致。

184/184 项通过，Editor 自然退出 0。检查包括六条导入、三种界面语言下的选轨和字幕、播放/停播、语言切换、复位与声音互斥。66 个输入文件的哈希回读一致，其中包括六条 WAV。 [本次回归摘要](../../artifacts/narration/20261120-issue92/supplied-unity-verification.json)

下一步由成员复审。目标头显可懂度和 Windows Player 构建仍待独立验证。
