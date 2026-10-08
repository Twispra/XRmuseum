# 2026-10-08：SuperLite 17B 细节与外观首版

实际负责账号 Ereshkigal1224；Codex 辅助查阅官方资料、制作独立 Blender 分件与原创贴图、渲染及自动检查。[认领与维护者确认](https://github.com/Twispra/XRmuseum/issues/25#issuecomment-6062156623)在 Issue #25；本轮具体版本、文件名、时间及待协调接口另见该 Issue 的本任务补充评论。没有代记主体或总装贡献。

工作分支 `codex/superlite17b-details-2026-10-08`，从资产汇总分支 `37428dff4307a31a042b8b3ed38e57d8856b502c` 建立，独立托管 worktree；开工时干净。原本主工作目录的 `?? XRmuseum/` 嵌套目录是先前已有的未知改动，未进入本分支或提交，也未重置/清理。

拟定对象为当前厂家资料中的 Kirby Morgan SuperLite 17B P/N 500-010（posts），制造年份和具体实物序号未知。官方产品/规格页及 2025 零件分解图用于确定外观、材质类别和部件编号，完整 URL 与事实/观察/估算边界保存在 [研究说明](../../artifacts/helmet-refinement/eras/superlite-17b/references/faceport-exterior-v1.zh-CN.md)。未把用户照片研究样件认作 17B，未下载或再分发官方照片、PDF 图像，也未复制它们做贴图。

## 首版产物

两份独立可编辑分件：面窗/压圈/密封圈/外露鼻夹钮，以及侧阀体外壳/两枚可见控制钮。分别保存在 `sources/parts/`，各有 GLB；四张正面和斜前方渲染供审阅。两张原创 1024px 基色/粗糙度贴图由确定性脚本生成，已打包进 Blender 文件；材质色泽是视觉解释，不是厂家测量。重建脚本只可用于全新空目录，拒绝覆盖已有人工作业。

面窗和侧阀体各用自己的局部原点，暂按米、Z 向上、-Y 朝前；绝对尺寸、螺钉间距、曲率、装配接口均属估算或待协调。预览的灰色背卡只用于显现透明镜片，已从 GLB 排除。壳体/颈圈、内部气路、通信接口、调节器和排气翼不在本首版几何中。由未来主体负责人和本任务作者共同确认版型、整体原点、尺度、朝向与安装位置后，再产生装配适配版；当前研究件不能视为最终可拼装接口。

## 验证与限制

被测基准 HEAD 为上述 `37428df`，额外文件只有本轮尚未提交的新资产。Windows / Blender 4.5.14 LTS；`scripts/check_faceport_exterior_v1.py` 回读两份 Blender 和 GLB，检查网格名称/数量、UV、贴图打包、GLB 镜片透明、预览物体排除，结果通过；[机器报告](../../artifacts/helmet-refinement/eras/superlite-17b/qa/faceport-exterior-v1-readback.json)与[限制说明](../../artifacts/helmet-refinement/eras/superlite-17b/qa/faceport-exterior-v1.md)保留。Codex 视觉检查四张渲染；还需组员人工对照同版 17B 审阅，不能写成已通过人工质量审查。

未测试 Unity 导入、场景接入、碰撞/LOD、真实 VR、可执行构建、用户研究、装备用途或真实尺寸。主场景、现用资产、输入、Packages、ProjectSettings 均未改。下一步：在 Issue #25 附上 commit 与预览、发个人 Draft PR（base 为 `codex/helmet-refinement-assets`）；由另一成员审阅再讨论汇入资产分支。正式总装仍待负责人认领。
