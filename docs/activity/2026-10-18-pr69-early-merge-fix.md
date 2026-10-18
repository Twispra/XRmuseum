# PR #69 合并前修复：字形、热点、实体手缓存与当前年代入口

## 范围和来源

用户授权修复 #65、#66、#69，验证通过后由整合人合并并同步原工程；#67 不合并。本记录仅负责 [PR #69](https://github.com/Twispra/XRmuseum/pull/69)，在独立 worktree 从 `83d64397d99f6d3b7dc99d85fe62bd23d5fff979` 开始，初始 tracked Changes 为空。原 `E:\XRmuseum` 的设置及未跟踪文件没有在此目录改动。

本次不重装早期模型、不运行原型生成器、不改变 Unity 2022.3.62f3c1／Windows PC VR／OpenXR 配置。后续完整回归发现既有 SuperLite 颈封热点中心在台面实体内，唯一场景修复是定点修改保存的该 attachment 三个坐标值，没有调用整体场景保存或重建。1988 年资料与独立研究场景属于 #66；本次没有 #67 的左侧主展台接入，不能把其他本地分支的预览写成 main 成果。馆藏依据仍为 Science Museum Group 1881-47；背面、局部尺寸、材质、虚拟前面板和 AIR／VOICE 的解释性边界保持。

## 修复

- 原保存 SDF 585 字符，新增三语文案使规范清单为 620 字符，其中 46 字符缺保存字形。字体为 Static、无 fallback，不能靠源 TTF 存在某字来宣称可显示。保留源 TTF 全部 7671 个码位，从既有 OFL Noto Sans SC Regular 400 来源补 15 个繁体字至 7686；只原位维护保存字体及其既有图集／材质身份。
- 早期前窗热点原先沿 `+forward` 放进帽体后侧。现在切换年代时对实际网格求交、使用外法线偏移，再保存相对于该部件的局部附着点；前窗随实际转轴运动。帽体和胸板也使用实际网格交点，避免将世界 AABB 的轴向长度当作头盔局部长度。
- 原 Prefab 的 518 个握持网格及缓存未包含早期 OBJ。烘焙器现在接受 ModelImporter 导入的 FBX 和 OBJ，并更新现有缓存与绑定；不修改模型导入器或碰撞体。这个修复服务于传统拿放路径及 Prefab，不能反推当前固定整盔旋转已发生实机故障。
- 旧 VisitorEraValidation 的运行时前置条件寻找两个启用的旧年代按钮，但 #59 已将它们退场。保存资产中的旧按钮／key／引用检查保留；当前流程改测生产实体按钮的射线悬停、Grip 按下、原位置松手确认。原持窗延迟切换断言保留在明确的可选传统交互夹具中；不再把该旧路径当作当前固定实体站。

## 验证状态

源码准备提交为 `e2b25a9aecc44c7a4e6c0c84274d0b9f3491a257`。在 Unity 2022.3.62f3c1 运行 `Museum.EarlyHelmet.EditorTools.EarlyHelmetMergeRepair.RunBatch`：重新编译成功（两条既有 CS0618 警告），FontBaker 和握持缓存维护通过，Editor 自然退出 0，未强制终止。SDF 631 字符／原身份保留／静态图集保存字节匹配；握持绑定现为 584 个 Filter、536 个唯一 Mesh、287,030 个三角面，其中 66 个 Filter 引用早期 OBJ（含保留的停用源前窗与活动副本）。

维护前后 1,207 个 tracked 工程文件仅改变预期的 SDF、GripSurfaceCache.asset、InspectableHelmet.prefab 三项；三份 `.meta` 与起始提交逐字节相同，没有保存场景、修改设置或模型导入器。维护专用入口不作为常规打开项目步骤。静态证据、字体报告和维护过程／指纹摘要写入 `artifacts/helmet-interaction/qa/runs/20261018-pr69-early-fix/`；大备份和完整故障日志保留在隔离目录外的本地维护运行目录，不纳入 Git。

资产维护提交为 `483b2da32a0be5147339d19136324be9379c892f`。首轮组合候选 `68a151cda56fca9efa23180499f4ced7cd7bbda9` 的 LocalizationValidation 为 11/11，但 VisitorEraValidation 在当前实体站进入 1839 英文前窗说明时发现真实正文溢出：字形无缺失、字号已到既有下限 `.28`。该轮 328 项通过、1 项失败，Editor 自然退出 1、无强制终止，退出后 1,291 个 tracked 工程文件零差；保留负向报告，不将静态字形通过写成布局通过。

