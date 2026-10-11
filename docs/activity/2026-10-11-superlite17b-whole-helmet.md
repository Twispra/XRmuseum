# 2026-10-11：用户资料驱动的 SuperLite 17B 整盔模型

用户授权依据 `D:\XR_documents\helmet` 建模，供以后 XR 博物馆使用，Codex 执行本次独立资产工作。不推断成员姓名/贡献归属，不把 AI 检查当作团队人工批准。

## 开工与范围

- 起始 main，HEAD `ac3d31cb2db37640cf784f3447ca90d5d517b55a`；初始状态只有 `?? XRmuseum/`。未知嵌套目录保留，未纳入成果或更改。
- 普通 fetch 遇到 .git/FETCH_HEAD 权限限制，经工具批准提权后 fetch 成功；origin/main 仍为同一 HEAD。没有写远程或合并 PR。
- 任务分支 `codex/superlite17b-whole-helmet-2026-10-11`，基准含 PR #39 独立分件归档。面窗 v3 / 侧阀 v4 及旧报告保留。
- 新资产仅在 `artifacts/helmet-modeling/superlite17b-2026-10-11/`，另增加本活动记录和资源记录。没有修改共享 Unity 资产。
- 已读状态、指南、课程依据、现有同型号资料和本地 BUILD_SPEC；逐张查看 12 图，渲染/读取 PDF 两页。本地 PDF 实际为 2015 #150424001，不是前一轮 2025 图。
- Issue #25 网页读取失败，没有发布认领留言或创建 PR。本次成果目前只在本地，不能当作已向团队交接。周索引/总状态留给约定汇总人。

## 成果与验证

[交付报告](../../artifacts/helmet-modeling/superlite17b-2026-10-11/REPORT.md)、[依据/假设](../../artifacts/helmet-modeling/superlite17b-2026-10-11/REFERENCE_NOTES.md)、[15 份输入的哈希清单](../../artifacts/helmet-modeling/superlite17b-2026-10-11/input-manifest.json)。原图片/PDF 只在本地观察，不提交、不做贴图。

新原创外观模型有 142 个网格、52,980 三角面，主要部件和可见内部齐全；全部米数为估算，包围盒不是官方实测参数。被测代码/资产是上述 HEAD 之后本轮未提交改动。Windows，本机 Blender 4.5.14 LTS（62c1db4208e8），后台在新目录构建并渲染六阶段四视图；check_model.py 新进程回读实际保存源，检查单位/UV/有限坐标/零面积面/空修改器/壳体封闭网格/透明镜片/11 与 4 两组固定件/预算。在另一个空目录重建，同一脚本逐对象世界坐标几何哈希比较结果见[最终回读](../../artifacts/helmet-modeling/superlite17b-2026-10-11/qa/readback.json)。

视觉检查修正提手后脚/头带悬空、头垫穿出和薄标牌倒角导致的零面积面，没有放宽检查阈值。临时输出、失败候选和故障日志保留在被忽略的 tmp，未覆盖历史证据。没有启动 Unity、FBX/GLB 验证、真实头显、性能、参与者研究或构建验证。

下一步：人工对照同一实物审阅轮廓；取得实测尺寸后校准。用户确认展示用途和比例后，按项目协调流程再认领运行时导出与 Unity 整合，不自动认领共享场景、实机或正式课程提交。
