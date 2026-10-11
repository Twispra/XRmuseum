# 2026-10-11：PR #38 技术说明审查修订

用户授权先合入 #40／#43，再修复 #38／#41／#42 并重新审查。本轮仅负责 [PR #38](https://github.com/Twispra/XRmuseum/pull/38) 的文档修订；不将文档合并或修订写成课程交付已完成。

## 基准与范围

- 原 PR head：`203c32bef3f7c818c4f04aafc96bb421db0d2f7d`，原构建自述来源为 `270c7b72da63f08baac85ee7bb94f7fcdb4a226d`。
- 同步的实际 `origin/main`：`a8da4e884964e8a95b8d1a6ef61fc1faf8802675`（包含已合并 #40／#43）。从原 head 建独立分支 `codex/pr38-review-fix-2026-10-11`，使用普通 merge 保留历史；同步提交 `5e67293b86d2fe3ae9af0059130d534ae864cd42`。
- 唯一冲突为 `docs/external-resources.md` 顶部追加内容：保留 main 的两段 SuperLite 登记、#38 登记和全部旧条目，再只修正 #38 登记的草稿／证据范围表述。
- 新增本活动记录，修改技术说明、原构建活动记录及上述 #38 资源条目。没有修改 `MuseumVR/`、项目状态或每周汇总；原 checkout 的既有 Unity 设置与未跟踪资料未进入本轮。

## 修订内容及依据

1. 原说明的“年代仅切换材质”不符合当前代码。[`RefinedHelmetBinding`](../../MuseumVR/Assets/Museum/Features/RefinedModels/Runtime/RefinedHelmetBinding.cs) 按索引启用精细／原始 renderer，重绑前窗触发区、铰链、剖面及接口；[整合说明](../refined-model-integration.zh-CN.md)已有相同边界。改为 Mark V（索引 1）显示精细几何，年代 0／2 显示原占位几何与各自材质。
2. [`HelmetHotspot`](../../MuseumVR/Assets/Museum/Features/HelmetHotspots/Runtime/HelmetHotspot.cs) 的可用／悬停／已访问状态通过颜色区分。[`HelmetHotspotTour`](../../MuseumVR/Assets/Museum/Features/HelmetHotspots/Runtime/HelmetHotspotTour.cs) 提供全局访问计数，不能替代逐热点状态表达。删去全部关键信息“三重冗余”的过满结论，明确当前非颜色替代的缺口。
3. 现有记录尚无可引用的坐姿触达设计基准或目标设备验收。保留功能入口描述，将坐姿／单手使用和完整用户流程改为待核验项目。
4. 保留原作者构建命令、时间、体积、退出码、OpenXR 运行自述与 exe 哈希，注明本轮未重做构建；原始构建／播放器日志和完整产物没有可访问的独立复核入口。`git lfs fsck` 通过不能证明当时工作区资产已展开，单个 exe 哈希也不足以绑定完整构建数据。
5. 技术说明为 Markdown 草稿，未确认 A4 排版后不超过四页。正式构建交付、教学团队访问和课程提交状态仍须按[课程依据](../course-requirements.md)核验；原 AI 政策尚未由本轮确认，未声称满足其全部条款。

## 本轮验证

Windows／Git 文档检查，未启动 Unity、播放器、Blender 或重新运行历史自动测试。

- `git diff --check` 通过；对本轮四个文档检查的 64 个相对 Markdown 链接均有目标，没有残留冲突标记。
- 修复后的 `MuseumVR` tree 为 `8f9ef25d7a3ff6d9e9bf014117927a6a0b0a2eca`，与原 PR head、原构建来源和上述 `origin/main` 相同。普通 merge 带入的已有主线资产不属于本轮重新制作，也不据此声称重新构建或实机通过。
- 对 `origin/main` 的最终差异仅为 #38 的三个文档加本活动记录；共享项目状态和每周汇总与 main 相同。资源登记中 main 的两段 SuperLite 内容及全部旧条目原样保留。

## 交接与限制

文档修订由 Codex 在用户授权下辅助完成，无新增第三方资源或 Unity package。后续由整合人推送原 PR、更新描述并复审；本轮没有推送、评论或合并 PR。原构建与运行自述尚未独立核验，A4 页数未确认；真实头显流程、坐姿可及性、声音舒适性、设备性能、参与者评价及正式课程交付均未在本轮验证。
