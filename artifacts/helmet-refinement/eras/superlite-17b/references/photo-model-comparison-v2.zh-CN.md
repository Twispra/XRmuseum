# 厂家参考图与模型对照：v2 本地首轮渲染

日期：2026-10-09；对象为拟采用的 SuperLite 17B P/N 500-010（posts）之独立**面窗/侧阀外观研究件**。厂家网页将下列照片归于 17B，但照片背部不可见，不能单靠它们证明通信接口正是 posts；拍摄年份也未知。原图版权归 Kirby Morgan，本仓库只给链接和自制渲染，不收录原图或用它作贴图。

| 模型、视角和部位 | 厂家参考图 | 本地 v2 模型近似视角 | 观察到的差异及下一步 | 剩余不确定 |
| --- | --- | --- | --- | --- |
| 面窗，斜前方，上下轮廓 | [17B 黄色整盔主图](https://www.kirbymorgan.com/sites/default/files/2021-02/17B-Helmet-Colors-yellow.jpg)，产品页主图；照片为斜前视角 | [v2 斜前渲染](../previews/SL17B_FacePort_threequarter_v2.png) | v2 上缘比 v1 高拱、下缘较平，已移除误作整圈的 `UpperVisor`。但照片中的下沿和侧壁过渡仍较模型更平直，需继续修轮廓。 | 相机未配准，照片透视不能量孔距；实际壳体开口曲面未知。 |
| 面窗，近正面，压圈与鼻夹 | 同一[整盔主图](https://www.kirbymorgan.com/sites/default/files/2021-02/17B-Helmet-Colors-yellow.jpg) 与 [零件图 #241108001](https://www.kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf) 第 1 页位置 29、33、35、37；[面窗手册 #260901001](https://www.kirbymorgan.com/sites/default/files/2026-09/face-port-port-retainer-nose-block-260901001.pdf) FCPRT-2 | [v2 正面渲染](../previews/SL17B_FacePort_front_v2.png) | 压圈厚度 0.012 m 已与参数表一致，但属估算。手册区分 11 枚压圈螺钉和 4 枚排气翼螺钉；本独立面窗件只表示前者。 | 孔位/头部尺寸、真实厚度及 O-ring 沟槽未知；四枚排气翼螺钉需与未来主体装配关系确定。 |
| 侧阀，斜侧，壳体/旋钮/接头 | [17B 产品页“Side Block”特写](https://www.kirbymorgan.com/sites/default/files/2021-02/fiberglass-side-block-IMG_0387.jpg)；[侧阀分解图 #230118015](https://www.kirbymorgan.com/sites/default/files/2023-01/chromed-brass-side-block-blowapart-230118015.pdf) 位置 11、20、30、33 | [v2 斜前渲染](../previews/SL17B_SideBlock_threequarter_v2.png) | v2 已将两个钮错位、改变轴向，但中央多边形板与过于规整的浅盒体仍不像照片中的横向金属阀体、深色下垂标牌。应急阀为可分独立组件，须在分件命名中体现。 | 侧阀图是局部透视照片；不能确认装配孔距、真实轴角或 500-010 背部通信接口。 |
| 侧阀，近正面，控制钮相对高度 | 同一[侧阀特写](https://www.kirbymorgan.com/sites/default/files/2021-02/fiberglass-side-block-IMG_0387.jpg) 与 [17B 黄色整盔主图](https://www.kirbymorgan.com/sites/default/files/2021-02/17B-Helmet-Colors-yellow.jpg) | [v2 正面渲染](../previews/SL17B_SideBlock_front_v2.png) | 两钮相对高度比 v1 接近照片；仍需辨别稳流钮 520-524 和应急钮 520-525 的独立形态与位置。 | 模型图是正交视，厂家照片有透视；不得把像素差当成精度测量。 |

这组对照只用于**观察和审阅**：照片与模型不是相机标定后的同角度配准，没有像素精度指标。v2 源文件/GLB/预览已在本地用微软商店 Blender 4.5.14 生成并回读，通过名称、UV、镜片透明材质与辅助背景排除检查；这不证明外形或装配精度。整盔正交前/侧/背/顶视资料缺口见 [整盔资料索引](whole-helmet-source-map-v2.zh-CN.md)。
