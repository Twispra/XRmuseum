# #63 三段旁白的音轨来源与复现

三段文字为本项目编写。首次操作说明采用当前实体站的握持、旋转和前窗操作。供气、通话均用“这个演示中”限定为展项的教学内容。旁白的史料与操作出处见 #63 活动记录和三语表。下表按资源名提供运行时所需的六条可分发成品。

| 片段 | 普通话音轨与时长 | 美国英语音轨与时长 |
| --- | --- | --- |
| 首次操作 | `intro-zh.wav` · 14.125 s | `intro-en.wav` · 10.599 s |
| 供气 | `air-zh.wav` · 7.219 s | `air-en.wav` · 4.549 s |
| 通话 | `voice-zh.wav` · 7.847 s | `voice-en.wav` · 5.244 s |

简体与繁体字幕共用普通话 `sit/cmn` 音轨，英文字幕使用美国英语 `gmw/en-US`。时长来自 PCM 帧数。六条音轨为 22.05 kHz、16 位、单声道 WAV，来自 eSpeak NG 1.52.0 原生共振峰合成。程序通过 UTF-8 文件读取[逐字脚本](scripts.json)，以统一的振幅参数 82 生成。普通话和英语分别使用速度参数 150 和 155，保存程序的原始 PCM。逐条记录时长、采样峰值、有效信号和 SHA-256。[音轨校验](audio-verification.json)

## 合成工具与成品使用

使用 eSpeak NG 1.52.0 官方 Windows x64 MSI，包 SHA-256 为 `7F673C709EA5DD579D3B5EBB98688CC575328A6AB7438D2BC405B88CEDAEAFB9`。Windows MSI API 以只读方式提取内嵌 CAB，Windows `expand.exe` 解包到独立工具目录。二进制 SHA-256 记录在校验文件。运行时资源仅包含以项目原文合成的 WAV，使用 Git LFS。旁白来源记为 eSpeak NG 原生语音合成，无真人录音或 MBROLA 语音库。[官方版本](https://github.com/espeak-ng/espeak-ng/releases/tag/1.52.0) · [Microsoft SHA-256 清单](https://github.com/microsoft/winget-pkgs/blob/master/manifests/e/eSpeak-NG/eSpeak-NG/1.52.0/eSpeak-NG.eSpeak-NG.installer.yaml) · [版本语音文件](https://github.com/espeak-ng/espeak-ng/tree/1.52.0/espeak-ng-data/lang)

eSpeak NG 程序为 GPL-3.0-or-later。项目根据 GPL 第 2 节、GNU FAQ 的输出原则，以及 eSpeak 原作者对自生 WAV 的说明，将本项目原文生成的原生语音作为项目音频分发。工具、安装包、引擎数据及第三方录音不属于交付物。此记录以工具和声音来源进行署名。[项目许可](https://github.com/espeak-ng/espeak-ng/blob/1.52.0/COPYING) · [GNU 输出许可说明](https://www.gnu.org/licenses/gpl-faq.en.html#WhatCaseIsOutputGPL) · [eSpeak 作者关于 WAV 的说明](https://sourceforge.net/p/espeak/discussion/538920/thread/58c2e481/) · [原生语音与 MBROLA 来源区别](https://sourceforge.net/p/espeak/discussion/538921/thread/0765155033/)

## 重新生成与复核

在仓库根目录执行，将 `--engine` 指向与 `espeak-ng-data` 同级的 eSpeak NG 1.52.0 Windows CLI 所在目录。更换脚本或参数时用新目录生成并审听，核对替换音轨的字幕与播放规则。

```powershell
python artifacts/narration/20261109-issue63/generate-audio.py --engine 'E:/Codex/qa63/espeak-1.52.0' --audio-dir 'MuseumVR/Assets/Museum/Features/DiegeticUI/Resources/HelmetNarration'
```

[生成脚本](generate-audio.py)复跑后对现有文件比较 SHA-256。现有检查覆盖有效 PCM、可检测信号、无数字削波和逐条校验。普通话及英语发音、语速、响度与目标头显内可懂度待人工试听。该工具音色属于离线合成的首版旁白。