因此在 `b4af252a8c8dd8e8214e9481a9d2364d8f50b7f9` 同步压缩前窗、帽体与胸板的三语完整卡片，保留照片中的三处带护栏视窗、虚拟开窗铰链、馆藏 1881-47 来源、背面及部件尺寸估算。英文含标题分别为 81／74／82 字符，简繁分别为 35／35／34 字符。没有修改卡片容器、字号下限、实体站或验收断言。JSON、保存表和运行时英文源逐条相等；当前规范字符清单为 613，全部已在保存的 631 字符 SDF 中。620 是前次维护时的历史快照，压缩后未删除保存的历史字形。

第二轮组合候选 `40f9975227f8426c552fb4de67215d77622418ed` 的 LocalizationValidation 仍为 11/11；VisitorEraValidation 已通过早期全部九张三语卡片、早期与 Mark V 的实体射线交互，但在 SuperLite 颈封热点停止，720 项通过、1 项失败。失败前的真实射线首个碰撞体已是 `Breastplate Base`，而 XRI 的多候选 hover 同时含颈封标记；不能据此说按下 Grip 才使控制器偏移。该夹具原本固定从胸板上前方斜射，对较低的 SuperLite 颈封不适用。

`96f977e` 与 `1096be0ca0cc7fc09f02195b7cf749d90ad3a136` 只修正验收夹具：与已有壳体射线夹具使用相同的真实 Physics 查询策略，找到首个碰撞体属于该热点的无遮挡方向；起点距标记 `.45m`，且开放手 `.08m` 包络不得位于场景 solid 中。找不到合法方向就失败，不关闭碰撞或过滤器，不移动产品标记。所有三年代的真实射线 press 断言保留，并增加每次松手恰好一个 selectExited 事件与松手后取消选中的断言。第二轮 Editor 自然退出 1、无强制终止，1,291 个工程文件零差；保留负向报告。

第三轮组合候选 `af1a2854ea391e95e5dd3689dab65ebfadb0d3c6` 在保存繁中胸板截图时发生 `DirectoryNotFoundException`，当时目录存在，目标文件完整路径为 261 字符，较短的前 11 张截图已保存。该环境失败轮 431 项通过、无行为断言失败，Editor 自然退出 1、1,291 个工程文件零差。随后不修改源码，用较短的独立运行名 `ve69` 重跑同一提交；不覆盖故障目录。

短路径轮已在相同 `af1a285` 成功保存该繁中截图，排除产品字形或文案导致第三轮文件异常。该轮继续发现第二个夹具假设：普通细线 Physics 查询首面是标记，不代表保存的 XRI `ConeCast` 查询也以它为首面；在 SuperLite 帽体，实际 ConeCast 首面仍为 `Bonnet Capsule`，不能说是 Grip 后控制器发生移动。该轮 728 项通过、1 项失败，自然退出 1、1,291 个工程文件零差。

`9285c6c78393d0be1c77e9c474b099ed69b8edc8` 将细线查询仅用于候选预筛。每个合法外侧候选先经正常 Input state 输入与等待，再读取生产 `XRRayInteractor.TryGetCurrent3DRaycastHit`；只有首面属于热点且实际悬停才按 Grip，所有候选均不满足则失败。保留 authored ConeCast、ray origin、mask、碰撞体和过滤器；日志增加实际射线位置、方向、模式、mask、trigger 策略与命中距离，真实 select／release 事件断言保留。

`9e2e746` 完整轮在 SuperLite 颈封出现 726 项通过、1 项失败：15 个细线候选未能提供合法外侧视角。没有据此判产品不可达。随后 `e3ff4c8` 运行明确标为 `diagnosticOnly` 的独立诊断，使用 public API 切换 SuperLite，记录模型、热点与碰撞边界：颈封标记中心为 `(0.016390, 0.947997, 3.421865)`，旧基座下缘为 `y=.972`、中央展台顶为 `y=.9`。128 个较粗方向／距离候选中 46 个起点在当时净空规则下可输入，实际 ConeCast 均未先命中标记。这个有限取样不包含浅坡，也将前一候选的实体手误计为场景净空障碍，不能当作全局不可达证明。

