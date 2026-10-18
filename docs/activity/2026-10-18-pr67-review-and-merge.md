# PR #67 重新审查、交互修复与合并交接

## 本轮授权和来源

用户在 #65、#66、#69 合并后要求重新检查 #67：没有问题就合并。此前“#67 先不合”的等待已被这次条件授权替代；同一会话“自己审批”的授权沿用，批准明确标为 AI 审查，不写成另一位成员的实机或组内意见。

开工实际 main 为 `0109e3f75019f585454da638b56101f235d40770`，#67 head 为 `4aab865315c6cb898344271f7777582a2cf8355a`。后者是 #66 合并后 GitHub 自动重接的分支，原作者被测 `86a728d` 仍保留在历史。重新 fetch 后，在已有独立 managed worktree `C:\Users\zhaoooooo\.codex\worktrees\pr66-obj-fix\XRmuseum` 新建 `codex/pr67-review-fix`，正常 merge 得到 `a18bc0e58257cbddb04b976a1bd099cffb06efb4`；没有改原工程或强推。

整合人协调全部提交和 Unity 运行；并行审查/修复仅分配明确路径：GalleryButton/Display Runtime、独立 GalleryValidation Editor、以及根负责的场景/三语核对和文档。没有并行保存场景或运行多个 Editor。

## 重查发现

1. **按下立即执行，不能取消。** 原 GalleryButton 只监听 `selectEntered`，按 Grip 即转 30°/复位，移开、跨目标或 F11 无法取消已经执行的动作。它与当前实体站按下反馈、原位松手确认、移开取消不一致。
2. **旧验证没有测真实输入。** `PhotoHelmetGallerySetup.ValidatePlayMode` 直接调用 `TriggerProgrammatically` 及 `LanguageChannel.SetLanguage`；它能证明方法和引用存在，不能证明 XR 选择、松手、取消、物理遮挡或保存字形布局。历史报告保留原有范围，本轮另补批处理真实输入验收。
3. **旧分支必须保留最新主线数据。** 三路合并后，JSON/资产表都是 266 条，仅新增 photo1988 两 key，原 264 条没有删除或改值；#69 三语短卡和 #65 面板三条文案保留。不能用旧分支整份表覆盖 main。
4. **实体遮挡未阻止选择及确认。** `a24f397` 的实际 queued XR Grip 反例中，Physics 最近实心 Cube 距离 0.214m，按钮 0.395m，但生产 ConeCast 仍优先返回按钮；按下产生一次 XRRayInteractor 选择，松开确认一次并旋转 30°。因此不能只把 Cone 首结果当作无遮挡证据，需要按钮自己的实体视线保护，按住和释放当帧都重新检查。
5. **设置面板与新模型相交。** 同一提交在实际眼位 `(-3.1,1.55,2.75)` 打开设置，完整包络中心约 `(-3.1,1.49,3.398)`，与两个真实模型 Renderer.bounds 相交，中心视线也被挡；269 项通过、2 项失败。原面板只收集台座、中央展品、设备及固定控件，需要把保存场景的 `Exhibit Placeholders` 展台子树也纳入障碍范围，而不是放宽视野或净空。

字体静态核对：两条新文案的三语共 68 个不同非空白字符，全部已包含在 main 的 613 字符规范清单和 631 字符保存 SDF 中。实际重新运行 make-charlist 后清单零差，不需要改源字体或重新烘焙。

## 场景和模型边界

实际合并场景相对 main：779→834 个 YAML 文档，新增 55、删除 0；既有语义仅修改左展台名称、追加 Display 子节点、隐藏旧 Exhibit Number。#65 的站点/入口组件、#69 中央 PrefabInstance（含颈封坐标）完整块保持原字节。中央 Prefab、抓取缓存、字体、InspectionShell、年代文字源码均与 main 相同；未新增或修改 LFS 资产，82 份工作区 LFS 均已展开。

左台原台顶 `y=1.16m`，三按钮底约 `1.17m`，模型底约 `1.18m`；没有为展示而改台面、模型几何或已有 GUID。几何净空、三语渲染与真实选择以本轮 Unity 结果为准，静态坐标不冒充头显触达验收。

本次仅审阅 #67 提供的左台最小陈列：独立模型与左转/复位/右转，不加入虚构的开窗、供气/通话、XR 剖切或中央年代绑定。1988 沿用原照片标牌来源声明，身份/机构/尺寸未核实，展签仍写外形研究与不是实测复原。用户对本 PR 的合并授权不等于已完成 #64 的双展台方案比较或全组采用意见；#64 保持开放，右展台不扩展。

## 修复和验收

