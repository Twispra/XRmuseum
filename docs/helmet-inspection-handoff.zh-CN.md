# 头盔观察功能：给组员的交接

这次把前窗开合、可调剖面和近距离操作接入了原来的 `MuseumStart`。访客可以单手拿近头盔、任意转动、松手悬停，再用同一只手调切面。操作菜单会在附近找空位，瞄准或抓取时保持不动。

分支：`codex/helmet-inspection-2026-09-29`。开发前基准：`4fe725294150a39ceba411f0d5088b51697e85ee`。功能源码提交：`3cf6f8ddb620f1943cef416a5eb4d72134a586ec`；之后的交接提交仅加入文档和验证证据。提交账号为 `Fttt1003`，AI 辅助范围已记录，尚需另一位组员审查后合并。

## 拿到分支后怎样试

1. 在 GitHub Desktop Fetch origin，切换到上述功能分支。若自己有未提交改动，先在自己的任务分支保存，避免直接覆盖。
2. 确认 Git LFS 资源已展开，用 **Unity 2022.3.62f3c1** 打开仓库中的 `MuseumVR`。
3. 打开 `Assets/Museum/Scenes/MuseumStart.unity`，按 Play。原展厅、原 XR Rig 和摄像机就是体验入口。
4. 先试 **FLOAT → 抓起头盔并转动 → 松手 → SECTION → ALIGN VIEW → EDIT CUT**。握住青色球柄后，移动手改变深度、转动手改变切面方向。
5. 点 WINDOW 或直接操作前窗；随后用 RESET / Home 检查回到展台、关窗、恢复外壳和普通物理。

完整按钮和模拟器操作见[使用说明](helmet-advanced-inspection.zh-CN.md)。`HelmetInspectionDemo` 仅是开发辅助场景，不是小组展厅入口。正常试用直接打开保存好的场景，**不要运行 Build / Integrate / Upgrade 等生成菜单**。

## 对旧项目改了哪些地方

| 范围 | 当前状态 |
| --- | --- |
| 原 FBX、`HelmetExhibit.prefab`、复位脚本、旧验证器 | 文件未修改 |
| 公共 XR 样例、Input Actions、Packages、ProjectSettings | 文件未修改；未升级版本或迁移平台 |
| 原摄像机、移动配置、地板、展台和另外两个展品 | 主场景中的原配置保留；移动/传送仍需要真实设备补测 |
| 中央展品 | 改用功能目录中的 `InspectableHelmet.prefab`；仍使用原 FBX、四个实体碰撞体和普通抓取参数 |
| 原 RESET 按钮 | 重新绑定到新实例的原复位组件，仍调用原复位逻辑 |
| 主场景 XR 查询 | 3 个 Ray（含 Gaze）与 2 个 Direct 查询添加 Collide 覆盖，使前窗/切面工具的 Trigger 能被选中；没有修改共享 XR Prefab |
| 新功能 | 前窗、剖面、悬停、面板与材质集中在 `Assets/Museum/Features/HelmetInspection/` |

按 YAML 对象 ID 比较，原场景 141 个对象块中有 4 个保留块发生变化：RESET 引用、说明牌、XR 查询覆盖和展台子节点；另移除旧头盔实例的 3 个引用块。新场景共有 318 块，增加部分主要来自功能面板及其文字。原 `.unity.meta` / 场景 GUID 保留。新资源 `.meta` 和引用检查通过，未替换或上传新的模型二进制文件。

有些行为是本次功能有意改变的：瞄准窗面时优先开窗；FLOAT / TILT 会使头盔悬停；RESET 恢复普通物理。新材质支持剖切和内侧显示，其 Shader 没有 ShadowCaster Pass，不能说阴影与原 URP Lit 完全一致。这些与“旧核心拿放和复位是否回归”分开评价。

## 后续开发改哪里

- **整盔状态、复位联动、切面参数和悬停：** `Runtime/HelmetInspectionController.cs`。
- **前窗手势：** `Runtime/HelmetWindowInteractable.cs`。不要再给活动窗加一个自由运动刚体。
- **切面手柄：** `Runtime/HelmetSectionInteractable.cs`；**随身菜单：** `Runtime/HelmetInspectionPalette.cs`；按钮绑定在 `Runtime/HelmetInspectionControls.cs`。
- **视觉效果：** `Shaders/InspectionShell.shader` 与本功能 `Materials/`。目前只处理已有灰模，不包含未经史料支持的内部设备。
- **主展厅实际使用的 Prefab：** `Prefabs/InspectableHelmet.prefab`。只改旧 `Assets/Museum/Prefabs/HelmetExhibit.prefab` 不会自动更新这个独立副本；涉及公共模型或抓取参数时，需要同步评估两个 Prefab。

以上相对路径均以 `Assets/Museum/Features/HelmetInspection/` 为起点。保留 `.meta`，不要让 AI 重建资源来“解决”引用问题。

## 回归证据与剩余工作

本轮在源码提交 `3cf6f8d` 上重新验证，源码、场景、Packages 和 ProjectSettings 没有额外未提交变化；报告中的 dirty 状态来自待交接文档和本地历史记录，不代表源码未提交。报告与选取截图见[活动记录](activity/2026-09-29-advanced-inspection.md)。静态审查和自动 XR 输入均由 AI 辅助执行，不代替组员审查。

可重复运行的 Editor 验证入口：

| 入口 | 用途 |
| --- | --- |
| `Museum.Inspection.Editor.HelmetInspectionValidation.RunMainBatch` | 原主场景中的前窗、剖面、悬停、近距面板和复位 |
| `Museum.Editor.HelmetPrototypeValidation.RunBatch` | 未修改的旧验收：10 次拿放、旋转、Home / RESET、掉落恢复等 |

批处理使用 `-batchmode -projectPath "<仓库>/MuseumVR" -executeMethod <入口> -logFile <本地日志>`，不要加 `-quit`，验证器会在结束后退出。关闭同一工程的可见 Unity 后再运行，报告写入独立的 `artifacts/helmet-interaction/qa/runs/` 目录。

尚未完成：真实头显的双眼剖面、手柄手感、坐姿触达、性能与完整学习任务，以及新 Windows Player 构建。剖面保留完整物理碰撞包络，当前用于观察，不能穿过隐藏外壳抓内部零件；拥挤位置没有空位时，菜单保留上一个位置。这些均不应写成已验收。

合并前由另一位组员看 PR 与上述证据，按操作顺序走一遍。若 main 同时改过 `MuseumStart.unity`，由一个整合人逐项合并引用，不能整文件接受 ours/theirs。本分支没有未合并功能依赖。若需要撤回合入后的功能，用常规 revert PR 处理场景及功能资源；不要删除共享 FBX 或改写历史。
