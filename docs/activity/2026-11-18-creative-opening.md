# 中央展台第一分钟 · 水面与潜水员的通话

访客站在水面这一端。眼前的问题是“戴上这顶头盔，就能独自工作了吗？”木制电话箱前有一枚铜键，松开才发话。先在电话上方读到“潜水员，收到吗？”，再看波形从头盔返回，盔旁的牌子回答“收到，水面。”两处都标了“本展模拟对话”。结束时电缆留在原处，访客可以再看一遍，也可以走开。

[出生点前后对照](../../artifacts/helmet-interaction/qa/runs/20261118-surface-call-a3fb217/opening-comparison.jpg) · [静音单手连续录像](../../artifacts/helmet-interaction/qa/runs/20261118-surface-call-a3fb217/surface-call.mp4) · [逐帧输入与场景状态](../../artifacts/helmet-interaction/qa/runs/20261118-surface-call-a3fb217/sample/observation.txt)

## 从画面选方向

| 本轮改前实看 | 策展判断 |
| --- | --- |
| 出生点的铜圈框住 Mark V，右边整块欢迎文字及三个语言键与它同高。[原图](../../artifacts/helmet-interaction/qa/runs/20261118-surface-call-a3fb217/baseline/02-spawn-zh.png) | 铜圈让人知道看谁。把唯一的问题移到盔旁，语言入口收成左上方的三键导向牌。 |
| 坐低后，台沿几枚操作键占满前景。头盔右侧留有与真实物件并列的空间。[中央低眼位](../../artifacts/helmet-interaction/qa/runs/20261118-surface-call-a3fb217/baseline/03-center-seated.png) | 把动作放在示意电话的木座上。旋转电话到能看见送话器和插座的角度，把铜键放低，让线从它上方经过。 |
| 左台的小头盔前有一圈窗框，能转动观察。右台以铜色和青色两根通道配“供气／通话”展签。[左台](../../artifacts/helmet-interaction/qa/runs/20261118-surface-call-a3fb217/baseline/04-left-exhibit.png) · [右台](../../artifacts/helmet-interaction/qa/runs/20261118-surface-call-a3fb217/baseline/04-right-exhibit.png) | 左台提示了一条实物观察路线；右台则给了可在中央演活的通信线索。 |

**协作方向，已制作。** 让访客在水面主动找潜水员。复用 Mark V、铜圈、电话、真实场景接头和既有 VOICE 往返时序，新增同座呼叫键、提问展签及两端情境字幕。选它是因为一句话发出后，观众有理由从电话移眼看头盔。代价是，把观察不同年代造型的时间留给了自由探索。

**实物方向，备选。** 先看两顶头盔的护栏和观察窗，单手转到侧面，任选一扇窗。选中后该窗轮廓在一张并排剪影上亮起，让访客指出两件展品的开窗位置差异。可复用两台现有模型和旋转操作，需要新增配对轮廓及选窗反馈。本轮只比较了可见外形。

## 打开与复跑

1. 用 Unity **2022.3.62f3c1** 打开 `E:\Codex\worktrees\xrmuseum-creative-opening-20261118\XRmuseum\MuseumVR`。在 Project 中双击 [SurfaceCall.unity](../../MuseumVR/Assets/Museum/Features/SurfaceCall/SurfaceCall.unity)，按 Play。已有资产可直接打开。
2. 看向头盔右侧。用一只手的射线指住铜键，按住握持键再松开；按住后移开可取消。桌面模拟器中点 Game 窗口，Y 操控右手，T 操控左手，G 按住／松开。可用左上语言键，或 F6／F7／F8 切换简体、繁体、英文。原有观察、旋转及供气仍可自由选择。
3. 自动复跑需退出占用此副本的 Editor。在副本根目录用下列 PowerShell 调用，两个空输出目录由夹具创建。结束码和原始素材保存在本次[进程记录](../../artifacts/helmet-interaction/qa/runs/20261118-surface-call-a3fb217/process.json)及同目录的两份 observation 中。

