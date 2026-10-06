# 2026-10-03 头盔年代切换模块

用户要求实现「**通过按钮切换不同年代、不同风格的潜水头盔**」。这是组长 2026-09-29 在群聊中明确列出的自由发挥方向之一，同时命中了组长那条硬要求：「**这个逻辑我觉得要做成可迁移的，因为现在还是白模，以后改模型或者不同年代的都可以用。**」

本次交付为**独立模块** `MuseumVR/Assets/Museum/Features/HelmetEras/`，分支 `codex/helmet-eras-2026-10-03`，基于 fetch 后的 `origin/main = 8e75e71`。

## 交付内容

| 关注点 | 放在哪里 | 换模型时要改代码吗 |
| --- | --- | --- |
| 一个年代有哪些事实、材质、说明 | `HelmetEraProfile` 资产（ScriptableObject） | 不用，只改资产字段 |
| 哪块网格对应哪一类表面 | `HelmetEraSwitcher` 的绑定列表 | 不用，重新绑定即可 |
| 切换时其他系统怎么响应 | `HelmetEraChannel` 事件通道 | 不用，订阅关系不变 |

```text
Assets/Museum/Features/HelmetEras/
  Runtime/  HelmetEraProfile.cs         一个年代的数据
            HelmetEraCatalogue.cs       年代清单、排序与环绕切换
            HelmetEraChannel.cs         事件通道，向其他模块广播当前年代
            HelmetEraSwitcher.cs        把资料材质套到渲染器上，可还原
            HelmetEraButton.cs          可抓取的切换按钮
            HelmetEraPlayModePreview.cs 无头显时的键盘预览
  Editor/   HelmetEraSetup.cs           一键生成资产与展示 Prefab
            HelmetEraValidation.cs      自动校验（不打开主场景）
  Profiles/ Prefabs/ Materials/         由 Editor 脚本生成
```

**隔离性**：模块**刻意不修改 `MuseumStart.unity`**。展示 Prefab 自带切换器与按钮，不引用主场景任何对象；接入主场景由整合人另行执行。完成后 `git status` 中 `MuseumStart.unity` 无改动。

## 本次验证（编辑器内）

Unity 2022.3.62f3c1（团结引擎），Windows Editor / Direct3D 11。

| 步骤 | 结果 |
| --- | --- |
| 脚本编译 | 通过。首次打开时暴露 1 个编译错误，已修复（见下） |
| 菜单 `Museum → Helmet Eras → Create Era Assets and Showcase` | 成功生成 3 个年代资产、图鉴、通道、9 套占位材质、展示 Prefab |
| 新建空场景（Basic URP）拖入展示 Prefab，进入 Play | 屏幕可见头盔三个部件 |
| 键盘 `N` / `P` / `1` `2` `3` | **三个年代外观明显不同**，切换正常 |
| Inspector 组件右键菜单「预览/切换年代」 | 同样正常 |

这是 **Unity 单眼渲染 + 键盘输入的编辑器内检查**，不是头显实测。

## 验证过程中修复的三个缺陷

三个都是**纯读代码发现不了、必须实机跑起来才暴露**的问题，按发现顺序：

1. **编译错误**：`HelmetEraValidation.cs` 引用了 `report.OutputPath`，而字段名是小写 `outputPath`。
   首次打开工程时 Unity 以 Safe Mode 拦下并报错，修复后编译通过。
2. **三个年代没有配置任何材质**：生成脚本只写了文字字段，`shellMaterial` / `windowMaterial` /
   `fittingMaterial` 全为空。结果是切换年代**画面毫无变化**。
   修复：新增 `EraLooks` 配色表与 `CreateEraMaterials()`，为每个年代生成
   `<年代>-Shell / -Window / -Fitting` 共 9 套占位材质并写入资料；
   同时清理早期版本遗留的 `EraShellWarmCopper` 等三个人造材质。
