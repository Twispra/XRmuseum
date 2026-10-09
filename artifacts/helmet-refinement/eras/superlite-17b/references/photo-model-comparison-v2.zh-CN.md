# 厂家参考图与模型对照：v2 待重建

日期：2026-10-09；对象为拟采用的 SuperLite 17B P/N 500-010（posts）之独立**面窗/侧阀外观研究件**。厂家网页将下列照片归于 17B，但照片背部不可见，不能单靠它们证明通信接口正是 posts；拍摄年份也未知。原图版权归 Kirby Morgan，本仓库只给链接和自制渲染，不收录原图或用它作贴图。

| 模型、视角和部位 | 厂家参考图 | 现有 v1 模型同类视角 | v2 修改依据/目标 | 剩余不确定 |
| --- | --- | --- | --- | --- |
| 面窗，斜前方，上下轮廓 | [17B 黄色整盔主图](https://www.kirbymorgan.com/sites/default/files/2021-02/17B-Helmet-Colors-yellow.jpg)，产品页主图；照片为斜前视角 | [v1 斜前渲染](../previews/SL17B_FacePort_threequarter_v1.png) | 新脚本拟将上下对称椭圆改为上拱较高、下缘较平的非对称轮廓；上方中央和鼻夹区不再均匀布点。零件图 #241108001 第 1 页位置 1/13 表明 visor 520-054 随壳体，故在 v2 脚本中移除 v1 全环 `UpperVisor`。 | 主图透视使轮廓/孔距不能精测；没有严格正视和壳体开口实测图。v2 源文件/渲染尚未生成。 |
| 面窗，近正面，压圈与鼻夹 | 同一[整盔主图](https://www.kirbymorgan.com/sites/default/files/2021-02/17B-Helmet-Colors-yellow.jpg)（仅近似正面）与[零件图 #241108001](https://kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf) 第 1 页位置 29、33、35、37 | [v1 正面渲染](../previews/SL17B_FacePort_front_v1.png) | 新脚本保留独立压圈、面窗、O-ring 和鼻夹外露旋钮；把压圈脚本前后厚度 0.012 m 与 v2 参数统一，仍为估算。 | 真实压圈厚度、O-ring 沟槽和螺钉配合未知。 |
| 侧阀，斜侧，壳体/旋钮/接头 | [17B 产品页“Side Block”特写](https://www.kirbymorgan.com/sites/default/files/2021-02/fiberglass-side-block-IMG_0387.jpg)；图名/展示位置见[产品页](https://www.kirbymorgan.com/products/helmets/superlite-17b) | [v1 斜前渲染](../previews/SL17B_SideBlock_threequarter_v1.png) | 新脚本拟以截角不规则壳体替换规则盒体，将两个正面平行旋钮改为不同位置/轴向，并把外部连接短柱移到侧下方。深色铭牌仅用无字原创块示意，不复制商标。 | 照片透视明显，控制轴及接头尺寸都未测得；特写不能区分 500-010/011 背部接口。v2 源文件/渲染尚未生成。 |
| 侧阀，近正面，控制钮相对高度 | 同一[侧阀特写](https://www.kirbymorgan.com/sites/default/files/2021-02/fiberglass-side-block-IMG_0387.jpg) 与 [17B 黄色整盔主图](https://www.kirbymorgan.com/sites/default/files/2021-02/17B-Helmet-Colors-yellow.jpg) | [v1 正面渲染](../previews/SL17B_SideBlock_front_v1.png) | 新脚本将两钮错开高度；未来的正交模型图只供审阅，不能称为与照片相机精确配准。 | 装配时要核对安装面、对外方向、弯管路径和比例。 |

图像比较目前是**v1 缺陷定位与 v2 修正目标**，不是 v2 结果。照片与 v1 镜头只是近似角度，没有相机标定、透视配准或像素误差指标。当前本机 Windows Smart App Control 拦截官方便携 Blender 的 OpenEXR.dll，错误 `0xC0E90002`；v2 `.blend`/GLB/渲染与回读尚未生成，不能当作已完成或已验证。整盔正交前/侧/背/顶视资料缺口见 [整盔资料索引](whole-helmet-source-map-v2.zh-CN.md)。
