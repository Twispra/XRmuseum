# 头盔观察热点

基准为 `origin/main` 的 `4fe7252`。本任务新增独立观察热点功能并将其作为嵌套 Prefab 接入 `MuseumStart.unity`，没有修改现有头盔 Prefab、模型、公共 XR 输入、Packages 或 ProjectSettings。`Assets/Museum/Features.meta` 沿用未合并观察功能分支的 GUID，减少后续合并冲突。

## 实现

- `HelmetHotspotTour.prefab` 包含前窗、上部外壳和胸甲三个可选择热点，以及信息、进度和完成反馈。
- 每个热点首次选择计入一次进度，重复选择只重新显示信息；重置会清空进度和访问状态。热点同时响应 XR Select 和 Activate，支持模拟器 G 键及鼠标左键扳机输入。
- 热点使用 Trigger Collider。功能启用时将现有 XRI Ray / Direct 查询临时设为 `Collide`，并允许 Ray Interactor 对悬停对象发送 Activate；停用时恢复原值，不修改共享 Prefab 或场景资产。
- 功能目录内含运行时代码、生成工具、材质、Prefab 和 EditMode 测试。新 C# 文件没有代码注释。

## 接入

`Assets/Museum/Features/HelmetHotspots/Prefabs/HelmetHotspotTour.prefab` 已作为 `MuseumStart` 中 `HelmetExhibit` 的子对象接入，保持局部位置和旋转为零。若后续改用未合并的 `InspectableHelmet`，整合者需要将同一 Prefab 移到新头盔根对象下，并逐项合并场景引用。

## 验证

Unity `2022.3.62f3c1` EditMode 测试共 9 项通过，覆盖唯一计数、重复选择、完成事件、重置、无效 ID、Select 和 Activate 热点激活、Prefab 结构，以及 XRI Trigger 查询与 Hover Activate 的启用和恢复。两个回归用例分别在修复前失败、修复后通过。主场景已保存嵌套 Prefab 引用；本次未完成主场景 Play Mode 启动检查。未进行真实头显测试或 Windows Player 构建。

本功能由 Codex 辅助设计、编码和自动测试。信息文字只描述当前灰模可见部位，不主张未经来源支持的历史细节；个人反思与人工设备结果仍由成员本人完成。
