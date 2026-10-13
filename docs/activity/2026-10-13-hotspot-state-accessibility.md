# 2026-10-13 头盔热点状态无障碍优化

## 范围

本轮对应 [Issue #45](https://github.com/Twispra/XRmuseum/issues/45)，基于 `main` 的 `37930ae235b6bfd00f9575805c759614a0cec6a4`。目标是在保留青／黄／绿色反馈的同时，让热点状态不再只靠颜色区分：悬停增加外环，访问后增加勾形标记；可用状态维持原圆点。

修改限于 `HelmetHotspot` 运行时状态表现及对应测试，没有修改 `MuseumStart.unity`、热点 Prefab、公共 XR / Input Actions、`Packages/`、`ProjectSettings/` 或模型资产。根目录原有未提交材质、XRI Prefab 和 XR 设置改动没有进入本工作区。

## 实现与行为

`HelmetHotspot` 在运行时为有可视 Renderer 的热点创建两个无 Collider 的 LineRenderer 提示。悬停外环和访问勾形标记始终朝向当前主相机；颜色仍沿用原状态色，但形状本身提供独立信息。热点因激光离开头盔而隐藏时，圆点、外环和勾形标记一起隐藏；再次显示、重置、禁用／启用组件和重复访问时会恢复正确状态。

## 验证

- 测试驱动：新增测试在修改前因不存在 `State Cues/Hover Outline` 而失败，修改后通过。
- `Museum.Hotspots.Tests`：19 / 19 通过，0 failed，0 skipped；[结果](../../artifacts/helmet-interaction/qa/runs/2026-10-13-hotspot-state-accessibility/hotspot-tests.xml)。
- 保存主场景回归：77,657 项自动断言、0 errors、0 warnings；热点子流程 786 项检查、20 次 Direct / Ray 选择通过；[热点报告](../../artifacts/helmet-interaction/qa/runs/2026-10-13-hotspot-state-accessibility/hotspot-interaction.json)。大量逐帧、逐字符检查不等于同数量独立用户操作。
- 诊断渲染确认可用圆点、悬停外环和访问勾形三种轮廓可区分；[对照图](../../artifacts/helmet-interaction/qa/runs/2026-10-13-hotspot-state-accessibility/state-cue-preview.png)和[摘要](../../artifacts/helmet-interaction/qa/runs/2026-10-13-hotspot-state-accessibility/summary.json)。

## 限制与下一步

本轮未连接真实头显；自动输入和单眼诊断图不能证明双眼可读性、坐姿触达、舒适度、色觉用户实际识别效果或当前 Windows 构建通过。上传前仍需人工审核差异；合并后建议在 Quest Link 实机中检查外环和勾形在三个热点角度、不同观察距离及复杂背景下是否清晰。
