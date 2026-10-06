# 2026-10-05 年代模块三处缺陷修复（回应 Issue #15）

分支 `codex/helmet-eras-2026-10-03`（PR #10），审查基线 `a339fb42`，主分支基线 `b40eb9f`。

## 1. 淡变中禁用后无法恢复一致状态

`OnDisable` 只是 `StopCoroutine`，而 `currentIndex` 在淡变**开始之前**就已更新；
淡变留下的 `MaterialPropertyBlock` 颜色覆盖也没有清理。
禁用再启用后：索引指向新年代、材质仍是旧年代、渲染器还压着淡变时的颜色；
再对同一年代调用 `ShowEra` 又会因为「index 相同」提前返回，**永远回不到一致状态**。

**修法**：把「被取消的切换」明确**完成**。新增 `CancelPendingSwitch(bool completeTarget)`，
`OnDisable` 调用它并传 `completeTarget: true`：停协程 → 套用目标年代材质 →
补发年代变化事件 → 清掉所有临时颜色覆盖。材质、索引与事件通道随即一致。

## 2. 新瞬时切换被旧淡变覆盖

原实现在**瞬时分支的 `return` 之后**才 `StopCoroutine`，旧协程继续活着，
稍后把材质与通道改回上一个年代，而 `currentIndex` 已经指向新年代。

**修法**：把「取消旧转换」提到 `currentIndex` 更新之后、分支判断**之前**，
一次覆盖所有路径 —— 淡变、`duration = 0` 瞬时切换、`fadeDuringSwap = false`。
瞬时分支额外补一次 `ClearAllOverrides()`，避免旧覆盖压住新材质。

## 3. Mark V 接头用途文案错误

原文案把后部两个鹅颈接头写成「分别用于供气与排气」。按美国海军水下博物馆的公开说明：
**从后方看，左侧是电话连接、右侧连接供气软管**，调节排气阀是另一个部件。
保存资产与生成源两处同步修正为：

> 从后方看，左侧鹅颈接头接电话，右侧接供气软管；排气阀是另一部件。

## 验证

- 本轮**未在 Unity 里运行**（修复者侧无法驱动编辑器与 Play 模式）。
- 已复核：三处改动都落在 Issue 指明的位置；保存资产与生成源的 AIR 文案完全一致；
  `ShowEra` 里「取消旧转换」位于所有分支之前，`OnDisable` 走收敛路径。
- 需要在 Unity 里做的定向验证（Issue 验收清单）：
  1. 默认瞬时切换与 Next / Previous 环绕仍正常；
  2. 淡变中禁用 / 启用、重选目标后无残留覆盖，材质 / index / channel 一致；
  3. 淡变中改为瞬时请求后最终保持最后目标，覆盖 `duration = 0` 与 `fadeDuringSwap = false`；
  4. 保存资产的 Mark V 文案与生成源一致。
- 未做真实手柄 / 头显测试；未在 PQ604A/B/C 构建。
