# 2026-10-06：PR #14 双语入口补齐与合并验收

关联 [PR #14](https://github.com/Twispra/XRmuseum/pull/14) 与 [Issue #17](https://github.com/Twispra/XRmuseum/issues/17)。用户明确授权补齐后合并 #10、#13、#14 并同步本地。本轮由当前 Codex 作为实际整合写入者，在 Issue #17 认领主场景、字体及本地化资产；另两路代理只读复核代码、引用和证据，不作为成员人工批准。

## 版本与范围

- 开工用户 checkout：`E:\XRmuseum`，main `b40eb9f652a3935595575e6a5ec0f287c09777b5`；已有 ProjectSettings.asset 和本地审查记录改动保留。
- 实施 checkout：`C:\Users\zhaoooooo\.codex\worktrees\review-pr3\XRmuseum`，分支 `codex/pr14-completion-2026-10-06`。
- 本轮已先合并 #10（`175f4787eea8b4e08dfdc3d02d6a719abe8ee27d`）和 #13（`9457f9bbcae6de263753caa75e7a6c53378ff163`）；#14 原 head 为 `d4f7fbaf10a230b08193f57365ba51979912c911`。`fd603988` 将实际 main 合入 #14，保留两边文档。
- 最终产品修改、主场景及字体的被测提交为 **`4d84e175b3d6ec9a0721dba5224419a01c39ee5d`**。后续提交只补证据、说明及许可文本，产品代码、场景、字体二进制/YAML 不再改变。
- 保留 PR #9 的英文文案、默认隐藏状态及演示期间的文字避让。没有改 Unity 版本、Packages、ProjectSettings、XR 输入配置或其他功能模块。

## 补齐内容

保存表共 21 条，原模块 17 条保留，旧三条入口 key 换为四条当前入口文案；Setup 种子与保存资产一致：

| 场景对象 | key | English | 中文 | 默认 activeSelf |
| --- | --- | --- | --- | --- |
| Gallery masthead | museum.sign.masthead | Beneath the surface | 海面之下 | true；演示期间隐藏 |
| Gallery subtitle | museum.sign.gallery | THE DIVING GALLERY | 潜水展厅 | false |
| Arrival brand | museum.sign.arrival_brand | The diving gallery | 潜水展厅 | true |
| Arrival introduction | museum.sign.intro | One object. Many ways to explore. | 一件展品，多种看法。 | false |

移除 Museum Title / Museum Subtitle / Welcome 的旧绑定，保留其隐藏策略。语言面板维持 `(2.6, 0, 6.2)`；场景和环境 Prefab 既有 Transform、Camera、物理组件及 main 的 43 个 override 均保留。四组字体/材质覆盖显式保存，避免只在当前 Editor 内存中有效。

整合工具只处理明确对象和 key，先完成全部前置检查、备份，再改绑定；未知绑定、错误文案或显隐状态会拒绝覆盖。日常预览直接打开保存场景，不运行 LocalizationSetup 或 HelmetPrototypeSetup。工具移除操作的范围及限制见[模块说明](../localization.zh-CN.md)。

字体沿用源 TTF（SHA256 `c4d3ade882e5c42147016e889b2865d9daef6bf507e1a10dc4768630072d6d4f`），以 TMP 3.0.7 Creator 底层 FontEngine 路径重烤。190 → 198 字符，只增加「之厅多水法海潜种」，无旧字符丢失；110 点 / Padding 6 / 2048² Alpha8 / SDFAA / Static。保留 `.meta`、GUID `73d312c39c8c91948afde3c8604a6c94`、atlas localFileID `-7840310081897550815` 和 material localFileID `-3044848435321221505`。生成缓冲与磁盘保存图集 SHA256 一致，八个新字均有真实 SDF 像素。一次性生成脚本只作为证据存档，不导入 Assets。

TTF name 表内嵌 Adobe 2014–2021 版权、保留字体名 `Source` 及 OFL 摘要，但没有许可全文。本轮从 [Google Fonts 的 Noto Sans SC 官方目录](https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/OFL.txt)补入[完整 OFL](../licenses/noto-sans-sc-OFL.txt)，版权行与 TTF 一致；纠正旧资源说明的“没有保留字体名”。源 TTF 未修改。

## 验证与证据

环境：Windows、Unity **2022.3.62f3c1**、Direct3D 11、XRI 2.6.4、Input System 1.14.0、OpenXR 1.14.3、TMP 3.0.7。编辑器许可证有效。打开隔离工程前已展开 LFS 工作区资产，未以指针文本代替 FBX。

- [字体报告](../../artifacts/localization/qa/runs/20261006-font-completion/report.json)、[实际进程退出](../../artifacts/localization/qa/runs/20261006-font-completion/process.json)、[辅助脚本](../../artifacts/localization/qa/runs/20261006-font-completion/FontCompletion.cs)、[新增字形诊断图](../../artifacts/localization/qa/runs/20261006-font-completion/eight-new-glyphs-rgb-diagnostic.png)：198 字符、引用和持久化像素核对通过，退出码 0。备份及完整日志仅留本机。
- [主场景本地化报告](../../artifacts/localization/qa/runs/20261006T010119421Z-integration-e97027ffdaf44f0a8a6ee720a6fe035f/report.json)、[进程退出](../../artifacts/localization/qa/runs/20261006T010119421Z-integration-e97027ffdaf44f0a8a6ee720a6fe035f/process.json)：`-batchmode -projectPath MuseumVR -executeMethod Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch`；**627/627 断言通过，errors/warnings 均为空，实际退出码 0**。其中 232 项为源文件完整性检查，不代表 627 次独立用户操作。
- 双手控制器/HMD状态在正常 Dynamic Input 更新中排队，走生产 Input Actions、XRRayInteractor hover/select 及保存的 LanguageButton.selectEntered；没有直接调用 SelectEnter 或 TriggerProgrammatically。探索演示开始/停止另用 public API，单独标记。验证英文初始状态、中文/英文切换、演示中切换语言及结束后恢复、隐藏对象再次启用时刷新、两个默认隐藏文案仍隐藏。
- [保存模块报告](../../artifacts/localization/qa/runs/20261006T010535039Z-validation/report.json)：LocalizationValidation **11/11 通过**；随后调用既有 HelmetExplorationValidation.RunBatch，[主线回归报告](../../artifacts/helmet-exploration/qa/runs/20261006T010535493Z-exploration-76dde6a6b9b54597b99b4a183688d737/report.json) **1469/1469 通过、errors 为空**。两者由[临时串行 runner](../../artifacts/helmet-exploration/qa/runs/20261006T010535493Z-exploration-76dde6a6b9b54597b99b4a183688d737/CompletionRegression.cs)在同一进程执行，2026-10-06 01:05:05–01:08:16 UTC，[实际退出码 0](../../artifacts/helmet-exploration/qa/runs/20261006T010535493Z-exploration-76dde6a6b9b54597b99b4a183688d737/process.json)。回归关键截图随报告保存，其余原始截图及完整日志留本机；未重跑与本次改动无关的全部拿放/剖切套件。

本地化验收开始时 tracked 产品文件干净；存在此前独立 QA 目录和活动记录的 untracked 文件，完整清单已写入报告。回归阶段仅额外导入临时 `Assets/Museum/Editor/CompletionRegression.cs`，串行调用保存资产校验与原有探索验证，退出后移除自身及其 meta；脚本作为证据保存在 Assets 外。

单眼截图实际检查了[默认英文](../../artifacts/localization/qa/runs/20261006T010119421Z-integration-e97027ffdaf44f0a8a6ee720a6fe035f/initial-en-production.png)、[中文入口](../../artifacts/localization/qa/runs/20261006T010119421Z-integration-e97027ffdaf44f0a8a6ee720a6fe035f/selected-zh-production.png)、[中文 Arrival 近景](../../artifacts/localization/qa/runs/20261006T010119421Z-integration-e97027ffdaf44f0a8a6ee720a6fe035f/selected-zh-arrival-closeup.png)、[演示期间](../../artifacts/localization/qa/runs/20261006T010119421Z-integration-e97027ffdaf44f0a8a6ee720a6fe035f/diagram-zh-production.png)及[恢复中文](../../artifacts/localization/qa/runs/20261006T010119421Z-integration-e97027ffdaf44f0a8a6ee720a6fe035f/restored-zh-production.png)。字体无方框，文案状态正确。近景相机是单独标注的诊断视角，不冒充玩家视角。

## 限制与交接

原布景圆环会遮住主标题下沿，固定玩家视角中 Inspect 面板可遮住标题左侧；演示模型与右侧语言面板在部分视角投影重叠。本轮没有改动它们的空间位置，因此不声明全视角无遮挡或面板动线已完成人工确认。真实头显的清晰度、可达性、舒适性、性能及最终布置仍需成员实测。

未做真实头显/手柄、系统键鼠模拟器操作、用户研究、无障碍评价或可执行构建；启动时 OpenXR 无可用头显，后续测试使用自动输入夹具。其余模块仍有英文文案，不代表全馆已双语化。

已完成 Issue #17 所列保存表、缺字、当前主场景绑定和回归证据补齐；下一步按用户授权推送本 PR 原分支、将 base 明确改为 main、校验 head 后合并，最后快进同步用户 main。实际合并结果以 GitHub PR 状态及合并后本地记录为准。共享资产在 Issue 完成评论中释放；后续实机布局调整另行认领。

AI 辅助范围见[外部资源记录](../external-resources.md)。课程规则、产品方向及全组审批要求没有被改写；不代替成员个人反思或设备验收。
