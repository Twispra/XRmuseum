# 2026-10-18 Phase 2 最终提交材料核查

> 同日后续：技术说明已按用户要求仅精简第 3 页，其他页面及活动表／个人文案／ZIP 不变；以下保留此前综合核查及 PDF 指纹。当前技术 PDF、备份与格式复核见[第 3 页精简记录](2026-10-18-phase2-page3-concise.md)，提交文件夹中的清单已同步新版指纹。

已依据 main `6088c736` 的最新 Windows 构建更新技术说明和活动记录，修正两篇个人文案中的过期状态／范围表述。现有提交材料的格式、源码版本、历史验证版本、贡献边界和时间线一致。**PQ604A/B/C 与真实头显完整流程尚未现场验证**，材料明确保留此限制；未执行 Canvas 上传。

## 小组提交

仓库文本栏填写：<https://github.com/Twispra/XRmuseum>。2026-10-18 已重新通过 GitHub API／连接器确认 `visibility=public`；根 README 的六人姓名、学号及账号与个人文件名一致，何梓绎的两个账号均保留。

在 `E:\XRmuseum\phase2小组和个人作业\` 中选择以下三个文件上传到对应的小组提交栏：

| 材料 | 文件 | 核查结果 |
| --- | --- | --- |
| 可执行程序 | `COMP5424-Group2-Phase2-Windows-x64-6088c73.zip` | Windows x64 Development/Mono；274 文件；74,726,292 bytes；CRC 与内部清单哈希通过，与本次构建包完全一致 |
| 原型设计与技术说明 | `COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.pdf` | 单个 PDF；4 页正文＋1 页参考文献；A4；四边 2.5 cm；Times New Roman，所有可见字号至少 12 pt；全部页面已目视复核 |
| 小组活动记录 | `COMP5424-Group2-Phase2-ActivityRecord.pdf` | 单个、单页 A4 PDF；沿用课程模板的三张表，六名成员全部列出；版本及最终状态已更新 |

DOCX 是可编辑源文件，`…ActivityRecord-template.docx` 是原模板，`原稿备份/` 是历史备份，本文是核查说明；均不属于上述三个小组上传文件。不要把整个资料文件夹重新压成一个总包代替课程分别要求的材料。

技术说明对应的新 PDF SHA-256 为 `a0f1da738bd8f77ab452612f24113f4fc0f3d137bf0d2653a75245ac826c7a2d`；活动 PDF 为 `552f370c1c03f99ba93175bf3d6a8b37eb1c521a6ffadb86af85c88969f45ec6`；ZIP 为 `3914fb87cb3e19742fc8f46b46747fcb7154478be56bb18b096b1cdbdfd4f102`。

## 个人提交

每人使用对应 TXT 的正文在自己的 Canvas **文本输入栏**提交。按保守口径计入标题、显示文字及 URL；六篇均未超过 750 词。原有反思内容保留，没有把本次 Codex 构建／文档整理改写为成员新增的独立功能贡献。

| 成员 | 学号 | 账号 | 保守词数 | 本轮处理 |
| --- | --- | --- | ---: | --- |
| 蔡子申 | 26134128g | r2kh8sck4y-bot | 679 | #67 改为已合并；区分初始左台工作与 Twispra 后续修复；#64 完整布局仍待评审；补固定版本证据 |
| 何梓绎 | 26069396g | Aragorn-cyber／heziyi-aragon | 519 | 原文保留 |
| 叶福涛 | 26053268g | Fttt1003 | 663 | 原文保留 |
| 张非凡 | 26108425g | nikoyeah830-wq | 573 | 将初版保护范围限定为头盔及剖面拖动，保留后续修复归属 |
| 赵基蘅 | 26071012g | Twispra | 656 | 原文保留 |
| 赵祥睿 | 26044846g | Ereshkigal1224 | 661 | 原文保留 |

交叉核对未发现两人排他认领同一独立成果：年代／初期本地化、后续三语扩展、热点、各时期模型、回位／Help、操作面板及主线修复／整合分别追溯。个人文案中的旧检查数字和旧构建均有历史版本边界，因此没有将其机械替换为最新结果。六文和技术说明中的 **43 个不同固定版本提交／文件目标**全部通过本地 Git 对象解析；PR／Issue 链接保留对应原始记录。本轮没有声称逐个重新 HTTP 检查了所有历史外部网页。

## 版本、时间线与验证结论

- 最终原型／ZIP 源码为 **6088c736**，已包含 #67 左台接入。已提交 `MuseumVR` 树与冻结 Editor 验证源码 **024d039** 相同（`eaa3237d73d386dc7b64bfc6c1994078ff2f958d`）。文档分支的新提交仅整理材料和证据，不改变该产品树。
- 活动记录覆盖 **22 September–18 October 2026**，已移除 `0109e3f` 旧快照、`#67 unmerged` 和“尚未构建／打包最终版本”等过期结论；#64 完整双台方案仍未决。六人历史贡献与后续修复分别记录。
- 最新构建于 **18 October** 完成：Unity 2022.3.62f3c1、Windows x64 Development/Mono、0 errors／2 条既有 OpenXR 可选警告，Editor **自然退出 0**，1,299 个工程文件构建前后零差。完整打包为 272 个 Unity 产物＋启动说明＋manifest，共 274 文件。
- ZIP 解压版完成 **40 秒桌面启动观察**，确认引擎、D3D11、输入及场景脚本初始化，未发现意外托管产品异常。没有可用头显，日志有 `XR_ERROR_FORM_FACTOR_UNAVAILABLE` 等 OpenXR 初始化诊断；观察结束后仅主动终止自有 Player（**exit -1／forced=true**）。没有把它写成 Player 自然退出、立体画面或 VR 交互验收通过。
- 技术说明的 801／271／11／128,389／989 继续对应既有 **024d039 Editor 套件**；没有重跑或汇总成参与者数量。个人文案的 959 等旧数字继续对应各自早期版本。16 October 的 `806993a` 只保留为历史构建，不再作为当前提交包版本。
- **仍待完成**：PQ604A/B/C 的实际软硬件与运行时核验，真实头显／控制器的完整用户流程、自然关闭、立体渲染、性能、舒适性、可访问性及用户研究。文件格式和包完整性通过不能替代这些现场结果。

