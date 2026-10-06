# 2026-10-06：修复主线头盔热点接入

关联 [Issue #20](https://github.com/Twispra/XRmuseum/issues/20)。用户明确授权使用自己的账号修复并 commit；负责人 Twispra，Codex 辅助实现、自动验证和记录。基于 fetch 后的 `origin/main = d84c93f352541949db246440adb857361ec80ecd`，分支 `codex/fix-helmet-hotspots-2026-10-06`，没有未合并依赖。

## 完成的修复

- 热点访问状态从 Tour 的同一份记录读取；保存 Prefab 无需调用 Configure 也会订阅复位，禁用、重新启用、换绑和同 ID 重配置后，计数与颜色一致。
- 为触发碰撞体提供真实的表面距离，解决近距离选择时被整盔抢选的问题。前窗标记移至窗沿左侧，保留原窗口直接操作和射线操作。
- 保留现有程序集边界，通过文字事件和本地化适配器更新动态文案；追加 `hotspots.*` 九条三语文案，保存表共 202 条，原 193 条内容不变。覆盖初始提示、三个详情、0–3 进度与完成提示。
- 源字体保留原 7651 个码位并补入 8 个繁体字，静态图集补烤至 533 字符；保留字体、图集、材质和 Prefab 的原 GUID／子资源引用。字体来源和 OFL 许可沿用现有 Noto Sans SC 记录，未引入新外部字体。
- 热点卡片移到台沿前方、头盔下方。修正布局中的溢出和台面遮挡，保留 `.28` 字号下限；检查默认相机中的文字范围、实体遮挡，并人工复核三语截图。

## 实际验证

环境：Windows、Unity **2022.3.62f3c1**、XRI 2.6.4、Input System 1.14.0、TMP 3.0.7、OpenXR 1.14.3，Direct3D 11。许可证可用；同一隔离工程串行运行。

| 验证 | 结果 | 实际退出码 |
| --- | --- | --- |
| 热点 NUnit／UnityTest，含保存 Prefab 的真实 Play Mode 生命周期 | 12 / 12 | 0 |
| 主场景三语与输入集成验收 | 82,319 / 82,319 | 0 |
| 其中新增热点输入、三语和可见性检查 | 381 / 381，已计入上一行 | 同主场景进程 |
| 本地化模块 | 11 / 11 | 0 |
| 原有探索回归 | 1,469 / 1,469 | 0 |

主场景和探索报告的 errors 均为 0。热点检查通过生产 Input Actions 和真实 Interactor 回调，分别验证三个热点的 Direct／Ray 选择、原窗口 Direct／Ray 开启、整盔抓取及持握越过热点；没有用 SelectEnter／SelectExit 直接伪造选择。语言状态、公开 Reset API 和禁用操作是明确的测试操作。

被测源码是上述基准提交加本修复的未提交文件；验证后核对了主场景报告中 **257 个文件哈希**，全部与磁盘一致。`Packages/<包名>` 的虚拟路径按 packages-lock 中版本解析到本机 PackageCache 核对。修复文件及测试源码另有独立哈希清单，此记录和证据随修复提交保存。

- [验证摘要](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/validation-summary.json)、[实际进程与命令](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/processes.json)
- [候选修改文件哈希](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/candidate-files.json)、[主场景被测文件哈希](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/tested-files.json)
- [热点检查明细](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/hotspot-interaction.json)、[12 项测试 XML](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/tests-final.xml)
- [字体像素、保存字节和身份核验](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/font-bake-summary.json)、[源字体扩充](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/source-font-expansion.json)
- [默认相机简体截图](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/selected-zh-production.png)、[繁体截图](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/selected-zh-hant-production.png)、[供气演示期间](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/diagram-zh-production.png)
- 完成提示诊断近景：[简体](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/hotspots-direct-progress-3-Chinese-diagnostic.png)、[繁体](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/hotspots-direct-progress-3-TraditionalChinese-diagnostic.png)、[英文](../../artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/hotspots-direct-progress-3-English-diagnostic.png)。诊断近景使用单独相机，不冒充访客视角。

完整大报告、过程日志、初期失败记录及字体备份保留在本机独立目录 `E:/XRmuseum/artifacts/helmet-interaction/qa/runs/20261006-hotspot-fix/` 和隔离工作树相应运行目录。初次新测试因 Play Mode 域重载重复初始化失败，已修正并实际复跑；卡片曾在视野下方、完成提示溢出、被台沿遮挡，均经后续验证修复。历史报告未覆盖。

## 复跑和交接

在已展开 LFS 资产的工程中，依次运行 Unity 批处理（每条等待实际退出）：

1. `-runTests -testPlatform EditMode -testFilter Museum.Hotspots.Tests -testResults <独立路径>`，不要使用 `-runSynchronously`。
2. `-executeMethod Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch`
3. `-executeMethod Museum.Localization.EditorTools.LocalizationValidation.RunBatch`
4. `-executeMethod Museum.Exploration.Editor.HelmetExplorationValidation.RunBatch`

公共参数为 `-batchmode -projectPath <MuseumVR> -logFile <独立日志>`。有截图或由验证器管理结束的入口不加 `-nographics`／`-quit`。新增 `HelmetHotspotAssetRepair` 是有备份的显式修复工具；日常预览和验证直接使用保存资产，不运行修复工具或展厅生成器。

Issue #20 记录本轮单一写入者和路径；主场景、公共 Input、Packages、ProjectSettings、模型及其 meta 均未修改。用户主 checkout 的原 `ProjectSettings.asset` 改动保留，SHA256 仍为 `81C0EB3103E303A2567619E388C45709E89FA1940353CDFE4A163BBE402C5587`；原未跟踪记录未纳入此提交。

本轮未验证真实头显、人体舒适性、可执行构建、用户研究或历史准确性。字形射线检查覆盖字形中心到相机之间的实体 Collider；无 Collider 的遮挡仍依靠截图复核。此前已有的其他菜单遮挡不属于本次修复。下一步由另一位成员依据提交和证据审查，再决定合并；本次不自动合并 main。
