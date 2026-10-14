# 虚拟手与双手头盔协作

日期：2026-10-14（北京时间）。任务 [#50](https://github.com/Twispra/XRmuseum/issues/50)，用户在当前对话明确授权按已讨论的分阶段流程开发。负责人 Twispra，Codex 辅助实现。工作分支 `codex/physical-hands-2026-10-14`，基于实际 `origin/main e5eebe0527383abdc6b1bbb3e62d0504ecfdf5f6`（#49 已合入）。

## 范围与实现

目标为虚拟手、近距拿放、碰撞阻挡及双手协作。引用聊天仅提供体验思想；未采用其 Unity 6 原型代码，也未升级 Unity **2022.3.62f3c1 / XRI 2.6.4 / OpenXR** 或改变 Windows PC VR 平台，不声称取得 Valve/Alyx 内部算法。

- 新增 `Assets/Museum/Features/PhysicalHands/`：原创浅灰手套与海军蓝袖口的左右手 Prefab、五指预设姿势、独立跟踪目标与世界空间碰撞代理、双手抓取转换器、开发接入工具、定向验收和 Windows 构建入口。Grip 驱动握姿与近距抓取，Trigger 驱动食指；不是光学手指追踪。
- 使用带扫掠、滑动和旋转路径可达性检查的运动学 **Box 手套包络**，张手约 15 × 9 × 20 cm，闭合随可见握姿收缩、打开立即扩展。包络覆盖手掌及预设手指空间，不是逐指动力学，也不提供真实力反馈。Direct/Poke 保留原控制器层级，在 Update、FixedUpdate 和 BeforeRender 对齐代理；独立目标读取设备姿态，避免查询位置反向成为追踪输入。
- XRI 继续独占持有物刚体运动。单手/双手转换器在 `1→2→1` 的选择数量变化时重建实际握点相对基线，限制物理追逐误差并保持原尺度。已有一手持盔时，另一手贴近本体的 Direct Grip 优先接握本体，减少外壳说明热点抢选；窗口/剖面目标与 Ray 保留原作用。
- Direct 候选查询球完整覆盖固定张手包络及其 **25 mm 接触余量**，修正倾斜按钮表面可达却未进入候选列表的问题。最终选择仍要求固定张手包络附近的 25 mm 范围和实心障碍视线检查；扩大候选收集没有放宽实际抓取范围或允许隔墙抓取。手模块启用时，Museum 观察控制与探索按钮按 `ColliderVolume` 表面距离排序；停用时还原每个控件原有距离模式与查询半径。
- 头盔通过 opt-in 开关允许不同手分别拿本体、开窗或调剖面。同手的 Direct/Ray 不能同时拿本体与子部件，窗口/剖面仍互斥。部件按操作手相对头盔的姿态驱动：两手随头盔共动时保持局部状态，操作手静止而本体移动时允许相对操作。本体被持有时仍由 XRI 驱动；最后一只本体手松开而部件仍被握住时暂时固定，全部释放后恢复对应物理状态。
- 跟踪失效、明显原点瞬移/快速转向及持有握点偏差过大时取消同手 Direct、普通 Ray 与嵌套传送 Ray，避免取消被误解为窗口短点击或投掷。Direct 恢复须松 Grip；各 Ray 以自己的有效 Select 松开独立解除保护，不以传送摇杆旧状态锁住普通抓取，也不吞掉未使用休眠射线的首次后续输入。既有 Home/RESET/FLOAT 保留。
- 共享 `MuseumStart.unity` 与 `InspectableHelmet.prefab` 由本对话单一整合；另保存 `PhysicalHandsLab.unity` 独立实验入口。既有 GUID、XR 摄像头/Origin、Samples、Input Actions、Packages 与模型源/导出沿用；没有编辑器/平台迁移。构建导致的 `preloadedAssets` 设置副作用在构建后恢复，见下方独立证据。

开工时原 `E:/XRmuseum` 位于 `main 4578e66`，已有场景、设置、RefinedModelValidation 及文档改动全部保留。本轮使用 managed worktree，未把原目录未知改动纳入分支，未用旧原型生成器初始化或覆盖资产。

## 自动验证与版本范围

全部运行使用 Unity 2022.3.62f3c1，报告记录各自的实际源码版本、额外未跟踪 QA 产物与来源指纹。下面的检查数包含来源、保存资产、夹具、状态和输入检查，不能写成同等数量的设备操作，也不合并不同版本的检查数作为一次验收。

| 验证 | 实际被测 commit | 结果与证据 |
| --- | --- | --- |
| 最终手模块专项 | `e8516dd94596a209ee925ba9e4e55b2aff755ecc` | **171/171、0 errors、0 warnings、进程退出 0**；[报告](../../artifacts/helmet-interaction/qa/runs/20261014-152154-hands-final-restoration/20261014T072222166Z-physical-hands-67cb9d3f0c224d55a7acfe85b3151d53/report.json)、[进程记录](../../artifacts/helmet-interaction/qa/runs/20261014-152154-hands-final-restoration/process.json)。 |
| 原主场景 Inspector 回归 | `8ecee3baa7f9e7f2c47dea54200f1d56557bb7ef` | **207 项通过、0 errors、进程退出 0**；该 suite 没有 warnings 字段，不记为 0 warnings。[报告](../../artifacts/helmet-interaction/qa/runs/20261014T065504238Z-inspection-main-38d7d2950df242b784dea296b6fc4612/report.json)、[进程记录](../../artifacts/helmet-interaction/qa/runs/20261014-145421-inspection-section-exterior/process.json)。 |
| Explorer 回归 | `b92af45431b232c8d880f26a4531e0c71a1ae135` | **1,486 项通过、0 errors、进程退出 0**；该 suite 没有 warnings 字段，不记为 0 warnings。[报告](../../artifacts/helmet-exploration/qa/runs/20261014T070452764Z-exploration-ffd6adc93a4341159881639a6a34ac38/report.json)、[进程记录](../../artifacts/helmet-interaction/qa/runs/20261014-150424-exploration-exterior/process.json)。 |
| 三语整合专项 | `e97f3c9433156d0472cc5503c15f3d4eb154b2e7` | **83,688 项通过、0 failed、0 errors、0 warnings、进程退出 0**；[可提交摘要](../../artifacts/localization/qa/runs/20261014T071209016Z-integration-15db260709674e84a19984a6d690bf39/summary.json)、[同次热点子集](../../artifacts/localization/qa/runs/20261014T071209016Z-integration-15db260709674e84a19984a6d690bf39/hotspot-interaction.json)、[进程记录](../../artifacts/helmet-interaction/qa/runs/20261014-151139-localization-clear-ray/process.json)。 |
| Windows64 Development 构建 | `09b9adfecf21a9ab6f8c261b72a0525c6d768d2e` | **Build Succeeded、0 errors、2 warnings、进程退出 0**；[构建报告](../../artifacts/helmet-interaction/qa/runs/20261014-153017-windows-development-settled/20261014T073044861Z-windows-development-cc9a7d7d6ebc46558d8a8bb1a20745f0/report.json)、[退出后源码与产物清单](../../artifacts/helmet-interaction/qa/runs/20261014-153017-windows-development-settled/20261014T073044861Z-windows-development-cc9a7d7d6ebc46558d8a8bb1a20745f0/post-exit-files.json)、[进程记录](../../artifacts/helmet-interaction/qa/runs/20261014-153017-windows-development-settled/process.json)。 |

最终手模块 171 项分为：source 55、saved integration 18、geometry fixture 10、API fixture 20、API collaboration 34、lifecycle fixture 8、production input 24、environment 1、API cleanup 1。几何夹具检查薄墙/桌面、快速目标移动、旋转中途受阻、持物碰墙及恢复，使用简化 Box 手套包络；不证明任意几何和速度下绝对零穿透。显式 API 夹具检查抓放、双手所有权和两种选择顺序、窗口/剖面共动局部状态、最后本体手释放后的部件固定、最终动态恢复，以及同手 Ray 安全取消和各自输入恢复；这些同步 manager/公开 API 检查不称为真实 Ray 输入验收。生产输入链通过合成 XRSimulatedController Grip → 保存 Input Actions → ActionBasedController → 原 Direct → 实际组件检查近距抓放、无缩放 `1→2→1→0`、持盔开窗与 Home 复位，另有生命周期与启停还原检查。隐藏 batch 帧按约 90 Hz 配速属于 harness 设置，运行耗时不是设备性能基准。

原 Inspector/Explorer 的选择与移动检查沿生产合成输入链运行，明确标记的公开 API 与姿态夹具分别记录。Inspector 剖面数学连续性夹具暂时隐藏便携面板实体，避免无关面板阻挡指定腕部轨迹；有物理手时，pure roll 从既有有界切面范围的外侧深度开始。API 仅布置固定本体、初始平面与清理，选择、移动、转腕、松手、重抓仍用生产合成 XR 输入，手与展品碰撞仍启用。既有完整姿态/法线误差 <0.15°、球柄/中心误差 <1 mm、本体位置 <1 mm/旋转 <0.1°及连续性、可见 roll、松手/重抓不突跳断言未放宽。该隔离夹具检查求解器，不代表面板显示时所有腕部路径均可达；面板按钮的输入路径在前面的实际流程中另行检查。单眼编辑器截图及诊断近景不替代双眼设备验收。

三语原始完整 `report.json` 为 **18,890,844 bytes**，留在本地；SHA256 为 `829f9dcb0ed93a9becb36696ae4066bfafd93131a75e2add7231cbdfc848227d`。可提交 `summary.json` 为 **451,632 bytes**，SHA256 为 `d4f97ad9fa0c443c8dabfe2e3c54ce35fbbeb339d7320f3f28605cf15a70a256`，声明 `derivedSummary=true`，保留原 commit/版本/时间/输入方式/限制、全部来源文件指纹、errors/warnings、samples/captures 及真正的失败计数。非 `trilingual main scene` 的 **1,560 项完整保留**；主场景三语 **82,128 项**只复制 18 项代表，明确省略 82,110 项，分组计数覆盖原完整报告。热点的 **786 项检查 / 20 次选择**是 83,688 项中的子集，单独提交用于审阅，不能再次相加或声称摘要包含逐项完整原报告。

早期 `7d3301174006df7370f0b78d51d84159dd578db6` 的 [154 项报告](../../artifacts/helmet-interaction/qa/runs/20261014-133531-hands-final/20261014T053559865Z-physical-hands-4a11717290d046c2916c3cddc0a97d83/report.json)仅保留为历史迭代证据；其后已有 UI 候选范围、控件排序与 Ray 恢复修正，最终手模块结论使用 e8516dd 的 171 项。后续 e8516dd 相对 Inspector/Explorer 版本主要补充验收路线、生命周期夹具及无语义 YAML 排序；运行时唯一差异是 `31cae9e` 修正初始禁用 Rig 的 Awake 激活状态，原已启用主场景行为不变，最终 171 项覆盖此修正。`09b9adf` 仅修改 Editor 构建工具的退出等待，运行时不变；各旧 suite 的实际被测 SHA 保持原值，不改写成最终 HEAD。

开发中修正了旋转仅看终点、握手缩包络导致候选丢失、热点抢接握、同手 Ray/嵌套传送 Ray 遗漏、倾斜控件未进入 broadphase、控件中心排序和禁用启动/启停恢复等问题。手代理采用自定义扫掠配合 Discrete、降低 contactOffset 后，历史组合修改夹具中的未选中重靠近漂移约 `2e-9 m`；这不证明 Speculative 是所有漂移的唯一原因，也不替代设备手感检查。初次编译、失败迭代与工具调试日志留本地，不作为最终通过证据。

## Windows 候选与构建限制

构建从已保存 `MuseumStart.unity` 运行 `BuildPipeline.BuildPlayer`，目标 StandaloneWindows64、Development；没有调用 Setup/Builder、保存场景、升级 package 或迁移平台。最终报告产物 **208,828,238 bytes**，Build 时间 **12.7617612 s**，报告总耗时 **19.7365325 s**；随后等待 20 s，并等待 Editor 不再 compiling/updating 后正常退出。两条 warning 为已有 OpenXR PoseControl/Vector2Control 的可选迁移建议，未为消除建议升级依赖。

构建前后 **1,121 个来源文件哈希匹配**，真正退出后再次复核零不匹配、trackedDiff 为空。构建修改的 `preloadedAssets` 已恢复；EditorBuildSettings 场景列表未变，保护文件恢复记录通过。退出后清单记录完整 **271 个产物文件**及其哈希，exe 与报告一致。先前 e8516dd 的首次 BuildReport 虽生成成功，但 Editor 退出卡住，已显式结束本轮自有进程、exit -1；只属于构建工具调试历史，最终通过采用正常退出的 09b9adf 运行。

完整候选目录：`E:/XRmuseum/Builds/Windows/physical-hands-final-20261014-1531/`。入口 `MuseumVR.exe` 的 SHA256：`fa01ccdbaa5f74c777609235b99ba8988285b2bf0754445e85bba268b2e61eb7`。可运行交付必须保留整个 Standalone 目录及实际 `_Data`、UnityPlayer、Mono 等依赖，不能只发 exe。**本轮未启动 exe，未验证 Windows Player 的 OpenXR 设备连接或真实流程**；构建成功不代表设备启动、舒适性、性能或用户研究通过。

## 首次设备反馈与交接

按[手模块指南中的约五分钟步骤](../physical-hands.zh-CN.md)，先打开保存的 PhysicalHandsLab 检查左右手掌/双眼对齐、张握手与食指响应、薄墙/桌面阻挡和撤回恢复；再在 MuseumStart 检查拿放、左右接握及 `1→2→1→0`、一手持盔另一手开窗/调剖面、共动局部状态、RESET/FLOAT 与传送保护。候选构建直接检查主场景展台与周边墙面。每项记录“正常/异常/未测”，并记录实际 SHA、额外改动、头显/控制器、OpenXR runtime/串流、站坐姿和问题复现顺序。

真实头显双眼对齐、手掌偏移、延迟、碰撞/握持手感、保护是否易懂、设备性能、完整用户流程与用户研究仍待验证；已有视角反馈不覆盖新增手模块。日常体验使用已保存场景和 Prefab，不运行 Install 或历史 HelmetPrototypeSetup.Build。后续根据首轮反馈调整原型参数，并按实际记录团队决定和成员贡献；AI 初检不冒充成员人工批准或设备评价。

最新 fetch 后实际 main 仍为 `e5eebe0527383abdc6b1bbb3e62d0504ecfdf5f6`。新增未合并 [PR #51](https://github.com/Twispra/XRmuseum/pull/51) 涉及 ExplorerButton/Panel、VisitorRecovery、翻译及指南，与本任务没有源码修改重叠，也不是本分支依赖；本轮未自动整合该 PR。其新增按钮合入后应做联合回归，不能把本次报告扩大为已覆盖尚未整合的功能。本任务未授权自动合并。

当前交接：保存手模块、最终自动回归和 Development 构建候选已提供，下一步收集五分钟真实设备反馈，并在 PR 中记录完成、验证、限制、下一步和问题。只提交选定有用报告/摘要与产物哈希清单，保留被引用历史报告，原完整三语报告和大量调试输出留本地；本轮没有覆盖其他人的原目录改动。

## 来源与 AI 辅助范围

手套几何使用 Unity 内置 primitive 和本项目脚本原创生成，材料原创；无新第三方模型、贴图、手部 SDK 或 Unity package。既有官方 XRI/OpenXR 样例与许可沿用[资源记录](../external-resources.md)。Codex 辅助设计、代码、手套几何、自动验证、构建入口及文档；包络尺寸、握姿和保护阈值是原型选择，不是历史装备实测、真实重量模拟或 Valve 源码。成员实机结果、团队决定、AI 政策及个人反思按实际活动另行记录。