`b74e240` 进一步只做有限的 90 个浅坡／距离候选诊断，补记录 `Display Top` 边界，并在几何起点净空查询中排除自有 XR／实体手；实际物理手、Collider、ConeCast 和过滤器均保持启用。诊断入口在报告中明确为 `DIAGNOSTIC ONLY`，不替代完整 VisitorEraValidation，不删除颈封验收断言。

第二有限浅角诊断 `1f1e71b` 的 90 候选有 18 个外侧输入、仍无实际首面命中；新增的中央 `Display Top` 记录给出 `ClosestPoint(marker)==marker`，确认标记中心在台面实体内。没有继续增加候选或关闭实体。

`b92b06aa7aed158662821abffedd7aef07baf7ee` 仅将 MuseumStart 的同一 `NeckDam_Neoprene_510-533` attachment[2] 三个 localPoint override 从 `(1.2779928e-19,-.00083330687,.00029928202)` 改为 `(-1.1419615e-12,.0013711446,.00142229)`。依据是该真实网格最高后侧顶点 `(-1.1419615e-12,.0010211446,.00137229)`，沿世界 `(0,.005,.035)m` 外偏，总距离 35.36mm；邻接后侧面外法线约 `(±.052,.418,.907)`，后向外偏与法线同向。预计世界点从 `(.016390,.947997,3.421865)` 变为 `(.016390,1.060298,3.642310)`，标记球半径 `.0325m` 下沿高于 Base 顶约 5.8mm、上沿低于 Capsule 底约 11.2mm，中心位于 NeckSupport 后缘外约 12.3mm。原 GUID、surface 引用、其他 attachment、模型、Collider 全部保留；偏移小于既有表面检查的 38.5mm 容差。该上后部选择基于网格真实形态，NeckDam 上部并不位于前面。

短后方诊断在冻结组合候选 `7e5b539cdb3faf65a32ce4491172ccdd33e4257d` 中实际通过：27/27 个后／斜后候选由保存的生产 ConeCast 首先命中颈封热点，首候选通过真实 XRRayInteractor select 一次、无整盔旋转、release 一次。实际中心 `(.016390,1.060298,3.642310)` 与计算一致；Display Top 的 ClosestPoint 变为 `(.016390,.960000,3.642310)`，中心已离开台面实体。实际球体相对 Base 顶净距约 5.798mm，相对 Display Top 顶约 67.798mm；Capsule 的实际世界 AABB 下缘为 `1.098766`，比静态初估低，保守球体上缘净距为 5.968mm，真实胶囊表面距离更大。此轮明确为诊断，不计作完整年代验收；Editor 自然退出 0、1,291 个 tracked 工程文件零差。

同一 `7e5b539` 随后的完整 VisitorEraValidation 为 **959/959**，无断言失败，报告 errors／warnings 均为空，保存 34 张诊断图。覆盖三年代往返、全部三语图文与保存字形、生产射线的前窗开合、九个热点实际首面／press／release、剖切开关与轨道深度、真实壳体旋转与中心固定、复位保留年代／语言／发现记录；原持窗延迟切换断言和新 OBJ 的六组真实三角面接触也通过。自然退出 0、无强制终止，退出后 1,291 个 tracked 工程文件零差。人工已逐张复核早期九张完整三语卡片，未见缺字方块、正文截断或卡片越界；早期模型外观／剖面与标题的单眼诊断图可见且 shader 检查通过。

完整退出日志另行检查：有既有许可启动提示及无头显导致的 `XR_ERROR_FORM_FACTOR_UNAVAILABLE`／OpenXR Display 初始化失败，未发现产品 C# 异常、断言、shader 编译错误或退出清理异常；不能把报告 errors=0 写成整个原始日志没有 error 字符串。同一 `7e5b539` 的 HelmetDiegeticValidation 为 **819/819**，报告 errors 为空、failure 为空；Editor 自然退出 0、无强制终止，1,291 个 tracked 工程文件零差。完整日志同样未见产品或清理阶段异常。Operator／Direct／PhotoComparison 组合验证由整合人追加。

