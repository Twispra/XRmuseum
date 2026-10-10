# 2026-10-09：SuperLite 17B 统一资料与面窗/侧阀 v2 分件

负责人 Ereshkigal1224；Codex 辅助资料检索、源文件脚本、预览和本机检查。关联 [Issue #25](https://github.com/Twispra/XRmuseum/issues/25) 与保持 Draft 的 [PR #37](https://github.com/Twispra/XRmuseum/pull/37)，目标分支 `codex/helmet-refinement-assets`。本次继续既有 `codex/superlite17b-details-2026-10-08`，不认领整盔主体、最终总装或 Unity 接入。

## 本次完成

- 将 P/N **500-010（communications posts）** 作为拟统一研究配置；型号选择来自 Kirby Morgan [17B 产品页](https://www.kirbymorgan.com/products/helmets/superlite-17b)。照片未逐张标 posts/MWPC，制造年份与实物序号未知。完整来源、手册编号/页码、角度、版权状态、官方数据/照片观察/建模估算/缺口与暂定装配接口集中于 [references 入口](../../artifacts/helmet-refinement/eras/superlite-17b/references/README.md)及其索引。厂家原图/PDF 页面只链接，不复制入仓库。
- 补入厂家 [17B 零件图 #241108001](https://www.kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf)、[面窗手册 #260901001](https://www.kirbymorgan.com/sites/default/files/2026-09/face-port-port-retainer-nose-block-260901001.pdf)、[侧阀手册 #260701004](https://www.kirbymorgan.com/sites/default/files/2026-07/brass-chromed-brass-side-block-260701004.pdf)与[侧阀分解图 #230118015](https://www.kirbymorgan.com/sites/default/files/2023-01/chromed-brass-side-block-blowapart-230118015.pdf)。据手册将独立面窗压圈螺钉由 12 个纯视觉估计头修为文档支持的 11 枚；另 4 枚排气翼螺钉留给后续相应部件，不编造孔距。侧阀模型中区分稳流钮 520-524、应急钮 520-525 与独立应急阀 505-070，降低规则盒体感。型号/零件号是官方事实，外形仍是展示估算。
- 在 `artifacts/helmet-refinement/eras/superlite-17b/` 保留 v1 不变，新增两份独立可编辑 v2 `.blend`、两份 GLB 和四张原创预览，更新对应脚本、参数与[照片—模型对照](../../artifacts/helmet-refinement/eras/superlite-17b/references/photo-model-comparison-v2.zh-CN.md)。两张 v1 原创材质贴图复用，未下载第三方模型/贴图。

## 测试与证据

开始生成前个人分支 HEAD `257fcf08b5594eac9bb21c20b5f33d3e4b9352c9`，上述脚本/资料为本次额外未提交改动；主 checkout 原有未跟踪嵌套 `XRmuseum/` 未触碰。`git fetch origin --prune` 后资产 base 仍为 `37428dff4307a31a042b8b3ed38e57d8856b502c`。测试使用 Windows 微软商店版 Blender 4.5.14 LTS；在新空目录生成资产，并用同版重新打开两份 `.blend`、导入两份 GLB。[独立 QA 记录](../../artifacts/helmet-refinement/eras/superlite-17b/qa/2026-10-09-store-blender-v2.md)与[回读 JSON](../../artifacts/helmet-refinement/eras/superlite-17b/qa/faceport-exterior-v2-readback.json)记载：面窗 16/16、侧阀 32/32 网格一致；UV、命名、镜片透明材质、预览辅助物排除及估算压圈厚度 0.012 m 通过。两份 `.blend` 由 Git LFS 管理，GLB 与 PNG 按本目录既有方式提交。

先前官方便携/安装版的 DLL 受本机 Windows 应用控制策略拦截；商店版通过完整生成与回读。没有降低或关闭安全保护。原[故障记录](../../artifacts/helmet-refinement/eras/superlite-17b/qa/faceport-exterior-v2.md)作为历史保留。

## 未完成与交接

照片并非严格同视角标定；面窗下缘、侧阀标牌/壳体和连接方向仍需另一成员按[对照页](../../artifacts/helmet-refinement/eras/superlite-17b/references/photo-model-comparison-v2.zh-CN.md)审阅。完整正面、左右侧、背面（含 posts）和俯视的可校准资料不足；所有局部尺寸和螺钉孔位仍为估算。与未来主体作者共定整体原点、比例、X 轴方向、面窗开口/侧阀接触面后才能装配。没有 Unity 导入、运行时绑定、真实头显、可执行构建、用户研究或潜水装备功能/安全验证。PR 保持 Draft，另一成员审阅前不汇入资产 base，更不接入 main。