交互修复复用现有实体按钮确认与取消机制并注册 station，以继承拖动互斥、跨目标残留、F11 和生命周期保护；静态展签保持 LocalizedText 单一绑定。`be2d795` 修复松手确认／取消；`f7b5391` 在 GalleryButton 的选择过滤、持有 Update、最早订阅的释放回调中检查实际实体 LOS，保留 Cone 辅助命中但拒绝目标点之前的实心 Collider；OperatorPanelController 只追加当前场景展台容器可见 Renderer，沿用原完整包络、0.06m 障碍余量、视野、距离、视线和中央净空要求，没有改共享实体站／PhysicalControl 或场景来让测试通过。

最终产品／验证源码冻结为 `024d03942cc93d90453583e1c7e33e4eac96ecf2`（产品 Runtime 与 `f7b5391` 相同）；后续仅补文档和运行证据。测试打开保存场景，不调用 Place/RepairSavedLabels/原型生成器，不保存产品资产。Editor 通过正常 Dynamic Input System 更新排入模拟设备状态，使用生产 Input Actions 与选择事件，未直接 SelectEnter／SelectExit／TriggerProgrammatically。五组均自然退出 0、forced=false，每轮 1299 个 tracked MuseumVR 文件前后零差；完整日志通过进程结束的应用异常／编译错误／断言／shader error 匹配均 0。启动期无 HMD OpenXR、许可／网络诊断以及既有 obsolete 编译警告单独看待，不声称零全部警告。[最终审计](../../artifacts/helmet-interaction/qa/runs/20261018-pr67-review/final-audit.json)核对报告、退出、完整日志哈希与静态输入等价。

| 验证范围 | 通过检查 / 失败 | executeMethod | 本轮证据 |
| --- | --- | --- | --- |
| 左台射线／Direct／三语 | 801 / 0 | `Museum.PhotoHelmetTrial.EditorTools.PhotoHelmetGalleryValidation.RunBatch` | [报告](../../artifacts/helmet-interaction/qa/runs/20261018-pr67-review/positive-gallery/report.json) |
| 面板及左台位置 | 271 / 0 | `Museum.OperatorPanel.Editor.OperatorPanelInteractionValidation.RunBatch` | [报告](../../artifacts/helmet-interaction/qa/runs/20261018-pr67-review/positive-panel/report.json) |
| 本地化模块 | 11 / 0 | `Museum.Localization.EditorTools.LocalizationValidation.RunBatch` | [报告](../../artifacts/helmet-interaction/qa/runs/20261018-pr67-review/positive-localization/report.json) |
| 主场景本地化集成 | 128389 / 0 | `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch` | [报告](../../artifacts/helmet-interaction/qa/runs/20261018-pr67-review/positive-integration/report.json) |
| 中央年代／热点 | 989 / 0 | `Museum.Localization.EditorTools.VisitorEraValidation.RunBatch` | [报告](../../artifacts/helmet-interaction/qa/runs/20261018-pr67-review/positive-visitor/report.json) |

命令为 `E:/unity/36version/2022.3.62f3c1/Editor/Unity.exe` 加 `-batchmode -projectPath <隔离checkout/MuseumVR> -logFile <新独立运行目录/unity.log> -executeMethod <表内方法>`；不加 `-quit`／`-nographics`，五轮串行。每轮 process.json 保存精确参数、PID、开始结束时间、源 commit 和全部 dirty。Gallery／Visitor 另由 MUSEUM_QA_OUTPUT 定位外部独立目录；原面板／本地化验证器按自身新 UTC/GUID 目录输出，再由 provenance 记录原路径／哈希收取有用证据。

检查数量含保存文件哈希与重复字形断言，不相加作独立用户用例。大报告完整逐项 JSON 以确定性 gzip 无损保留，摘要带完整原 JSON SHA；没有删掉失败检查来获得通过。独立比较场景318项沿用前轮 `7e5b539` 的明确历史证据，比较场景／模型／材质／着色器输入与本轮一致，不冒充新跑。静态整合报告源为 `c071766`，最终审计验证其场景／表／字体／照片模型、Prefab、材质和比较场景与最终源码全同。

失败证据分别保留在本轮 QA 的 `negative-*` 目录，不被最终通过报告覆盖：`d7ed9d5` 为新验证器 `Capture` 名称冲突的编译失败，未执行行为检查；`8016e13` 为错误要求 StateChange 原始 Grip 持续为 1 时内部 filter 必须 false（实际无重选、无拖动）；`c071766` 为错误要求 ConeCast 首结果必为物理最近 Cube，尚未执行 Grip；以上三项仅修正验证器的前提。`a24f397` 的实体穿挡动作和面板相交才是实证产品问题，按原失败条件修 Runtime，不删除断言。编译／断言失败日志不能写为零异常的通过运行。

