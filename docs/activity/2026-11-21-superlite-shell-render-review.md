# SuperLite 17B 黄色裸壳 R2 · 外形审阅

2026-11-21，承接 [Issue #25](https://github.com/Twispra/XRmuseum/issues/25) 下的[第一版裸壳](2026-11-21-superlite-bare-shell.md)。用户提供四张精细整盔渲染图和一段转台视频，要求先审阅黄色头壳。打开[独立 Blender 文件](../../artifacts/helmet-modeling/superlite17b-shell-review-2026-11-21/SuperLite17B-yellow-shell-R2.blend)可以直接旋转检查；[三组同机位对比](../../artifacts/helmet-modeling/superlite17b-shell-review-2026-11-21/review.png)左列为第一版，右列为 R2。[新版四视图](../../artifacts/helmet-modeling/superlite17b-shell-review-2026-11-21/preview.png)另含正面与底视。

## 从四张图和转台提取的结构

| 来源 | 可见特征与处理 |
| --- | --- |
| 图 1，无阀侧 | 黄色窗肩向后倾，上端升起，在圆顶前缘留下凹口。两块金属配重间露出的黄色凸台有较平的中面、近直侧边和大圆角。修改窗肩的后倾、峰谷及侧台的面和坡脚。 |
| 图 2，阀侧近景 | 窗肩的斜向宽面一直延续到侧颊。侧阀挡住了后缘的一段，两侧凸台分别估形。 |
| 图 3，底面 | 黑色颈套与蓝色内衬占据大部分画面。黄色壳边只露出一部分，本轮沿用既有裸壳颈口和沟槽。 |
| 图 4，侧后方 | 圆冠延伸至后配重上方，窗肩后缘形成连续斜向转折。用后斜视检查这条转折以及侧台到颈缘的过渡。 |
| 3DModels org 的 SuperLite 17 转台 | 实际查看了 YouTube 时间轴的逐秒分镜，0 秒为正面，2 秒为阀侧，4 秒为后面，6 秒为无阀侧；23 秒、27 秒补看前斜与后斜视。无阀侧凸台与黄色窗肩在转动中位置连续。标题标注 SuperLite 17，具体 17B 子配置与尺寸仍未知。[用户提供的视频](https://www.youtube.com/watch?v=3Obmic7dRyk) |

## 实际修改

以第一版保存文件为输入，连同前窗窝孔一起重新布置黄色窗座，抬起窗肩前缘，在它与圆冠之间留出凹口，使窗肩宽面向下接到侧颊。两侧仅在需要增加曲率控制的区域局部细分，用原外表面的采样结果同时修正内外壁。每个原有盲孔的孔口、孔壁和孔底分别作刚性变换，孔间的连续黄壳保持光顺。[修形脚本](../../artifacts/helmet-modeling/superlite17b-shell-review-2026-11-21/revise_shell.py)

Blender 4.5.14 LTS 从保存源回读，文件含一个网格、一款胶衣材质、73,494 个三角面。网格为单一连通实体，九项回读检查通过，包括内腔从面窗到颈口贯通、UV 有效、退化三角面和非相邻面交叠均为零。颈口与沟槽各 160 点的坐标与连线保持，16 个盲孔保留原孔形。细分增加了面数，当前结果用于外观审阅，尚未作运行时减面或设备性能验证。[几何回读](../../artifacts/helmet-interaction/qa/runs/20261121-issue25-bare-shell-r2/geometry.json) · [局部曲面与孔形复核](../../artifacts/helmet-interaction/qa/runs/20261121-issue25-bare-shell-r2/shell-details.json)

## 对照结果

图 1 用颈边、冠顶和近侧视方向估计机位，然后固定旧、新模型的相机和尺度。仅沿可见黄壳取样，距离按相同图像横坐标下的竖直差计算。冠顶位置参与机位估计。窗肩到凹口的图像落差从约 **33 px** 调整到 **81 px**，图 1 约 **79 px**；窗肩曲线的平均绝对偏差从 **33.58 px** 降到 **8.54 px**，可见冠弧从 **11.98 px** 降到 **3.48 px**。窗缘投影方向仍有差异，黄色窗肩与金属压圈交界和裸壳净孔边属于相邻的两条线。上述数据只能描述所用机位下的图像外形。[三线叠图](../../artifacts/helmet-modeling/superlite17b-shell-review-2026-11-21/reference-outline.png) · [图 1 测量报告](../../artifacts/helmet-interaction/qa/runs/20261121-issue25-bare-shell-r2/reference-fit.json)

另用上一版的冻结机位复核厂家 17BHDL-2 斜前示意图。壳体外轮廓交并比从 **0.906** 变为 **0.909**，净窗孔从 **0.888** 降到 **0.848**。前窗后倾同时影响该方向看到的开口宽高；审阅时可把新版正面窗孔和侧面窗肩一起看。四张整盔参考里被配重、阀体和颈套遮挡的区域继续按裸壳资料估形。[厂家图复核](../../artifacts/helmet-interaction/qa/runs/20261121-issue25-bare-shell-r2/archived-manual-fit.json)

## 版本与交接

原 checkout 为 `codex/superlite17b-unity-integration-2026-10-11@86715c9`，含既有资源记录与未跟踪的模型、活动和嵌套目录。实际工作在之前的独立裸壳分支 `codex/issue25-bare-shell-study@b13e838` 续做，开工时该工作树干净，fetch 后 `origin/main` 为 `e4a43db`。本轮测试环境为 Windows、Blender 4.5.14 LTS，加本轮未提交的脚本与研究资产，旧壳 SHA-256 为 `09766af684372e1a47bc5ecb810e2d6f9d30cc54fca3f51912768e733575ce09`，R2 为 `800f5175684cf9a31f83344bb03c16af2a079f7e77391a27ca1b17bdbccf777e`。用户图片与视频分镜仅供本机参照，交付的渲染来自项目模型。当前交接点为**用户审阅裸壳**，加零件须待审阅通过。