3. **淡变把物体刷成白色**：`SetRenderersVisible()` 用 `MaterialPropertyBlock` 把 `_BaseColor`
   硬写成 `Color.white` 再改 alpha，但 **URP 不透明材质会忽略 alpha**，结果只是把物体刷白；
   而属性块**从不清除**，一直压在渲染器上，于是**切换后外观永远是白的，三个年代看起来完全一样**
   （切换逻辑其实一直在正常工作）。
   修复：改为以材质自身颜色为基准做明度变化（最低压到 35%，避免黑闪），
   并在恢复完全不透明时 `SetPropertyBlock(null)` 清除覆盖；换材质前也先清一次。
   同时把 `transitionDuration` 默认值改为 **0**（直接切换），最不容易出意外。

## 无头显成员的可验证性

组内 6 人中有 2 人没有 Quest 3。要验证按钮需要搭建 XR Origin + XR Device Simulator 四个预制体，
对不熟悉 Unity 的成员门槛偏高。因此模块自带了键盘预览（`HelmetEraPlayModePreview`）：
Play 模式下 `N` 下一个、`P` 上一个、数字键 `1`/`2`/`3` 直接跳转。
键盘路径与 XR 按钮**走同一条切换逻辑**，所以它能验证材质替换链路，但不验证射线交互本身。
另外 `HelmetEraSwitcher` 的三个公开方法带了 `[ContextMenu]`，可在 Inspector 右键直接触发。

## 与课程和客户要求的对应

| 依据 | 对应 |
| --- | --- |
| 组长 2026-09-29：逻辑要可迁移 | 数据全部放在 ScriptableObject；新增年代或换模型**不需要改代码** |
| 组长 2026-09-29：通过按钮切换不同年代的潜水头盔 | 本模块即该方向的实现 |
| 客户回复 A5：年代叙事还是自由探索「由你们提出建议」 | 本模块提出的正是用**年代切换**串起时间线这一方案 |
| 客户回复 A3：「一个物件也可以，但要看访客能从中发现什么」 | 一顶头盔切换三个年代，**在有限场地内成倍放大可发现内容** |
| 客户回复 A3/A6：史料必须可溯源、不能暗示历史不支持的东西 | 每个年代资产带 `factSource` 字段，**分开记录史料口径与团队解释**；占位材质明确标注非历史复原 |

## 尚未验证的范围

- **没有在真实头显上验证过**。按键舒适度、体感延迟、文字清晰度均未测。
- **自动校验工具已实现但本轮没有运行**（`Museum → Helmet Eras → Validate Era System`），
  因此没有产出 `artifacts/helmet-eras/qa/runs/` 下的报告。
- 未做学习效果、无障碍或参与者评价。
- 9 套材质是**占位外观，不是历史复原**；三个年代的差异目前只有颜色，**没有造型差异**。
- 尚未在 PQ604A/B/C 机房环境构建或运行。
- 表格里 1839 / Mark V / SuperLite 17B 的年代与结构描述为**团队采用的公开资料口径**，
  不是本组测量，也未经馆方确认。

## 接入主场景（由整合人执行）

1. 把 `HelmetEraSwitcher` 挂到头盔展品对象上，指定图鉴与事件通道。
2. 在绑定列表里填入壳、前窗、配件对应的 `Renderer`，并逐项核对子材质序号。
3. 把两个按钮放到展台附近，或复用现有面板的空位。
4. 重新运行主场景既有验收，确认抓取与复位行为未受影响。

## 后续方向

把三个年代的差异从「颜色」推进到「造型」：让每个年代声明自己存在的部件
（例如 1839 无胸板、Mark V 四窗加胸板、SuperLite 用颈环），切换时隐藏不存在的部件。
`HelmetMaterialSlot` 已经是语义化槽位（Shell / Window / Fitting / Breastplate），
这条路径不需要新的美术资源即可先看到结构差异。
