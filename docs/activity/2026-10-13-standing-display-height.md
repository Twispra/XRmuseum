# 站姿反馈后的中央展示高度调整

关联 [Issue #48](https://github.com/Twispra/XRmuseum/issues/48) / [PR #49](https://github.com/Twispra/XRmuseum/pull/49)。用户明确补充：**站着游玩，地面正常，展品太高**；前轮已反馈头显旋转正常。因此本轮保留设备眼睛高度和 Floor 跟踪，降低中央头盔展示区域。20 cm 是本轮用于设备复测的初始布局调整，不是用户研究或适用于所有身高的结论。

## 版本、范围与保存改动

基于前轮修复分支 `deed268`，实际 origin/main 仍为 `4578e66`，已 fetch。#48 扩大并记录共享场景范围，主代理为唯一场景写入者；子代理只读复审，另一子代理只写新的独立 Editor 验证脚本。开放 #25 / Draft #28 为模型资产工作，不涉及本轮共享场景。原目录的 XR 修复本地镜像、ProjectSettings 既有改动及未知输出继续保留。

保存 `MuseumStart.unity` 改变六个原值并新增一个场景覆盖：中央展台组、Helmet Interaction Station、Helmet Era Controls 的 Y 各为 −0.20 m；中央 Plinth 局部中心 Y 改为 0.65 m、高度为 0.90 m，使其世界底部保持 0、顶部为 0.90 m；头盔的显式世界复位 Y 改为 0.9719999 m。桌面盖板仍厚 0.06 m，随中央组整体下降，台面顶高 **1.16 → 0.96 m**。头盔局部姿态、模型比例、热点、前窗铰链与台面的相对位置不变；复位按钮和说明牌随工作站下降，个人面板继续按原逻辑相对访客摆放。两个侧展台保持原高度。

首轮主场景回归发现热点信息卡也下降后，文字矩形离开原生产相机视野。修复提交 `1633e98` 只对场景内热点 Prefab 的 `Information Panel` 增加局部 Y=0.21 m 覆盖（原 0.01 m），保留信息卡原世界阅读高度；没有移动整个 Tour，热点仍跟随真实模型。原相机视野和文字遮挡检查均保留，失败报告/日志留本地。

新验证脚本：`Assets/Museum/Features/XRTracking/Editor/HelmetDisplayHeightValidation.cs` / 独立 meta。既有 `RefinedModelValidation` 现在明确选择中央 `Exhibit 02 - Placeholder/Plinth`，不再从三个同名 Plinth 中任取一个来判断 SuperLite 支撑高度；原断言保留并新增目标存在检查。

没有修改 XR Origin、相机、地面、公共输入、packages、ProjectSettings、模型/Prefab、GUID、材质或访客文案。没有重跑任何模型/原型/整合生成器，没有改动或覆盖原 SuperLite 安装校准报告。历史安装验收的布局保护仍然有效；之后若重新安装模型，应基于新布局另做校准，不能绕过旧保护。

## 本轮验证

初次高度/精细整合被测版本 `ac421556ecf64e05448a1689805fccde1a42a50f`；最终高度与三语主场景被测版本 `1633e98a8e85a431abba5a0ff73218d10f17fa9d`。Unity **2022.3.62f3c1** / Windows / Editor，独立工作副本。测试时没有额外已跟踪源码改动；最终高度测试的额外已跟踪 dirty 为本轮 external-resources / getting-started 文档，另有未跟踪的活动草稿与本地 QA 输出，报告保留完整列表。模型沿用前轮已展开的实际 LFS 资产，未重建 meta。

- 最终高度专项：`Museum.XRTracking.EditorTools.HelmetDisplayHeightValidation.RunBatch`，**65 项检查通过，0 errors / 0 warnings，退出 0**。验证中央台面 0.96 m、底部贴地、两侧台面 1.16 m、保存 home 与初始世界位置一致；正常物理沉降、公开即时复位与平滑返回 API、三年代可见状态/抓取组件/比例、XR Origin 保持不变及源码/资产未意外保存。[报告与来源指纹](../../artifacts/helmet-interaction/qa/runs/20261013T151317398Z-display-height-1c0e8efb59534ec5b581050af36b3f61/report.json)。
- 精细整合（`ac42155`）：`Museum.RefinedModels.EditorTools.RefinedModelValidation.RunBatch`，**423 项检查通过、0 errors、退出 0**。确认 Mark V / SuperLite 表面热点、窗口、剖面、Ray 输入与平滑返回等受影响功能，SuperLite 支撑明确核对中央台面。[报告](../../artifacts/helmet-interaction/qa/runs/20261013T151317398Z-display-height-1c0e8efb59534ec5b581050af36b3f61/refined-integration.json)与进程记录保留；原始诊断截图留本地。之后仅新增信息卡 Y 场景覆盖，其三语/输入/遮挡行为由最终主场景回归覆盖。
- 最终三语保存主场景：`Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch`，**83,424 项检查、0 errors / 0 warnings、退出 0**；含 786 项热点检查 / 20 次 Direct/Ray 选择、信息卡相机视野及文字遮挡检查。[摘要、原始报告哈希与来源指纹](../../artifacts/helmet-interaction/qa/runs/20261013T151317398Z-display-height-1c0e8efb59534ec5b581050af36b3f61/main-regression-summary.json)、[热点子报告](../../artifacts/helmet-interaction/qa/runs/20261013T151317398Z-display-height-1c0e8efb59534ec5b581050af36b3f61/hotspot-interaction.json)。计数含重复文字/字形断言，不算独立用例；完整重复报告/截图及失败日志留本地。
- 三张 1.60 m 站姿单眼截图已目视初检，最终 Mark V 站姿图也已回读。中央模型与台面衔接未见本轮新增错位。截图使用临时相机复制生产视角的水平位置/朝向/FOV，眼睛 Y=1.60 m 是合成夹具，不是用户实际身高；没有移动真实 XR 相机。[Mark V 站姿图](../../artifacts/helmet-interaction/qa/runs/20261013T151317398Z-display-height-1c0e8efb59534ec5b581050af36b3f61/standing-1.6m-era-1.jpg)。年代 0 仍是原占位展示，不能因三年代测试通过写成三顶精细历史模型完成。

## 交接与限制

已将本轮 4 个源码/场景文件的精确增量补丁同步到用户 `E:\XRmuseum`，逐文件规范化 Git blob 相同；原 ProjectSettings SHA256 前后均为 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`。[同步记录](../../artifacts/helmet-interaction/qa/runs/20261013T151317398Z-display-height-1c0e8efb59534ec5b581050af36b3f61/local-application.json)。这是 main checkout 的未提交本地镜像，权威版本仍在同一工作分支，没有提交/合并 main。

用户退出 Play、重新载入保存的 MuseumStart，通过相同 Virtual Desktop / Quest 3 路径检查观察高度、前窗、年代按钮、拿放和复位。若仍需要微调，记录具体观察部位及体验，继续调整中央展示布局。

本轮没有再次取得真实头显高度体验、双眼/舒适性、用户研究、实机性能或可执行构建证据；公开 API 的复位测试与既有合成 XR 输入回归分别标明。真实设备的地面与转头在用户反馈中正常，不因布局调整虚构重新验收通过。原有 frame submission 错误仍单独待设备复查。PR 仍待成员审阅和设备回读，未自动合并。
