# 2026-10-13：热点悬停修复与两项 PR 合并

## 授权、版本与范围

用户在本轮明确授权：修复 [PR #46](https://github.com/Twispra/XRmuseum/pull/46) 的悬停问题，验证通过后合并；[PR #47](https://github.com/Twispra/XRmuseum/pull/47) 合入当前资产目标。本轮沿用 Issue #45 / #25，不另建功能任务。没有更改远程保护、账号权限或伪造另一成员的 APPROVED Review。

#46 原贡献 `3ae4fc6` / `heziyi-aragon` 保留，本轮维护者使用现有 Git 身份 `twispark` 提交审查修复（Codex 辅助），不将原功能贡献归为合并者。修复分支本地为 `codex/pr46-hover-state-fix-2026-10-13`，从原 PR head 开始，最终以普通 fast-forward push 更新既有 `codex/hotspot-state-accessibility` PR，不强推。

- 开工实际 main：`37930ae235b6bfd00f9575805c759614a0cec6a4`。
- #46 原 head：`3ae4fc6ae5d6fef6258888431634033f29ee2973`；修复代码与测试提交：`a0df95bb5489f5ccbb2e7866f7790cd576a8e9e5`。
- #47 head：`85879660c63aec04d528323160f3278877c63902`；资产目标原基准：`169c643cac3c94d12f8c1cad75894bc2db2c9ab9`。
- 原 `E:\XRmuseum` checkout 保持在 `codex/project-audit-2026-10-11` / `ac3d31c`；开工已有 ProjectSettings.asset 与未知未跟踪资料均保留，未纳入推送。

产品修改仅两份既有热点 C#：`HelmetHotspot.cs` 与 `HelmetHotspotTourTests.cs`。没有重建原型、重存场景/Prefab、更改模型/材质、GUID、公共 XR/Input、Packages、ProjectSettings 或访客文案。实质记录、状态摘要与精选验证证据单独提交。

## 修复与回归

删除热点自己的 `isHovered` 缓存；显示外环与状态色时直接从 `isActiveAndEnabled && interactable.isHovered` 读取 XRI 聚合状态。一个 interactor 离开时，其他 interactor 的悬停仍保留；最后一个离开才清除外环。组件重新启用会从现有 XRI 状态恢复，禁用时显隐调用不会重新开启悬停外环。

原状态测试改为使用真实 `XRInteractionManager.HoverEnter/HoverExit`，检查访问、外环、显隐和复位共存。另补两项长期回归：双射线部分/最后退出，以及保持 XRI 悬停时重启组件回调。生命周期用例在 EditMode 直接调用回调，明确不把它等同于真实设备操作。只读子任务复审没有发现新增阻碍。

上一轮临时复现探针已保存到原独立审查运行目录，并仅删除该探针及其生成 meta；没有清理未知资产或覆盖原失败报告。

## 验证版本、命令与限制

使用隔离工作副本 `C:\Users\zhaoooooo\.codex\worktrees\pr46-review-2026-10-13\XRmuseum`。打开 Unity 前再次确认 MuseumVR 内 LFS 资产全为已展开对象；沿用 Unity **2022.3.62f3c1** / Windows / Editor。Library 缓存位于 E 盘本地缓存目录，未进入 Git。

被测源码提交为 **`a0df95bb5489f5ccbb2e7866f7790cd576a8e9e5`**；EditMode 运行前/后 `git status --short` 均为空，没有额外未提交探针。运行命令使用 `Unity.exe -batchmode -projectPath <隔离 MuseumVR> -runTests -testPlatform EditMode -testFilter Museum.Hotspots.Tests -testResults <新目录>/hotspot-tests.xml`。结果 **21/21 passed、0 failed、0 skipped，退出码 0**。

新证据目录：[20261013-pr46-hover-state-fix](../../artifacts/helmet-interaction/qa/runs/20261013-pr46-hover-state-fix/)。主场景验证运行 `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch`，不使用 `-quit` / `-nographics`，不生成或保存产品资产。相同干净源码提交通过 **81,849 项检查、0 errors、0 warnings，退出码 0**；339 个输入文件的运行前后哈希一致。热点子流程 **786 项检查、20 次 Direct/Ray 选择、6 张三语单眼诊断图**通过。报告的声明与局限见精选 `hotspot-interaction.json`、`summary.json` 和 `source-files.json`；完整主场景原始报告（SHA256 见摘要）、运行日志及进程清单只留本地。

运行结束仅增加新 QA 输出，无被测产品资产的未提交改动。后续提交的文档/证据不会改变 `a0df95b` 的两个源码文件；推送前再核对文件差异和远程目标。

本轮没有真实头显、双眼立体、手柄物理触达、色觉用户研究、舒适度、性能或当前 Windows 构建验收。自动测试通过只对应所记版本与覆盖范围；没有用旧报告冒充本次主场景重跑，也没有将大量逐帧/逐字符断言算成独立用户操作。

## 合并与交接

#47 已于北京时间 **2026-10-13 18:51** 合入 **`codex/helmet-refinement-assets`**，merge SHA 为 **`39e895f9c5bd449cccd8c02c41fdb4f2c2eb423b`**。GitHub 回读 `merged=true`；fetch 后确认原 head 为资产分支祖先、不是 main 祖先。没有改变 #47 的目标，也未将其资料直接接入产品。

#46 在验证通过后更新既有 PR 并按用户授权合入 main，最终 merged 状态、head 与 merge SHA 以该 PR 的 GitHub 回读为准。没有删除成员分支。下一步仍是实际成员在设备上检查外环/访问轮廓的可读性，并按 #25 确认早期建模候选；#25 整体任务保持开放。

无新增外部资源。Codex 辅助范围为已有热点运行时修复、测试、复审、证据与合并记录，沿用已有程序辅助声明；成员审核和课程 AI 许可不由本轮自动验证代替。