按三语接入指南追加运行 LocalizationIntegrationValidation，初轮在 `7e5b539` 为 411 项通过、1 项失败：旧 PR9 结构检查使用只查启用对象的 FindObjectOfType，漏掉已由当前实体站隐藏的 HelmetInspectionPalette 上旧 progressive 组件。保留负向报告；自然退出 1、无强制终止，1,291 文件零差。`3a816a853a4eab6eae80d8d62a720d2f7eab25e0` 仅改该 Editor 验收文件：当前实体站先通过真实生产 LanguageButton 射线输入检查三语切换、图示播放延续、可见实际译文／保存字形／布局；原 PR9 渐进流程、热点和三语 helper 全部断言随后在明确的可选旧入口夹具执行，finally 与提前失败 Finish 均恢复修改的 activeSelf／enabled，再检查恢复后的当前入口和真实语言输入。未改变产品、Station、ray／Collider／过滤器或共享 helper。既有 TrilingualMuseumValidation 的临时截图输出仍在 tmp/trilingual-qa，此限制在报告中明示；有用证据另收至独立 QA run。

最终 LocalizationIntegrationValidation 在精确组合源 `aa9dba38e9d6d7ea3fd0d3f77385ae844dcdd06e` 为 **122,830/122,830**，errors／warnings 为空，包含原热点 direct／ray、语言播放连续性、三语完整原流程与当前 Station 恢复后生产输入断言；没有删除旧断言来通过。Editor 自然退出 0、forced=false，退出后 1,291 文件零差。完整日志从编译、启动、Play 到自然退出已核：仅既有许可和无头显 OpenXR 环境提示，未见产品 C#／shader／清理异常。原 27MB 完整报告、进程及指纹由整合人统一收在 `artifacts/helmet-interaction/qa/runs/20261018-pr65-66-69-combined/localization-integration-aa9dba3/`，本次自有 QA 只保留摘要／原始 hash／引用。

Unity 使用隔离工程；Library 位于 E 盘 junction，82 份 LFS 工作区资产全部展开。维护使用已完全退出的 PR #65 Editor 基线 Library 副本，SDK 缓存仅补复制；早期 OBJ、修改后的字体和代码在本工程实际重新导入／编译，不称为整库冷启动。未将 #65 的未通过源码混入 #69。

## 限制和交接

桌面合成 XR、公开 API 夹具和单眼诊断图不能证明真实头显手感、双眼可读性、舒适度、性能、学习效果或 Windows 可执行构建通过。早期模型仍是单照片驱动的解释性研究模型，没有重建历史内部结构。最终远程 PR 状态、合并 SHA、与 #65／#66 的组合验证及原工程同步由整合记录确认；本子任务不推送或合并远程 PR。

首轮合并时 GitHub 主分支保护要求至少一位具有 write 权限的成员批准，整合人实际合并 #66 曾被该条件阻止，未绕过或伪造批准。其后用户明确授权整合人自行审批；整合人使用 Twispra 的 admin 账号提交标注 AI 评审的正常批准，`bypass=false`，已将 #66 合入 `88ca9c387cd734ab8e6fb7aca84c07d459e2ffea`、#65 合入 `d078ab4f8064b50ffbde90e3d6fd3d5630133898`。这不称为独立真人审查。

本 worktree 已 fetch 核对该实际 main，并正常 merge 为 `2b1eeea253cd3176600cd63115aec5ba45fc6f0f`。与运行冻结的组合候选 `7e5b539cdb3faf65a32ce4491172ccdd33e4257d` 比较，整个 `MuseumVR/` 的 Git tree 没有差异；包括当前修复、测试源码、模型、场景、文字与字体，且 LFS checkout 已完成。#67 仍未纳入；#69 的最终验收、推送／审批／合并与原工程同步由整合记录确认，本记录此处尚不写为 #69 已合并。

最终本 PR 源码为 `3a816a853a4eab6eae80d8d62a720d2f7eab25e0`，与最终组合源 `aa9dba38e9d6d7ea3fd0d3f77385ae844dcdd06e` 整个 `MuseumVR/` tree 相同；后者相对 Visitor／Diegetic 被测 `7e5b539` 只改变 LocalizationIntegrationValidation Editor 一文件。因此产品未变，已有受影响行为结果可沿用；LocalizationValidation 的五个输入／验收文件 hash 同一性另有 QA 记录。最终证据提交仅增加活动／QA，不改变被测产品。
