# PR #44：四项修复与合并前复审

范围：[PR #44](https://github.com/Twispra/XRmuseum/pull/44)，关联 #25。用户明确授权修复四项审查问题、重新审查，并在通过后直接合并 main；没有代替其他成员作人工 APPROVED 评价。原作者的 SuperLite 建模与接入提交保留。本轮 Codex 分工检查运行时、保存资产和验收代码，由同一整合任务统一保存、提交和运行 Unity。

开工基准：PR 原 head `86715c941875c851e5f7768acc6bc08cf62dd68d`；fetch 后 main `06e68e89871fdac278f64c2ec58a1df866291360`。修复使用独立 worktree，分支 `codex/superlite17b-unity-integration-2026-10-11`。原 `E:\XRmuseum` 的 `codex/project-audit-2026-10-11` / `ac3d31c`、已有 ProjectSettings 和未跟踪资料保留，未纳入修复。原 ProjectSettings.asset SHA256 为 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`。

## 问题和完成后的行为

1. **观察热点脱离模型、颈部沿用肩部说明。** 三个已有发现 ID 保留，SuperLite 点位绑定实际前窗、外壳和颈部密封表面，前窗点随打开面板移动。简体、繁体、英文改为对应部位说明，并向访客说明侧开属于观察演示。切回 Mark V 恢复原局部姿态和文案；已显示卡片刷新为当前年代，探索进度与解释计时保持。
2. **展签仍写 Mark V。** 新年代文案组件依据实际已应用的 `RefinedHelmetBinding` 状态更新展签。窗口或剖面被握持导致切换延后时，文案也等待实际模型切换；通过 MuseumText.Set 写入动态原文，避免逐帧覆盖语言结果。
3. **VOICE 把颈箍和铰链作为电话接头强调。** SuperLite 的电话机械高亮列表清空；切换绑定先清除旧高亮，通信演示保留原示意端点与路径。三语目的地标为“通信位置（示意）”，不声称已补齐实际 posts 接头。
4. **源线性颜色被二次转换。** 保存的八个非玻璃材质及导入工具先对 Blender `base_color_linear` 做 gamma 编码，再写 Unity 普通 Color 属性；玻璃和其他材质参数保持。新增 GPU 浮点回读对照源参数，并以原错误输入作为负对照。

产品修复提交 `31b12ab53587b7393c46e288b7b734fe2c27fe3b`。后续提交仅完善真实输入验收夹具和证据，不重新生成模型、场景或整盔。

## 保存资产与字体

[独立保存资产检查](../../artifacts/superlite17b-unity-integration/qa/runs/20261012-pr44-fix/asset-review.json)：原 PR Prefab 的 910 个对象 ID 全保留，新增一个文案组件；7 个原物理组件块保持。主场景保留原 753 个对象 ID，仅已有头盔 Prefab 实例增加该组件的 23 条引用配置和 7 个 stripped 引用，未替换其他场景内容。Mark V 绑定、原碰撞体、meta/GUID、剖面数据、FBX 与源模型保留。

三语表 205 → 210 条，新增 `refined.superlite.instructions`、`refined.superlite.hotspots.front_window`、`refined.superlite.hotspots.shell`、`refined.superlite.hotspots.neck`、`refined.superlite.communication_location`。字符清单初补齐为 548，压缩前窗说明后为 547；静态 SDF 536 → 558 字形，保留已补齐字形。源 Noto Sans SC 子集保留 7660 原码位，补入「側、僅、夾、環、輕、頸」至 7666；仍用本机同一 SIL OFL 1.1 字体的 Regular 400 实例。保持字体、材质和图集 GUID / 子资源 ID。[字体补齐](../../artifacts/superlite17b-unity-integration/qa/runs/20261012-pr44-fix/font-completion.json)、[源子集扩展](../../artifacts/superlite17b-unity-integration/qa/runs/20261012-pr44-fix/source-font-expansion.json)、[资源与 AI 范围](../external-resources.md)。

原 #43 v2 `.blend` SHA256 `54a21895b807a19ccc0345a25101cb5369c0a9c976d175a40a58a528a5b8b905`；两份实际展开的 runtime FBX 均为 `febfddaf522c50b0044237998c7a661b1bbb1d1220fb07588cfc3e23f5718fda`。没有运行旧 HelmetPrototypeSetup.Build、Mark V / SuperLite 安装器或 Blender 原型生成器。范围明确的临时保存脚本仅配置新增组件、表和字体，源文件与执行结果另存证据，已从 Unity 工程移除。

## 验证与复审

环境：Windows、Unity **2022.3.62f3c1**、Linear Color、PC VR / OpenXR / XRI 2.6.4。Play Mode 命令使用 `-batchmode -projectPath MuseumVR -executeMethod <入口> -logFile <本地日志>`，保留图形且不传 `-quit` / `-nographics`，由验收入口控制退出。没有更新编辑器、Packages 或 ProjectSettings。

最终产品 / 验收代码被测提交为 **`df135bb181f1a144cf5718c6e763238fd7e835b8`**；本地化模块在产品相同的 `6314c6ea0b6f867aa255cce0d38c530f4ce1a252` 上运行，之后仅新增精细验收的 Ray 夹具与诊断。各测试启动时产品文件无额外未提交改动，只有文档和本轮未跟踪证据。最终证据提交只增改文档 / 报告，不改变已测 Unity 文件。

| 入口 | 本轮结果与证据 |
| --- | --- |
| `Museum.RefinedModels.EditorTools.RefinedModelValidation.RunBatch` | 退出 0，**422 项检查通过、0 errors、23 张截图**。[完整报告](../../artifacts/helmet-interaction/qa/runs/20261012T115156460Z-refined-integration-e6a9890e3ed1406199dcc69fda707823/report.json)、[派生摘要 / 输入指纹复核](../../artifacts/superlite17b-unity-integration/qa/runs/20261012-pr44-fix/refined-summary.json)。原拿放、前窗、剖面、AIR / VOICE 与复位回归保留；新增三个实际网格表面距离、前窗跟随、三语展签 / 卡片 / 通信限定、1 → 2 → 1 姿态 / 文案 / 已选卡片 / 进度恢复。10 次 Direct（Mark V 基准 1、SuperLite 三语各 3）和 3 次 SuperLite Ray 均产生实际 selectEntered，未误抓整盔 / 窗体；Ray 使用保存的生产 ConeCast，实际首次 3D 命中为目标 collider。 |
| `Museum.Localization.EditorTools.LocalizationValidation.RunBatch` | 退出 0，**11 / 11 通过**，210 条表、558 已保存字形。[本轮报告](../../artifacts/localization/qa/runs/20261012T114444001Z-validation/report.json)、[版本与进程记录](../../artifacts/superlite17b-unity-integration/qa/runs/20261012-pr44-fix/localization-module-run.json)。 |
| `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch` | 退出 0，默认 Mark V 保存主场景 **82,373 次断言、0 errors**，其中热点 **786 次**；大量逐帧三语文本检查不是独立用例数。[报告派生摘要 / 完整报告 SHA256](../../artifacts/localization/qa/runs/20261012T114900142Z-integration-6973e660dbf04ce8a3d4f71d05ccb95c/summary.json)、[热点交互报告](../../artifacts/localization/qa/runs/20261012T114900142Z-integration-6973e660dbf04ce8a3d4f71d05ccb95c/hotspot-interaction.json)。语言按钮、三语生命周期、实际 Direct / Ray、归位和访问状态均通过；不把默认年代回归混作 SuperLite 用例。 |

八个保存非玻璃材质经普通 Color 属性与 GPU 浮点目标实际回读，对 Blender 源线性 RGB 的最大通道误差 **1.7881393 × 10⁻⁷**。原错误线性直写作为负对照被区分。最终三语前窗近景手动目视核对，文字完整、无缺字 / 截断，字号 .3；模型展台截图也已核对。

精细报告的 **306 个**输入指纹、主场景报告的 **339 个**输入指纹在最终归档时全匹配；后者 44 个 Unity 虚拟 Packages 路径按锁定版本解析到同一 Library/PackageCache 文件后逐字节核对。主场景完整报告 18,575,371 字节留本地忽略目录 `tmp/pr44-full-reports/`，SHA256 `055329b82c702d2dd0b2b18d65bf4da1e05b7d4da7e3dbe759f99327928575e4`；只提交摘要、热点报告和截图。原完整文件及失败运行不覆盖。`git diff --check`、逐文件暂存后的检查与 `git lfs fsck HEAD` 通过，原工作目录的 ProjectSettings 哈希仍与开工一致。

独立只读运行时和保存资产复审均未发现遗留阻塞；最终实际输入报告覆盖四项修复后，结论为 **可以合并 #44**。GitHub checks / statuses 为空，不声称 CI 通过；自动技术复审也不是另一位成员人工 APPROVED。按维护者已经明确授予的“通过后直接合并”授权执行，合并结果以本 PR 回读为准。

## 验收夹具诊断与限制

失败尝试保留在各自独立运行目录，本轮只归档有用摘要和哈希，不覆盖原 PR 或审查证据。旧普通抓取用例把手放在 SuperLite 壳体内部，真实 XRI 选中了 Bonnet 热点；现用真实后壳表面触点，并严格验证整盔 Direct 选择、移动和旋转。XRI 优化 Direct 查询使用被禁用的 SphereCollider 的配置，按实际包源码计算查询中心和半径。

前窗热点中心与面窗 trigger 重叠，二者 ClosestPoint 距离同时为零。用例改为触碰同一热点可见前侧，仍在热点球内，并要求热点距离为零、窗口距离大于 1 mm、右 Direct 实际 hover 与真实 Grip 完全释放；原实际选择事件、类型、整盔/窗体未被选中断言及 Ray 首次命中检查保持。没有隐藏控件、更改产品物理范围或调用程序化 SelectEnter。

首次实际前窗选择后的英文卡片溢出（最低字号 .28，原框 .51 × .12），因此缩短三语说明，保持观察演示限定、原字号下限和信息卡范围。旧主场景归位夹具把仍保持 Grip 的手留在移动热点附近，却始终要求访问绿色，忽略正常的悬停橙色。颜色断言改为按实际 isHovered 精确对照保存 hoverColor / visitedColor / availableColor，保留访问标志与原 RGBA 容差，并新增停放手后的实际 hover 退出断言；运行时未因测试改变悬停行为。

旧正前低位的颈封 Ray 夹具未命中，旧报告没有首个命中记录，不能断言某面板就是阻挡对象。本轮使用实际右侧 0.65 米接近位置，保留全部界面并检查实际首次命中与选择；最终报告确认首次命中 Breastplate Hotspot（ID 保留，当前主题为颈部密封），UI hit 为 none、XRRayInteractor 选择成功。原失败与其他尝试共六次的失败摘要 / 哈希见[诊断记录](../../artifacts/superlite17b-unity-integration/qa/runs/20261012-pr44-fix/failed-fixture-runs.json)，不作为通过证据。

桌面模拟输入、GPU 回读、字形和单眼截图只证明记录中的保存资产与行为。真实头显 / 双眼、设备性能、Windows 可执行构建、人工术语与访客学习评价、同配置实测尺寸和实际 posts 接口仍未验证。17B 实物前窗螺栓固定，项目侧开仍是博物馆观察演示；Unity 程序凹凸 / 涂层未烘焙。最早年代索引 0 仍为占位。

## 交接

普通预览打开已保存的 MuseumStart / Play / F10，先确认 LFS 实体展开；不要重跑生成器、安装器或 AlignSupportBatch。下一步由获得设备权限的成员对实际合并版本补做头显、性能和完整访客流程检查。最终是否合入及 merge SHA 以 [PR #44](https://github.com/Twispra/XRmuseum/pull/44) 实际状态为准。
