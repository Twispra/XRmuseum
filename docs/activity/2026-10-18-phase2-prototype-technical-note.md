# 2026-10-18：Phase 2 原型设计与技术说明

## 任务、版本与保留

用户要求根据整个项目完成原型设计与技术说明：需求到功能映射、主要设计和技术决策、限制及评估重点，单个 PDF，正文不超过四页 A4，四边 2.5 cm，Times New Roman 至少 12 号，参考文献不计正文。

开工 `main`／fetch 后 `origin/main` 为 `6088c736e89d05101e09645c1796556012a6fd5d`，已包括 #67 和 #70；本轮分支为 `codex/phase2-technical-note-2026-10-18`。本轮只写文档、可复现导出源和 PDF，不改变 Unity 产品、课程规则或研究结果。

原工程已有 `MuseumVR/ProjectSettings/ProjectSettings.asset` 修改，SHA-256 前后保持 `81C0EB3103E303A2567619E388C45709E89FA1940353CDFE4A163BBE402C5587`。旧中文评审稿先在本机备份，随后只增加历史稿／最新提交稿的导航前缀；2026-10-16 原文逐字节保持在其后，旧 PDF 和报告不覆盖。六篇个人文案、已交付 Activity Record、官方课程原件和其他本机文件不纳入本次修改。

## 依据与内容

只读核对 Group Project Description、Accessible Museum Scenario、Phase 2 Rubric，以及当前代码／保存资产与活动证据。独立子任务分别核对课程需求、实际行为、构建与验证边界；没有运行 Unity 或重跑历史用例。

英文正文按四页组织：范围、用户流程与六项需求映射；交互和模块设计取舍；实际版本绑定的实现／验证／构建证据；限制与评估重点。参考文献独立一页，官方资料只列来源与页码，仓库证据使用完整 SHA 固定链接。

尤其保留以下事实边界：三年代均有不同几何，左台 #67 已接入且仅提供旋转／复位／展签；默认原位旋转取代旧自由抓取，NEW VISIT 仍属隐藏的旧入口；三语、坐姿和单手是团队设计选择／待验证假设，不是客户已定最低清单。新实体／左台按钮采用松手确认，未将入口 LanguageButton 或旧知识热点统称为同样行为；模型绑定与热点文案由各自模块负责。

实测冻结源码 `024d03942cc93d90453583e1c7e33e4eac96ecf2` 和 `6088c736` 的已提交 MuseumVR tree 同为 `eaa3237d73d386dc7b64bfc6c1994078ff2f958d`。引用 801／271／11／128,389／989 次既有 Editor 检查，不相加作用户案例，也不写成当前写稿时新跑。最后完整构建仍为 10-16 的 `806993a`，早于当前功能且 Player 未启动；PQ604 当前构建／启动和完整流程验收保留为 Phase 2 提交前事项。

## 输出与格式核查

- [英文文稿](../phase2-prototype-design-and-technical-note.en.md)，以及[单个 PDF](../../artifacts/technical-notes/20261018/phase2-prototype-design-and-technical-note.en.pdf)。本机作业文件夹另有字节相同的提交副本和可编辑 DOCX。
- PDF 共五页：四页正文、一页参考文献；没有封面或空白页。A4，四边 1417 twips（2.5 cm 的 Word 舍入值）；可见字体只有 Times New Roman 常规／粗体，字号 12／14／16 pt。页码也为 12 pt。
- 正文内容约 1,436 个英文空白词；引用 12 项来源，14 个唯一固定版本仓库链接，PDF 含 38 个链接区域。提交／文件／目录目标均可在本地 fetch 后的 Git 对象库解析，没有编造 Canvas 链接。
- Microsoft Word 只读导出；使用 bundled `render_docx.py` 的 rasterize 与 Poppler 生成页面。渲染首版有额外空白第二页，改用标题分页属性消除，未缩小正文或放宽页边距。最终五页均已复核：第 1／4／5 页与已检查图片逐字节相同，第 2／3 页重看最终图片；无缺字、裁切、重叠或跨页表格错误。
- 最终 PDF SHA-256：`ec22b0effe718f049863894bc5f56470e8029aaf28337c0d5e5646a34f769b80`，147,561 bytes。版式／源版本／SHA 核对见[导出记录](../../artifacts/technical-notes/20261018/export-validation.json)。

可维护源为同目录 `report-content.json` 和 `build_report.py`，通过 Codex bundled Python 运行；DOCX/PDF 导出使用本机 Word。原始逐页图和本机过程审计留本地，不将课程原件、旧稿备份或大量临时输出纳入 Git。资源及辅助范围见[工具记录](../external-resources.md)。

## 尚未完成与交接

报告任务已完成，等待正常 PR 审阅；没有执行 Canvas 上传或 PR 合并。报告明确保留最新构建／PQ604、真实头显完整单手／坐姿流程、阅读／舒适度／性能、参与者学习评价和工作人员交接的验证缺口，不把文档交付写成整个 Phase 2 已验收。
