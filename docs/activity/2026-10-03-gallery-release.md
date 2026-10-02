# 2026-10-03 展厅发布前回归与交接

用户要求先验证已经完成的展厅，再上传 GitHub，之后继续自由探索功能。本次交付对应 [Issue #4](https://github.com/Twispra/XRmuseum/issues/4)，分支 `codex/museum-atmosphere-2026-10-02`。10 月 3 日重新 fetch 后，`origin/main` 仍为 `74556aa`；展厅产品源码为 `342cdda2b77aab781d991cbd497c994330e5e76a`。本轮没有修改产品代码或输入绑定。

## 本次验证

Unity 2022.3.62f3c1，Windows Editor / Direct3D 11。关闭交互编辑器后串行运行以下入口，省略 `-quit`，由验证器完成后退出；不执行任何 Builder。

| 入口 | 本次结果 | 证据 |
| --- | --- | --- |
| `Museum.MaritimeGallery.Editor.MaritimeGalleryValidation.ValidateBatch` | 49/49；保存场景字节未变 | [报告](../../artifacts/maritime-gallery/qa/runs/20261002T162435260Z-validation-7c6d63760e8e47d39ffcf42d386ec952/report.json) |
| `Museum.Inspection.Editor.HelmetInspectionValidation.RunMainBatch` | 207/207，0 个记录错误 | [报告](../../artifacts/helmet-interaction/qa/runs/20261002T162513715Z-inspection-main-5cc99770046d4d14b3ab4601b2e645e4/report.json) |
| `Museum.Editor.HelmetPrototypeValidation.RunBatch` | 90/90，10 次拿放循环 | [报告](../../artifacts/helmet-interaction/qa/runs/20261002T162722639Z-0cf10c0637604fd382a278b8ee22c434/playmode-validation.json) |

三个进程实际退出码均为 0，见[进程结果](../../artifacts/maritime-gallery/qa/runs/20261002T162435260Z-validation-7c6d63760e8e47d39ffcf42d386ec952/process-results.json)。[静态回归](../../artifacts/maritime-gallery/qa/runs/20261002T162848402141Z-publish-audit/report.json)核对原依赖哈希、场景变化和输入映射。报告中的 dirty 信息主要是开工前已存在的未跟踪 QA/草稿；被测产品文件无额外修改。运行目录使用 UTC，因此文件夹日期为 10 月 2 日，本地时间为 10 月 3 日。

本次检查未发现新增环境造成的前窗、剖面、悬停、个人面板、旧拿放、Home 或实体 RESET 回归。检查了本轮起始与剖面渲染；这是 Unity 单眼渲染，不是头显实测。不能把通过项数理解为整个项目没有任何 bug。

## 操作与交接

1. 切到上述交付分支，使用保存好的 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`，无需重建资产。
2. Play 后点击 Game、切英文输入，按 T/Y 操作左/右手柄或 Tab 切换设备；确认选中 Controller。
3. 射线对准 WINDOW / SECTION / FLOAT / RESET 等实体按钮，按 **G 并松开**。G 对应 Grip → Select；鼠标左键对应 Trigger → Activate/UI Press，不是这组实体按钮的选择键。之前聊天中的左键说明有误，本次更正说明，没有悄悄更换团队输入。
4. 模型抓取仍按住 G、松开放下；Home 或 RESET 复位。FLOAT 及剖切详细操作见[原功能说明](../helmet-advanced-inspection.zh-CN.md)。
5. 环境调整入口、来源、资源边界见[环境说明](../maritime-gallery.zh-CN.md)。同一时段主场景只由当前整合任务写入。

此轮仅交付环境与复测证据。后续自由探索另开分支，明确依赖本次 PR；不把未合并的环境当作 main。原未跟踪草稿、`.vscode`、缓存和中间报告留在本机，不纳入提交。另一位组员依据实际 PR 审查后决定合并。

## 尚需完成

自动交互检查禁用了官方模拟器的输入写入，再注入模拟 XR 控制器；不能据此声称完整官方键鼠操作或真实头显测试通过。真实设备的跟踪、移动/传送、坐姿触达、文字清晰度、舒适性、帧时间，以及当前 Windows 可执行构建仍未验证。

两侧 AIR / VOICE 仍为静态解释装置。供气/通话教学、按需提示和学习回顾属于后续功能。AI 辅助范围沿用[来源记录](../external-resources.md)，本次新增只读代码检查、回归执行及交接，不代替真实用户研究或组员审批。
