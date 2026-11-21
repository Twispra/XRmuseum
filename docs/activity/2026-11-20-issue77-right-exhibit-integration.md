# #77 右侧展台正式接入

## 范围与方案状态

- 关联 [#77](https://github.com/Twispra/XRmuseum/issues/77)，基线 `5ddaab5a101a153a0d864e7765df31098f64bc49`，分支 `codex/issue77-right-exhibit-integration`。
- 本次范围为展板接入、Unity 验证和 PR 准备；负责人 Cai Zishen（`r2kh8sck4y-bot`）。开工时 checkout 干净，没有其他开放 PR 修改本次目标路径。
- 共享场景本轮写入范围仅为 `MuseumStart.unity` 的右侧展台：新增一个既有 Prefab 实例及其父级引用，停用两条被替代的前侧展签。保留中央、左台、现有管线、顶部标识、XR／Input、Packages 和 ProjectSettings。
- [#64](https://github.com/Twispra/XRmuseum/issues/64) 尚未记录全组采用决定。本次接入实现方案 B，提交组内审阅；本次实现和自动检查不代表全组批准。合并前仍需在 #64 记录采用意见。

## 完成后的行为

启动正式构建入口 `Assets/Museum/Scenes/MuseumStart.unity` 即可看到右侧展板，无需 Play Mode 菜单临时挂载。展板复用主线已有的 `RightExhibitPanel.prefab`，没有将旧有设计计作本次新建成果。

- 水面供气泵 → 潜水头盔：单向供气。
- 水面团队 ↔ 潜水员：双向通话。
- 标明教学示意，并以左向箭头提示中央 Mark V 演示。
- 复用简体、繁体和英文九个标签、既有本地化通道和字体，颜色只作辅助。
- 两条旧前侧展签保存为禁用，避免重复文字；再次运行预览复用同一实例。

接入位置为 `Exhibit Placeholders/Exhibit 03 - Placeholder/Right Exhibit Panel`，局部坐标 `(3.10, 0.74, 2.801)` 米。原 Prefab GUID `9df2132b28acef948ae7fe343deb7098` 保持。独立的黄昏工房和 SurfaceCall 场景不在本次接入范围。

## 验证与证据

被测源码 `5c8e35e05f41332aba42695df425493d4095f6f1`，dirty 无；两个 Unity 进程均自然退出 0。环境为 macOS / Apple M5 / Metal / Rosetta，Unity 2022.3.62f3c1。

| 验证 | 本轮结果 |
| --- | --- |
| `Museum.RightExhibit.Editor.RightExhibitPanelValidation.RunBatch` | 1,102 项通过：保存场景真实包含实例、Edit Mode 预制件来源、唯一实例、旧标签禁用、三语字形与排版、箭头、四眼位布局和物理通路 |
| `Museum.Localization.EditorTools.VisitorEraValidation.RunBatch` | 1,132 项通过，0 项检查错误／警告：三年代切换、提示点和 Mark V 演示入口 |
| 测试后来源保存 | 1,446 个工程文件 SHA-256 与测试前一致；Git 状态保持干净 |
| LFS 与引用 | 111 路径均已展开并匹配 SHA-256；原展板、材质、本地化表、meta／GUID 沿用主线 |

[本次报告、运行清单和三语截图](../../artifacts/helmet-interaction/qa/runs/20261120-issue77-right-exhibit-integration/README.md)。首次检查时序失败和修正见证据说明，未覆盖历史报告。Unity 保存引入的无关材质／默认字段／空白重写已移出本次 diff，只保留接入所需的序列化内容。

检查入口现在读取保存场景，不会为缺失的展板临时创建实例后再报告通过。诊断相机只用于截图，产品相机和测试位姿没有被保存。未验证真实头显双眼、控制器触达与舒适度、性能、Windows Player 构建或用户研究。

## 查看与交接

1. 使用 Unity 2022.3.62f3c1 打开本分支的 `MuseumVR`。
2. 打开 `Assets/Museum/Scenes/MuseumStart.unity`，进入 Play。
3. 查看右侧展台正面；用入口语言按钮或 F6／F7／F8 切换三语。
4. 在中央 Mark V 档查看供气／通话入口；切到早期与现代档时入口应隐藏。
5. 审阅 PR、在 #64 记录方案结论，再由另一位成员审核合并；真实设备验收单独补充。

接入已保存，不需要重新生成展板或运行原型重建工具。源模型、导出、Unity 模型副本无变更，没有新增第三方素材或运行时依赖。AI 辅助范围见[外部资源记录](../external-resources.md)。
