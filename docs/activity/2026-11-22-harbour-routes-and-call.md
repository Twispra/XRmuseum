# 正式展厅的右台路线图与中央水面电话

本轮以 `main@16b397f4` 为基线，按任务授权采用右台方案 B，将两项既有内容接入正式 `MuseumStart`。右台保留 [PR #87](https://github.com/Twispra/XRmuseum/pull/87) 的提交历史和展板 Prefab，停用被替换的两块旧前签。中央采用 [PR #84](https://github.com/Twispra/XRmuseum/pull/84) 的电话装置与情境对白，沿用现有 Mark V 设备、线缆和双向通话时序。[方案记录 #64](https://github.com/Twispra/XRmuseum/issues/64) · [右台任务 #77](https://github.com/Twispra/XRmuseum/issues/77)

## 参观路线

1. 前往右台。上行的单箭头从水面供气泵指向潜水头盔，下行的双箭头连接水面团队与潜水员。底部指引返回中央 Mark V 展台。三种语言保留相同的箭头形状和端点关系。
2. 前往中央，选择 Mark V。按住电话座铜键会看到按下反馈。原位松开后，问话先显示在水面端，随后头盔旁出现回应。访客可在静音状态下读完对话。面前的头盔仍可切换年代，回到 Mark V 即可再使用电话。
3. 在无头显的 Windows 窗口，主键盘 **3 / 2** 分别前往右台与中央，**C** 操作同一枚铜键，**F6 / F7 / F8** 切换简中、繁中、英文。头显中用控制器指向参观导航或电话铜键，按住握把并松开。Windows 完整 ZIP 中附有启动与操作说明。

## Windows 窗口实测

从干净的联合候选提交 `ad3b4978` 使用 Unity 2022.3.62f3c1 构建 Windows x64，在无头显的 2560×1440 程序窗口用键盘参观正式场景。三种语言都能读到两端名称，金色供气箭头朝头盔，青色通话箭头朝向两端。[三种语言的窗口实拍](../../artifacts/helmet-interaction/qa/runs/20261122-harbour-routes-call/right-routes-three-languages.jpg)

在中央按住铜键，亮灯并出现松开提示。松开后先看到水面问话，随后看到潜水员回答。英文和简体中文都走通，同一展位可再次发话。右台按 C 或按住铜键后切换年代均未误发。程序正常退出。[铜键、发话、等待和回应实拍](../../artifacts/helmet-interaction/qa/runs/20261122-harbour-routes-call/telephone-sequence.jpg)

## 修正坐姿视野后的验收

在右台用控制器快捷导航抵达时，原导航浮板遮住了坐姿视角中的“水面供气泵”。调整浮板至右上并下移电话端字幕牌后，在联合提交 `970de083` 的正式场景重新验收。

- 展板检查 1,102 项、展厅导航 145 项全部通过。导航保持显示，在 1.20、1.30、1.55 米眼高分别查看简中、繁中、英文。九组视角的 9 处文字及 11 个箭杆、箭头部件，在左眼、右眼和中线的模拟视线均无导航遮挡。[1.20 米右台视角](../../artifacts/helmet-interaction/qa/runs/20261122-harbour-routes-call/xr-right-seated.png)
- 正式场景电话验收通过 21 项。模拟控制器从悬停、按住到松开走完整条通话，原位松开只发送一次，移开取消，换代、返回、重播与复位均通过。简中、繁中和英文都显示两端对白。该轮将设备音效静音。[中央展台实拍](../../artifacts/helmet-interaction/qa/runs/20261122-harbour-routes-call/xr-telephone.jpg)
- 从同一干净提交重新构建并实际运行 Windows x64 程序。三语展板与三语发话、回应均可见，正常退出。47 个 Unity LFS 资产核过 SHA-256，构建前后 1,545 份工程文件内容一致。交付 ZIP 的主线提交、文件校验及解压运行结果见 [PR #98](https://github.com/Twispra/XRmuseum/pull/98)。本轮设备未连接实体头显。
