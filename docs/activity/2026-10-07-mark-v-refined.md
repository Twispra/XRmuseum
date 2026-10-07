# 2026-10-07：Mark V 精细模型候选版

实际负责账号：Fttt1003；Codex 辅助资料收集、对照、参数化几何、原创贴图、渲染、资产检查及交接。唯一任务入口 [#25](https://github.com/Twispra/XRmuseum/issues/25)，[开工认领](https://github.com/Twispra/XRmuseum/issues/25#issuecomment-6031293826)。不创建新的建模 Issue，不代记其他成员贡献。

工作分支 `codex/mark-v-refined-2026-10-07`；从 `codex/helmet-refinement-assets` 的 `0c0aeee6337607bf9d8230e6575c7ed69fde75f1` 创建独立 worktree。开工时本工作目录干净；研究/建模和检查使用本次新文件，未纳入别的 checkout 的未提交工作。

## 完成内容

主参照定为1944 DESCO Mark V Mod.1标准空气式馆藏件。核对博物馆、制造商及1916年海军手册七项来源，记录同一件尺寸和当前名义接口规格，隔离当代亚克力/阀型与1944版本。NAVSEA两份资料403无法读取，明确未采用。图片/PDF在本机研究，不再分发原媒体；出处、访问记录和史实/估算区分保存在[研究目录](../../artifacts/helmet-refinement/eras/mark-v/references/research.zh-CN.md)。

新增完整可编辑总装、12个独立部件库、整体/前窗FBX及GLB、6张原创2K材质贴图、十张1440px渲染及独立浏览器审阅器。外壳中空且四窗贯通；前窗与铰链/锁紧件分开；胸板四压条和12紧固件、空气/电话鹅颈、排气、spitcock及颈锁分组。内部气道、电话件和按钮有史料上的存在依据，但1944具体形状/走线未经验证，独立列为示意。未编造铭牌编号、测绘精度、流量或深度。

## 验证

Blender4.5.14 LTS，Windows；运行新的 `scripts/check_mark_v.py -- --output <资产目录>`，实际源/导出哈希、拓扑、UV、法线、边界、窗孔、数量、方向和转轴检查见[报告](../../artifacts/helmet-refinement/eras/mark-v/qa/export-readback.json)。229网格、393,700三角形；源网格无拓扑问题，12部件库贴图可访问。GLB编码计数相等，Blender回读会移除两片已逐一定位的微小面，详见[检查说明](../../artifacts/helmet-refinement/eras/mark-v/qa/README.md)。十张渲染通过Codex视觉检查。

CUA实际操作新审阅器的开窗、视角、旋转/缩放、七组件选择、内部显示与复位；控制台无error/warn。页面自动HTTP加载和原始GLB在浏览器中验证；本轮没有测试file://文件选择。截图在模型 `qa/`，文件哈希在 `delivery-manifest.json`。

源 `.blend` → `exports/` FBX/GLB 已交付并回读；**没有Unity使用副本**，未改变已有 `.meta`/GUID、Prefab、场景、脚本、输入、Packages或ProjectSettings。二进制源/导出沿用LFS，新增GLB与本模型贴图LFS规则。主线代码差异应为空；文档和独立资产检查不启动Unity。

## 下一步与协作

组员从本工作分支或PR版本查看/继续，不以main中是否存在判断模型文件。研究补充和审阅在#25，编辑部件前说明路径/负责人；同一 `.blend` 一个写入者，部件库可独立认领，保留共用textures目录。总装协调后合入资产汇总分支；本次Draft PR不关闭整个三年代任务，也不自动接入main。

优先核对同一1944件的轮廓、顶窗、电话杯、排气与胸板曲率。未做的工作：取得内部/背面更完整实物证据、工程间隙/碰撞、烘焙/LOD及Unity/VR性能、运行时模型替换。后续整合者需验证现有前窗、剖面、供气/通话、复位、三时代与三语功能；这不是本轮已经完成的测试。

模型入口：[README](../../artifacts/helmet-refinement/eras/mark-v/README.md)；[交接](../models/mark-v-refined.zh-CN.md)；新资源和AI范围见[资源记录](../external-resources.md)。没有组员人工批准、用户研究或真实头显结果，不代写个人反思。