## 备份、来源和保存

修改前已备份当时全部 **12 个直接文件**，包含旧 PDF、DOCX、六篇 TXT、模板及 ZIP，位置为 `phase2小组和个人作业/原稿备份/20261018-2145-final-review/`，`backup-manifest.json` 保存原哈希。备份均与清单一致；其余四篇个人文案、模板和 ZIP 未改。旧版技术说明及其渲染／审计仍保留于 `artifacts/technical-notes/20261018/`，新规范副本位于 `artifacts/technical-notes/20261018-build-update/`。

技术说明新建证据入口使用提交 [d05def503ad286815575ed7038c1f50ddc21dd9c](https://github.com/Twispra/XRmuseum/commit/d05def503ad286815575ed7038c1f50ddc21dd9c) 的[构建活动](https://github.com/Twispra/XRmuseum/blob/d05def503ad286815575ed7038c1f50ddc21dd9c/docs/activity/2026-10-18-phase2-windows-submission.md)与包验证；素材和 AI 辅助范围见 [资源记录](../external-resources.md)。本轮未运行 Unity 或修改产品；保留原工程已有 `ProjectSettings.asset` 修改（SHA-256 仍为 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`）。

[最终综合核查 JSON](../../artifacts/phase2-submission/20261018-final-review/final-validation.json)记录当前文件指纹、个人词数、身份匹配及版本核对；[独立格式／ZIP 审计](../../artifacts/phase2-submission/20261018-final-review/final-format-audit.json)记录各页字体与边界及 CRC／内部 manifest 复核。现有 [PR #71](https://github.com/Twispra/XRmuseum/pull/71)承载文档更新，尚未合并；正式 Canvas 操作由各成员／小组提交人完成。
