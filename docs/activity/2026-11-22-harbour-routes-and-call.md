# 正式展厅的右台路线图与中央水面电话

本轮以 `main@16b397f4` 为基线，按任务授权采用右台方案 B，将两项既有内容接入正式 `MuseumStart`。右台保留 [PR #87](https://github.com/Twispra/XRmuseum/pull/87) 的提交历史和展板 Prefab，停用被替换的两块旧前签。中央采用 [PR #84](https://github.com/Twispra/XRmuseum/pull/84) 的电话装置与情境对白，沿用现有 Mark V 设备、线缆和双向通话时序。[方案记录 #64](https://github.com/Twispra/XRmuseum/issues/64) · [右台任务 #77](https://github.com/Twispra/XRmuseum/issues/77)

## 参观路线

1. 前往右台。上行的单箭头从水面供气泵指向潜水头盔，下行的双箭头连接水面团队与潜水员。底部指引返回中央 Mark V 展台。三种语言保留相同的箭头形状和端点关系。
2. 前往中央，选择 Mark V。按住电话座铜键会看到按下反馈。原位松开后，问话先显示在水面端，随后头盔旁出现回应。访客可在静音状态下读完对话。面前的头盔仍可切换年代，回到 Mark V 即可再使用电话。
3. 在无头显的 Windows 窗口，主键盘 **3 / 2** 分别前往右台与中央，**C** 操作同一枚铜键，**F6 / F7 / F8** 切换简中、繁中、英文。头显中用控制器指向参观导航或电话铜键，按住握把并松开。Windows 完整 ZIP 中附有启动与操作说明。

## Windows 窗口实测

从干净的联合候选提交 `ad3b4978` 使用 Unity 2022.3.62f3c1 构建 Windows x64，在无头显的 2560×1440 程序窗口用键盘参观正式场景。三种语言都能读到两端名称，金色供气箭头朝头盔，青色通话箭头朝向两端。[三种语言的窗口实拍](../../artifacts/helmet-interaction/qa/runs/20261122-harbour-routes-call/right-routes-three-languages.jpg)

在中央按住铜键，亮灯并出现松开提示。松开后先看到水面问话，随后看到潜水员回答。英文和简体中文都走通，同一展位可再次发话。右台按 C 或按住铜键后切换年代均未误发。程序正常退出。[铜键、发话、等待和回应实拍](../../artifacts/helmet-interaction/qa/runs/20261122-harbour-routes-call/telephone-sequence.jpg)
