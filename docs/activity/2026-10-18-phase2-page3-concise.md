# 2026-10-18 技术说明第 3 页精简

用户明确授权：精简第 3 页，减少哈希和测试数字，采用小组项目报告的叙述，保留必要证据和未实测限制；备份旧稿并重新导出，正文不超过四页。

## 改动

将第 3 页改为“Prototype checks and Windows build”：按中央展品、左台与设置面板、语言三部分说明检查内容与修复结果，再说明 Windows 构建和有限桌面启动观察。去掉源码树哈希、成串断言数量、精确打包字节及退出码等明细，原报告及仓库证据继续保留。保留一个构建版本、引用链接和 40 秒观察时长；模拟检查、没有可用头显、主动停止进程、未验证正常关闭／VR、PQ604 尚需完整走查等边界保持原意。

未增加用户体验、学习效果、单手／坐姿能力或实机成功的结论。原先的 AI 辅助说明、需求与设计章节、评估重点及全部参考文献未改。

## 备份与输出

修改前的技术 PDF、DOCX、英文文稿、JSON 内容源及提交清单共五项已备份至：

`phase2小组和个人作业/原稿备份/20261018-2156-page3-concise/`

备份清单哈希全部复核一致。旧 PDF SHA-256 为 `a0f1da738bd8f77ab452612f24113f4fc0f3d137bf0d2653a75245ac826c7a2d`；其规范副本仍保留在 `artifacts/technical-notes/20261018-build-update/`，没有覆盖。

当前 PDF 仍使用提交文件名 `COMP5424-Group2-Phase2-PrototypeDesignTechnicalNote.pdf`，放在用户指定文件夹；新规范副本见[提交 PDF](../../artifacts/technical-notes/20261018-concise/phase2-prototype-design-and-technical-note.en.pdf)。新 PDF SHA-256：`941b4baaf771ceecb4412681b08472ed87b0f36de3ac8e2512f34fb0c71f40d0`。README、历史稿导航及用户文件夹中的提交清单已指向新版。

## 验证

- 第 3 页从 **376** 词减至 **333** 词。正文仍为 **4 页**，另 **1 页参考文献**；A4、四边 2.5 cm、Times New Roman，全部可见文字至少 12 pt。
- 用本机 Word 只读导出，并通过 bundled `render_docx.py`／Poppler 渲染；根代理及独立代理均检查全部五页，无裁切、溢出或重叠。
- 第 **1、2、4、5 页**与上一稿逐像素、逐页文本均相同，仅第 3 页变化；其正文最小右余白约 75.06 pt，满足页边界。
- 12 项参考来源、14 个唯一固定版本链接保留，Git 提交／文件目标均可解析。模拟检查、构建和桌面观察的原始证据版本未变。
- 活动 PDF、六篇个人文案及 ZIP 的哈希与上一轮核查一致；本轮未运行 Unity、Player 或设备测试。原工程已有 ProjectSettings 修改保持原哈希且未暂存。

内容源、导出脚本、规范 PDF、[导出核查](../../artifacts/technical-notes/20261018-concise/export-validation.json)及[独立排版核查](../../artifacts/technical-notes/20261018-concise/independent-review.json)均保存于本次独立目录。Codex 辅助完成本次文稿修订，不改变六位成员的贡献归属或课程规则；PR #71 沿用现有文档分支，未授权自动合并。