```powershell
$editor = 'E:\unity\36version\2022.3.62f3c1\Editor\Unity.exe'
$project = (Resolve-Path MuseumVR).Path
$run = Join-Path (Resolve-Path .) ('tmp\surface-call-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run | Out-Null
foreach ($mode in 'Baseline','Sample') {
    $argsForEditor = @('-batchmode','-force-d3d11','-projectPath',$project,'-logFile',"$run\$mode.log",
        '-executeMethod',"Museum.SurfaceLine.Editor.SurfaceCallEvidence.Capture$mode",'-surfaceOutput',"$run\$mode")
    if ($mode -eq 'Sample') { $argsForEditor += @('-surfaceFrames',"$run\frames") }
    $p = Start-Process -WindowStyle Hidden -FilePath $editor -ArgumentList $argsForEditor -PassThru -Wait
    if ($p.ExitCode -ne 0) { throw "$mode exited $($p.ExitCode)" }
}
```

## 版本与证据

- 本地分支 `codex/creative-opening-20261118`。开工基线 `383949843abfb547a0fe256ec1d0a7d8bfda0d86`，已 fetch 的 `origin/main` 为 `6580c924`。展开并逐项核对 93 个 LFS 实体的 SHA-256 后启动 Unity。
- 场景制作 `7997b84ddeff45c08ceae7532286e93d33d0dc8c`。受测 `a3fb217a434937aedd202fc28d7acf2b81e9fa3c` 增补输入事件记录。两次运行的 Git 工作区在进入和退出时均干净。对照场景 MuseumStart 与基线的 Git blob 均为 `7b54243b9a682d47b43da1e801d8d387323c0002`。主 checkout 在开工时已有项目设置和未跟踪资料，本轮全部写入独立副本。
- 场景、行为及重拍夹具在 `E:\Codex\worktrees\xrmuseum-creative-opening-20261118\XRmuseum\MuseumVR\Assets\Museum\Features\SurfaceCall\`。共享代码只增设 VOICE 自供字幕开关，样板开启；12 条新增三语文案进入现有 JSON 及保存表，全部使用既有字形。[共享演示](../../MuseumVR/Assets/Museum/Features/HelmetExploration/Runtime/HelmetConnectionDemonstration.cs) · [文案源](../../MuseumVR/Assets/Museum/Features/Localization/Tables/MuseumTranslations.json)
- 全部新证据在 `E:\Codex\worktrees\xrmuseum-creative-opening-20261118\XRmuseum\artifacts\helmet-interaction\qa\runs\20261118-surface-call-a3fb217\`。Windows、RTX 3070 Laptop、D3D11、Play Mode 单眼 1600 × 1000、垂直视角 55°。原版和样板的出生点均为 (0, 1.36, 2.20)，动作段眼高 1.25 m。短片采集了同一次操作的 123 个单眼画面，从首到末相距 20.536 秒；依实际采样时刻编码。单眼画面取自当帧 Camera.main 位姿。侧台通过输入设备位姿直接取景。简报内 11 月 10 日的历史图仅作参考。
- 输入系统注入追踪头部和右手设备状态，使用场景中原有 XRI 射线。按住时字幕为“松开通话”，确认数为零；在原键松开后确认数增至一。伸手移开的独立取消检查保持零次呼叫。发送、等待、回复、静态双向线路均留图，重播及减少动画另测。静音状态下器材音源未播放。两次 Editor 均自然退出 0。
- 已逐图检查简体、繁体及英文的发话、回复和结尾。保留等待对端的停顿；删掉重复背景提问和重复的通话阶段牌。进一步打磨时，先处理坐姿下方旧操作排的视觉重量，以及从左展台回看时语言牌与小铜圈的叠影。真实头显的清晰度、单手触达与舒适度，以及初访者能否自主发现并复述，仍待实测。
