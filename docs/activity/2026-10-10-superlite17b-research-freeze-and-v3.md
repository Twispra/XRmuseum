# SuperLite 17B：资料收束与面窗/侧阀 v3（2026-10-10）

负责人：Ereshkigal1224；Codex 辅助检索、建模脚本和检查。对应 [Issue #25](https://github.com/Twispra/XRmuseum/issues/25)、现有 [Draft PR #37](https://github.com/Twispra/XRmuseum/pull/37)。工作分支 `codex/superlite17b-details-2026-10-08`，目标分支 `codex/helmet-refinement-assets`；不接入 main。

## 已完成

- 核查厂家 17B 产品页、整盔照片、侧阀特写、17B 和侧阀分解图、面窗维修资料及 FAQ 的相关零件信息。每项可用证据和使用边界、仍缺视图/尺寸/年份/配置对应写入 `artifacts/helmet-refinement/eras/superlite-17b/references/research-freeze-2026-10-10.zh-CN.md`。未复制厂家图片。
- 按官方照片的**可见外观**修正独立面窗下缘、侧阀圆润横向主体、错位控制钮和无品牌标牌。几何数值继续列为估计；新建 v3 `.blend`、GLB、四张原创预览、参数和可复现脚本。v1/v2 未覆盖。
- Blender 4.5.14 重开 `.blend` 并导入 GLB 的检查通过；见 `artifacts/helmet-refinement/eras/superlite-17b/qa/faceport-exterior-v3.md` 与 JSON。

## 仍未完成与交接

- 2026-10-10 本轮资料收束后，暂停进一步搜集资料。当前公开资料未找到可确认同一 500-010 实物的正交正/侧/背/顶视图、面窗孔位/尺寸、侧阀安装尺寸与两个旋钮的可靠轴线测量；照片的通信配置和年份也未确认。未知项不填假精度。
- 独立面窗和侧阀仍需团队成员对照来源审阅。主体与总装没有认领为本任务成果；正式单位比例、整体原点、面窗接触曲面、侧阀连接点需要与主体制作者协商。Unity 接入与运行时绑定另行协调。
- 未运行 Unity/实机；回读成功不能替代造型准确性或装配验收。PR 维持 Draft，待同伴审阅。
