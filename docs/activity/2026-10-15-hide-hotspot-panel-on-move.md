# 2026-10-15 移动头盔时隐藏热点解释框

## 范围

本轮对应 [Issue #54](https://github.com/Twispra/XRmuseum/issues/54)，基于 `origin/main` 的 `4d8796bbeeabd44f28517f52d16e4dcc5b84a0c8`。用户选择热点后仍显示对应解释；开始抓取整盔时，解释框立即隐藏并保持隐藏，直到再次选择任一热点。

修改限于 `HelmetHotspotTour`、对应 EditMode 测试、无障碍行为说明和本次验证证据。没有修改 `MuseumStart.unity`、热点 Prefab、公共 XR / Input Actions、`Packages/`、`ProjectSettings/`、模型或材质；根目录原有未提交改动没有进入独立工作区。

## 实现

`HelmetHotspotTour` 在启用时查找父级 `XRGrabInteractable` 并监听整盔 `selectEntered`。已有热点说明存在时，抓取开始会停止解释计时并把面板透明度设为零；松手不会恢复旧内容。下一次 `Visit` 沿用原有入口恢复透明度并重新开始 30 秒保持与 1 秒淡出计时。组件禁用时解除监听，避免重复回调。

## 验证

- 测试驱动：合成层级测试在修复前因抓取后透明度仍为 1 而失败；保存主场景接线测试同样先失败，恢复修复后通过。
- Unity 2022.3.62f3c1 / Windows Editor：`Museum.Hotspots.Tests` 共 23 项，23 passed、0 failed、0 skipped；[结果](../../artifacts/helmet-interaction/qa/runs/2026-10-15-hide-hotspot-panel-on-move/hotspot-tests.xml)。套件覆盖抓取隐藏、松手保持隐藏、再次选择恢复、30 秒淡出、三种热点状态、Direct/Ray 兼容设置以及保存主场景父级抓取接线。
- 最新主场景完整回归在本改动与临时撤掉运行时代码的基线对照中，都停在同一既有断言 `ordinary shell remains directly grabbable`；两次失败报告仅保存在本机临时目录，没有作为本功能通过证据提交。因此本轮不宣称完整主场景回归通过。

## 限制与下一步

未进行真实头显、双眼可读性、实际手柄拿放或当前 Windows 构建验证。上传前仍需人工审核差异；合并前应由共享交互负责人确认最新主线的普通壳体抓取回归问题，不能把本轮 23 项热点测试替代完整设备验收。
