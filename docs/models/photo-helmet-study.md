# 照片实物头盔：独立外观研究样件

模型协作统一入口 [#25](https://github.com/Twispra/XRmuseum/issues/25)；原首件任务 [#26](https://github.com/Twispra/XRmuseum/issues/26) 于 2026-10-07 收拢关闭，仅供历史追溯。原作者/实际负责人为 Fttt1003；Codex 辅助来源核对、原创程序建模、导出、预览和检查。基于 `main 0c0aeee`，分支 `codex/helmet-refinement-photo-study-2026-10-06`。

本件只是一版可共同审阅的外观研究资产。项目现有 Mark V 展品选择继续保留；本件未被鉴定为 Mark V，也没有接入 MuseumStart 或替换任何现用模型、Prefab、输入、包和设置。实际尺寸、完整内部构造、历史准确性和 VR 性能尚未验证。

最新协作目标：小组先完成三个时代的精细模型，暂时不进入 main；见 [三个年代资产协作说明](three-era-collaboration.md)。本照片样件是研究候选，不计作某个时代已完成的最终模型。

## 对象与证据边界

用户于 2026-10-06 提供九张照片。照片 01–04 作为同一件实物的正面、侧面与背侧研究依据；05–08 是多件头盔的展柜，09 是另一件带上窗的完整潜水装具。后五张可用于比较，不能把其中的部件直接拼成首件。

照片 01 牌示可辨“中国制造”“1988”；馆藏号、厂牌细字、型号与完整牌示转录仍待更清晰资料确认。照片有透视畸变、反光和悬挂遮挡，不能从像素比直接得到可靠毫米尺寸。原图保留用户本地，仓库只保存来源编号、文件名和 SHA256；原图未作模型贴图，也未把照片中的人、展架和安全链条复制进模型。

| 来源 | 能支持什么 | 不能支持什么 |
| --- | --- | --- |
| 用户照片 01–04，编号见 [来源清单](../../artifacts/helmet-refinement/photo-study/qa/photo-sources.json) | 前窗的中央/三小圆结构、侧窗格栅、圆顶、颈环、胸甲和可见背部外形 | 精确尺寸、隐蔽机械、材料化验、接口功能和工作深度 |
| [香港海事博物馆藏品概览](https://www.hkmaritimemuseum.org/collection)，2026-10-06 查阅 | 博物馆藏品的背景；未找到与照片同一件实物的公开尺寸条目 | 不能作为本件 1988、型号或局部尺寸的独立网页证据 |
| [DESCO 中国接口封盖](https://www.divedesco.com/P/689/AirInletCap-ChineseThread)，2026-10-06 查阅 | 制造商说明 TF12/TF3 使用中国规格接口；其配件页标示螺纹 1.025–14 | 不是本件型号鉴定，也未把这一螺纹尺寸套进模型 |
| [SEAM 现售 TF12](https://www.haiyundive.com/products/tf12-diving-helmet.html)，2026-10-06 查阅 | 现售产品描述可作后续型号比较线索 | 页面重量/深度等参数不代表照片中 1988 实物，本件不采用这些性能值 |
| [美国海军水下博物馆 MK V](https://navalunderseamuseum.org/mk-v/)，2026-10-06 查阅 | 标准空气式 Mark V 的四窗、背部双鹅颈等资料，用于比较 | 不能把 Mark V 的左右接口功能直接分配给这件未知头盔 |

型号识别可继续研究 TF 系列线索，但本版文件使用中性名称 `photo-helmet-study`。需要先核对铭牌、馆藏信息和同一型号资料，再决定是否进入团队的三个年代展品。

## 做了什么

- 可编辑的空腔壳体、前窗固定座/框/透明窗片及圆形装饰结构、左右侧窗和弧形格栅。
- 颈环、胸甲、前部柱件、螺母与背部可见管件。材料/磨损由原创纹理表达，不是实物照片投射或扫描。
- 前窗、侧窗、胸甲、背部各有独立父节点；几何锚点只表示位置，不宣称具体供气或电话用途。
- 单独导出整体和前窗 FBX/GLB；Blender 源文件保留分件、可调整倒角和原始纹理。背景灯光/底座属于摄影棚，导出不含这些对象。
- 原创 1024×1024 基色/粗糙度/法线贴图，共六张；外壳使用连续圆柱 UV，其余采用自动展开。原图和第三方照片没有作为可发布资产重用。

这是首件视觉草稿。整体轮廓、前窗结构和材质还需要与照片并排审阅；制作出图不等于达到实物复原精度。

## 参数分类

| 参数 | 当前采用 | 性质 / 下次怎么核实 |
| --- | --- | --- |
| 单位 | Blender 米制，1 单位 = 1 米 | 导出约定，不代表实物经过测量 |
| 圆顶高度/半径 | 脚本中顶部 z=0.423 m，最大半径 0.148 m | 观察比例的建模假设；整体验证尺度待实物尺寸 |
| 壳体厚度 | 0.0032 m | 只为构成空腔的假设，不是历史壁厚 |
| 前窗座外半径 | 0.102 m | 照片比例近似；与整体一起调整 |
| 前窗透明片厚度 | 0.0025 m | 视觉假设，材料也未确认 |
| 胸甲周边螺栓 | 12 个均布位置 | 被遮挡的数量/间距仍待核实，本版排列是占位假设 |
| 螺纹 | 简化螺旋，脚本参数 | 外观表达，不满足真实配合或制造要求 |
| 前窗动作 | 本版没有开合动画 | 照片不能证明 Mark V 式铰链；以后先确认真实结构 |
| 顶部/内部 | 简化圆顶和内壁 | 未补未见上窗、气道、阀芯、耳机等隐蔽构造 |
| 质量/深度 | 不赋历史性能参数 | 必须来自同一实物/型号资料；不能由模型推算 |

完整参数在生成脚本中可查。后续修正参数应更新本表和版本说明；不能把本版假设升级为“文献测量值”。

## 文件与查看

- [整体 Blender 源文件](../../artifacts/helmet-refinement/photo-study/sources/photo-helmet-study.blend)：Blender 4.5 LTS。集合 `01_HELMET_EDITABLE_PARTS` 为模型，`02_STUDIO_NOT_FOR_EXPORT` 为预览环境。
- [整体 FBX](../../artifacts/helmet-refinement/photo-study/exports/photo-helmet-study.fbx) / [整体 GLB](../../artifacts/helmet-refinement/photo-study/exports/photo-helmet-study.glb)。
- [前窗 FBX](../../artifacts/helmet-refinement/photo-study/exports/front-window-study.fbx) / [前窗 GLB](../../artifacts/helmet-refinement/photo-study/exports/front-window-study.glb)。
- [整体预览](../../artifacts/helmet-refinement/photo-study/previews/01-overview.png)、[正面](../../artifacts/helmet-refinement/photo-study/previews/02-front.png)、[侧面](../../artifacts/helmet-refinement/photo-study/previews/03-left.png)、[背侧](../../artifacts/helmet-refinement/photo-study/previews/04-rear.png)、[前窗细节](../../artifacts/helmet-refinement/photo-study/previews/05-front-detail.png)。
- [几何统计](../../artifacts/helmet-refinement/photo-study/qa/geometry-report.json)；生成脚本 `artifacts/helmet-refinement/photo-study/scripts/build_photo_study.py` 支持 `--output` 指定新目录，重新生成时使用独立输出，避免覆盖人工修改。

GLB 携带预览材料与纹理。FBX 几何可导入 Unity，但玻璃/PBR 材质仍需在 URP 中独立配置；导出回读并不等于 Unity 中透明效果已经验证。

### 不安装 Blender 也能看

下载本分支并运行 `git lfs pull` 后，双击 [独立查看器](../../artifacts/helmet-refinement/photo-study/preview/review.html)，点击“选择 GLB”，选择 `exports/photo-helmet-study.glb` 或前窗 GLB。查看器已包含 three.js 0.180.0，不需要在线 CDN；模型单独载入，避免重复嵌入大文件。左键旋转、滚轮缩放、右键平移；可切正面/背面/侧面及仅查看部件。

GitHub 页面只用于阅读说明和预览 PNG，不能直接运行这份 HTML。可以从 Draft PR 下载代码 ZIP；若得到 LFS 文本指针，应从 GitHub 文件页单独下载实际模型或用 Git LFS clone。

查看器源码在 `scripts/viewer.js` / `scripts/viewer-template.html`；使用 three.js 0.180.0、esbuild 0.25.10 打包为 HTML。three.js 的 MIT 许可保留在 `preview/three-LICENSE.txt`。这两个开发工具没有加入 Unity Packages。

### 本版验证范围

Blender 4.5.14 回读整体与前窗的四份导出：无空几何/非有限坐标，边界与源文件误差小于 0.1 mm，三角形数一致，各网格有 UV；源文件六张贴图已打包，外壳非流形边为 0。[实际报告](../../artifacts/helmet-refinement/photo-study/qa/export-readback.json)

整体 113,976 三角形。这是当前资产统计，尚未做 LOD、部件合并或 VR 性能测试；不能直接作为运行时面数预算。自动 UV、螺纹与磨损仍是研究版，需要人工精修。

独立查看器在本机 HTTP / Codex 浏览器中实测载入、正/侧/背面、分件自动居中、拖动旋转、滚轮缩放、复位，以及文件选择器载入前窗 GLB；控制台未见错误或警告。[查看器记录与截图](../../artifacts/helmet-refinement/photo-study/qa/browser-review.json)。普通浏览器双击 HTML 的 `file://` 模式未在本次自动测试中验证：自动化浏览器不允许该协议，未绕过限制。

## 组员怎样继续

补资料：在 #25 写“照片研究样件”以及 URL/页码、对应照片/部件、是否同一实物、尺寸测量边界。采用后把资料写进本文件。看模型：说明 commit、视角、具体问题，附标注；作者回复处理结果。

修改模型：先在 #25 认领部件、声明文件和工作分支，不再为普通部件另开 Issue。一个 `.blend` 同一时段一位编辑者；不同部件可另存独立源文件，再由一位装配者汇总。当前源文件由 Fttt1003 负责，尚未把其他成员指定为写入者。

模型/分件通过审阅后先汇总到 `codex/helmet-refinement-assets`，暂时不进入 main。PR #28 保持 Draft，并指向资产汇总分支。三个时代模型完成、需要接入产品时再另建接入 Issue/PR。接入时需要重新绑定窗件、Renderers、高亮、热点与锚点，并重新生成对应网格的剖面数据；不能把本件直接覆盖现有 Mark V FBX。

## 优先待完善

1. 核实实物型号、馆藏号及真实整体尺寸；补顶面、正背面和铭牌近景。
2. 对照首四张照片调整圆顶/胸甲比例、前窗圆形结构位置和背部件外形。
3. 审核侧窗圆/椭圆程度、紧固件总数、透明结构材料与真实窗件连接方式。
4. 再做可用 UV、材质细调和面数/部件合并方案；当前几何统计只是资产信息，不是 VR 性能结论。
5. 选定最终展品之后才做主场景交互兼容、剖面、碰撞体、双眼透明排序和设备性能检查。

研究来源和照片不向模型授予工程/潜水用途。项目未做实物测量、真实头显、用户研究或 Player 构建，本轮也未更改项目既有功能。
