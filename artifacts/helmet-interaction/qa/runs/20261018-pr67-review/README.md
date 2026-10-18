# PR #67 保存主场景复审证据

来源、修复、授权、原目录保留及设备限制见[活动记录](../../../../../docs/activity/2026-10-18-pr67-review-and-merge.md)。最终通过项由 `final-audit.json` 汇总；各项必须同时满足报告全通过、Unity 自然退出 0、forced=false、1299 个 tracked MuseumVR 文件前后无差、完整本地日志无应用异常／编译错误／断言或 shader error 匹配。检查次数含保存源指纹及重复字形断言，不能相加当独立用户用例。

## 失败迭代（保留原结论）

| 目录 | 被测提交 | 结论与作用 |
| --- | --- | --- |
| negative-d7ed9d5-compile | d7ed9d5 | 新验证器名称冲突编译失败，没有行为报告 |
| negative-8016e13-cross-target-premise | 8016e13 | 行为无重选／无拖动；内部 filter 前提不符合 StateChange 语义 |
| negative-c071766-cone-blocker-premise | c071766 | 未执行 Grip；Cone 首结果不能当物理最近面 |
| negative-a24f397-solid-action | a24f397 | 实体穿挡后实际确认并转 30°，产品反例 |
| negative-a24f397-panel-overlap | a24f397 | 实际眼位面板包络穿模型且视线被挡，产品反例；含图片 |
| negative-42f8c77-language-fixture | 42f8c77 | 遮挡／同帧取消／生命周期／Direct 已过，但夹具错误恢复未追踪时射线停用状态，整轮失败 |

## 证据格式

每个运行保留 `report.json`、`process.json`、`source-after.json`、`provenance.json` 及少量有用图片。provenance 记录原文件及保存 LF 版本哈希、完整本地日志位置／哈希和异常匹配。大于 8MB 的报告将完整原 LF JSON 无损保存为确定性 `report.full.json.gz`，`report.json` 仅作带哈希的摘要；没有丢弃逐项检查。运行器及完整日志留在活动记录列明的本机独立证据目录，正面结果由最终审计再次核对。

输入为桌面 queued XR/HMD/键盘状态，经正常 Dynamic 更新、生产 Input Actions 和 XRI 选择事件；Direct fixture 临时停射线并恢复。公共设置开关、手势预置和观察发现预置明确标为 fixture，不算实际按钮选择。图像为保存产品场景的临时单眼诊断相机，不是头显截图。没有生成或保存产品场景、模型、字体、Prefab、Packages 或 ProjectSettings；没有真实设备、舒适性／触达、性能、用户研究或 Player 构建结论。
