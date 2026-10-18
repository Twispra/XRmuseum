# #65 / #66 / #69 联合运行证据

完整说明与版本边界见 [活动记录](../../../../../docs/activity/2026-10-18-pr65-66-69-fix-and-validation.md)。每个子目录保存一个独立运行，不覆盖失败证据。`negative-*` 为失败，`diagnostic-*` 为有限诊断，其他目录以报告的断言与自然退出状态判断。

- `visitor-7e5b539`：完整三年代 959 项与选定卡片/模型截图。
- `diegetic-7e5b539`：819 项生产实体 UI。
- `operator-7e5b539`：255 项个人面板，选定五张截图。
- `direct-7e5b539`：31 项生产物理手直接接触。
- `comparison-7e5b539`：318 项保存比较场景与六张状态图；报告内 sourceCommit 由外层进程清单的精确 SHA 补足。
- `localization-40f997`：11 项三语模块；后续文字/字形/模块依赖未变。
- `localization-integration-aa9dba3`：122,830 项当前站点与独立传统流程检查，明文摘要及无损 gzip 全报告。
- `negative-7e5b539-legacy-localization`：旧 PR9 入口退场后集成夹具前置失败，411 项通过 / 1 项失败；属于失败历史。
- 其余 `negative-*` / `diagnostic-*`：布局、实际射线、文件路径和热点遮挡定位过程，具体因果见活动记录。

每组 `process.json` 记录精确 commit、启动命令、进程退出码与是否强制停止；`source-after.json` 记录全部 1,291 tracked 工程文件零差或明确差异；`provenance.json` 记录源路径、原始文件/日志哈希、保存副本哈希与 C# 异常/编译错误匹配。没有提交许可原始日志；原日志内存在无头显 OpenXR 等既有环境消息，“无意外产品异常”不等于整份日志不含 error 字符串。

`source-7e5b539.json` 为完整来源清单，取自最终 Visitor 运行的 source-before：原 SHA-256 `e01c582ac40f28869c30ce5f1dc622abf5f8b8d111195f028ccecdb042e1b574`，保存为无 BOM / LF 后 SHA-256 `8992ff058b26a48e661f27de4934a0e0b1d56044b2ef4bb13514b2d3961fc8a9`。JSON 值保留，PNG 原字节保留。

临时单眼相机近景用于排版诊断，不能据此宣称默认眼位、双眼头显可读性、真实触感、舒适度、性能、用户研究或新构建通过。

`audit-summary.json` 汇总七个通过轮的精确源码、全部断言通过/自然退出/指纹零差检查。`source-compatibility.json` 证明 7e5→aa9 的 1,291 文件仅 LI Editor 夹具不同，运行时和资产不变。LI 的 `report.full.json.gz` 无损保留全部 122,830 条断言，解压 SHA 见摘要及 provenance；`report.json` 明确是摘要，不能按摘要内所选断言条数推断总量。LI 的 18 张图是可选传统夹具，production 文件名仅指相机位姿，不代表当前站点状态；旧 masthead 与圆形前景的既有遮挡未作本轮布局通过声明。
