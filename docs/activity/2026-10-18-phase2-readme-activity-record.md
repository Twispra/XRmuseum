# 2026-10-18：Phase 2 README 与小组活动记录

## 授权与基准

用户明确要求先做两项：确认仓库公开并按六个个人文案文件名补齐根 README 成员身份；依据真实活动、贡献与六篇文案填写课程 Activity Record 模板，导出一个提交用 PDF。没有授权或执行正式 Canvas 提交、最新 Player 构建或 PR 合并。

开工分支为 `codex/phase2-essay-fact-check-2026-10-18`，HEAD 与 fetch 后的 `origin/main` 均为 `0109e3f75019f585454da638b56101f235d40770`。本轮工作分支为 `codex/phase2-readme-activity-record-2026-10-18`。已有 `MuseumVR/ProjectSettings/ProjectSettings.asset` 修改和所有原未跟踪资料保持；其 SHA-256 仍为 `81C0EB3103E303A2567619E388C45709E89FA1940353CDFE4A163BBE402C5587`。

## 仓库与 README

GitHub `get_repo` 实时回读 `Twispra/XRmuseum` 的 `visibility: public`，默认分支为 main；本轮未更改仓库权限。README 登记六名成员的完整学号（保留末尾 g）与账号，其中何梓绎同时登记文件名给出的两个账号。身份来自用户指定文件名，不从 Git 作者字符串推测学生身份。

同步纠正 README 中早期模型占位和默认自由拿放的过期描述，明确 #69 三套几何、#59 原位旋转、#65 面板及保留旧流程的边界；保留 `806993a` 构建早于这些修订、当前新构建与目标设备尚未验收的限制。

## 活动记录

课程模板批注明确每组填写一份、约一页、包含全部成员，使用简短责任、实际结果、已实行决定和后续问题；因此采用覆盖 2026-09-22 至 10-18 的单份记录，没有编造逐周会议或课程周号。参考六篇已核校文案、Git 提交、PR 状态与独立活动证据；确认原实现与 Twispra 后续修复、#43 源资产 10-11 与 #44 接入 10-12、#52 与 #59 模式更替、#66 独立研究与 #67 未合并等边界。

输出：[Phase 2 小组活动记录 PDF](../../phase2小组和个人作业/COMP5424-Group2-Phase2-ActivityRecord.pdf)。可编辑副本留在同一本机作业文件夹，原始模板不变。本轮不将原模板、其批注、六篇个人文案、备份或大量本机 QA 文件纳入 Git。

未来协调人仅标为 Proposed，不伪造已接受的指派；最终版本构建、ZIP 和 PQ604 运行保留为 Phase 2 提交前事项，坐姿／单手／双眼文字／学习评估保留为 Phase 3 工作。没有将历史构建或自动检查写成最新头显、参与者或实验室通过。

## 验证与来源

- PDF 为一个 A4 页面，330 个可见英文空白词，全部可见文字为 12 pt；沿用模板 Times New Roman，中文姓名回退 Microsoft YaHei。PDF 含 40 个可点击 GitHub 证据链接，六人姓名和文件名身份逐一检查通过。
- 原模板 SHA-256：`abf1c8cc942c5279b3630429a7389a8b014632b7aa2afaca266750ea944e9f56`；前后完全一致。仅编辑 DOCX 副本的正文和新证据关系，所有其他原包部件字节不变，页面尺寸／页边距不变。仅将空末段压为 1 pt 空行以消除空白第二页，可见文字字号未缩小。
- 最终 PDF SHA-256：`28a90f1ecd0f8974af8f3667159d6861a168a4d6e5a3726715daf08a04091d92`，151,559 bytes。可编辑 DOCX SHA-256：`20db094fa5af80b50658b5b12f380628f931bdf5b676f18cab64f220c8c57cf3`。
- Windows runtime 没有 bundled LibreOffice，原文档渲染器首次报告找不到 soffice。使用已安装 Microsoft Word 的只读 `ExportAsFixedFormat` 导出，再通过 bundled `render_docx.py` 的 rasterize 与 Poppler 生成 150 DPI 图片；最终页已逐页目视检查，无裁切、缺字、重叠或多余空页。没有调用桌面 LibreOffice。
- 六份个人文案与此前修订后 SHA-256 完全一致；README 相对链接均有目标；保留原设置哈希；diff-check 通过。未启动 Unity、Blender、Player 或重做历史测试。

详细模板契约、包清单、字体／链接／哈希验证和最终页面图留本机 `artifacts/phase2-submission/20261018-readme-activity/`，不混入正式提交 PDF。资源及 Codex 辅助范围见[工具记录](../external-resources.md)。

## 交接

已完成本轮两项本机交付及 README 的 PR 准备；按正常 PR 审阅后再合并，当前不把分支文件写成默认分支已更新。正式 Canvas 上传、最新构建与 PQ604 运行不属于本轮完成项。