`42f8c77` 的 591 项通过／1 项失败运行已证明修复后实体不穿挡（0 选择／0 确认／0°）、同一 Dynamic 输入回调插入挡物再松手取消（1 enter／1 exit／0 确认／1 取消／0°），以及停用新按 Grip 禁止选择、Direct 接触；但三语阶段失败，所以不是最终通过证据。验证器在新增模拟追踪设备之前快照了由 PhysicalHand 暂时 SuspendQueries 的 ray.enabled=false，Direct 结束错误恢复该值，导致语言射线停止更新；这不是 Welcome Panel 实体遮挡语言按钮的证据。快照改为 Direct fixture 开始前实际追踪已恢复时读取，并检查退出 fixture 后射线活跃；三语同时使用当前设置面板的真实 PhysicalControl 生产输入，旧 LanguageButton 路径仍留给原本地化集成独立检查。没有改生产追踪恢复逻辑来迁就夹具。

最终 Gallery 报告 801/801；三语逐项实际 hold 时 1 次选择、0 次确认、语言不变，release 时 1 次退出、1 次确认、精确切到目标语言，所有五条展签随后核对固定 key、保存字体、字形、无溢出／截断、完整相机矩形与实体视线。三语独立图已逐张核看，全文完整、无遮挡。左转过程图有手套短暂遮住标题尾部，总览裁切左展签／按钮，因此它们只证明旋转／两展并存，不证明所有截图都全文可读。面板左台两图分别展示原眼位出现中心、以及只转头看中心后全包络可见；不把原眼位部分在画面边缘写成无需转头即可完整阅读。诊断图不替代双眼头显检查。

## 合并期间的主线与依赖协调

审批期间 main 又正常合入文档 PR #70（`d445664`，提交 `2faf692`）：仅 README、独立活动、external-resources 与 Phase 2 PDF，MuseumVR 零差。本分支以正常 merge `71c9f71` 保留该四文件改动，external-resources 自动合并两轮声明；被测 MuseumVR tree 仍为 `eaa3237d73d386dc7b64bfc6c1994078ff2f958d`，没有因文档整合重跑相同产品测试。

#67 原属 stack 68（#66 已合并、#67 是唯一未合并成员），PR 旧 base.sha 一直锚在 `88ca9c3`。GitHub 反复以“The merge-base changed after approval.”撤销正常批准，三次正常 merge-async 都被“一位有写权限成员批准”拦截，未绕过规则。标准 update-branch 对 stacked PR 返回不支持；先确认没有其他开放成员，再按 GitHub 官方 Stacks API 正常 unstack，仅解除 #67 的旧依赖关系，已合并 #66 保留。随后针对含 #70 的最终 head 正常批准和合并，实际状态见 PR 回读；没有更改保护、审批人数或账号权限。

## 本地保留与限制

原工程开工时唯一 tracked 修改为 `MuseumVR/ProjectSettings/ProjectSettings.asset`，SHA-256 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`；另有 **32,482** 个未跟踪文件。本轮独立保存副本/patch/路径、大小与 mtime_ns，位置 `E:\Codex\2026-10-18\xrmuseum-pr67-preservation`。同步前检查 incoming 路径冲突，合并后仅 fast-forward，最后再核对原文件；实际合并 SHA 与同步结果以 [PR #67](https://github.com/Twispra/XRmuseum/pull/67) 回读交接为准。

独立工作期间，原目录由另一任务正常切换至已合并 #70 的 `codex/phase2-readme-activity-record-2026-10-18`／`2faf692`，不回滚该新提交或新增本地文件。同步时重新记录当前分支、HEAD、设置与未跟踪文件快照，先核对实际 incoming 差异，再以非强制 fast-forward 更新本地 main 并切换；原始开工基线继续保留，新快照不覆盖旧记录。

同步前原始基线复核：设置和 32,476 个原未跟踪文件元数据未变；六篇个人 txt 已由并行 Phase 2 文案任务修订，发生在本任务同步写入之前。逐篇核对当前 SHA 与该任务 `phase2小组和个人作业/原稿备份/20261018-203717/revision-validation.json` 的 after_sha256 一致，保留其当前版本和原稿备份，不擅自恢复成本轮开工时旧版本。最终同步使用独立 `sync-baseline-current.json` 核对全部现有未跟踪文件及六篇当前哈希，实际通过数量见 PR 回读；不能把别的已授权任务修改误记成“所有原始文件始终零差”。

环境仍为 Windows / Unity 2022.3.62f3c1 / PC VR / OpenXR。模拟设备和单眼图不能证明真实头显输入、双眼可读性、坐姿触达/舒适性、性能、用户研究或新 Player 构建；本轮不改变这些设备验证边界，不自动关闭 #64 或 #60。
