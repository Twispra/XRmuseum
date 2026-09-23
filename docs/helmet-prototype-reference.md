# 潜水头盔建模试制：来源、尺寸与假设

日期：2026-09-23。范围：真实尺寸简模与一个精修前视窗样件。此文记录本轮的参考依据，不将试制品声明为可用于潜水的设备、精确历史复原或工程测绘模型。

## 原型定位

采用 **Mark V-inspired 标准空气式潜水头盔**。整体布局参考制造商实物照片，尺度以一件有公开尺寸记录的头盔为锚点。未取得原始制造图纸或实物测量数据。各局部尺寸、厚度、磨损分布和机械间隙属于原型设计假设，后续可调整。

## 公开来源

1. [DESCO 制造商：US Navy Mark V Diving Helmet](https://www.divedesco.com/P/162/MarkVDivingHelmet)
   - 制造商说明壳体为旋压铜、配件为铜合金铸件；当前产品窗片为 Acrylic，历史版本使用玻璃。四窗布局为前方圆形铰接视窗、两侧圆窗和顶部椭圆窗。
   - 页面提供正面、侧面与细节照片，可用于轮廓和构造观察。当前产品存在镀锡和抛光外观，不能将铜色一概视为唯一历史标准。
   - 只用作视觉参考。照片没有作为模型贴图、材质或可发布资产重用。
2. [U.S. Naval Undersea Museum：MK V Diving Rig](https://navalunderseamuseum.org/mk-v/)
   - 博物馆说明标准空气式头盔的四个视窗、后方两个鹅颈连接件，以及头盔与胸板的连接关系。
   - 同页还介绍混合气型装备；本轮不采用其大型净化罐等结构，也不将整套装备重量用于头盔物体。
3. [Case Auctions：Lot 636，US Navy WWII Mark V Dive Helmet](https://caseantiques.com/item/lot-636-us-navy-wwii-mark-v-dive-helmet/)
   - 此件实物记录约 **19.5 in 高 × 12.5 in 宽 × 16.5 in 深（最深处）**，换算为 **0.4953 × 0.3175 × 0.4191 m（H×W×D）**。
   - 本轮整体尺度锚点。包含胸板，不是单独圆形 bonnet，也不是潜水员全套衣装。拍卖描述提供外包尺寸，不提供局部工程尺寸。
4. [Hampton Roads Naval Museum：In the Collection: Mark V Diving Helmet](https://hamptonroadsnavalmuseum.blogspot.com/2023/02/in-collection-mark-v-diving-helmet.html)
   - 另一件馆藏标为约 20 in 高、14 in 宽、15 in 深；高度量到胸板尖端，宽度以侧窗、深度以前窗至后部为边界。
   - 仅作真实物体数量级交叉检查。对象和量测边界不同，**不与 Case 数据取平均，也不混合成一个“精确尺寸”**。

## 可直接查看的照片

均来源于 DESCO 同一产品页。照片仅保留为本机研究副本，未确认再分发许可，因此不随仓库分发；下表的原始 URL 可追溯来源：

| 本地研究副本 | 原始图片 URL | 用途 |
|---|---|---|
| 铜色正面（本地研究副本） | [PICT0010.jpg](https://www.divedesco.com/Content/files/ProductImages/PICT0010.jpg) | 轮廓、前窗格栅、铰链与锁扣 |
| 镀锡正面（本地研究副本） | [PICT0001.jpg](https://www.divedesco.com/Content/files/ProductImages/PICT0001.jpg) | 材料对比、胸板构造 |
| 抛光外观（本地研究副本） | [Mk V Polished.jpg](https://www.divedesco.com/Content/files/ProductImages/Mk%20V%20Polished.jpg) | 侧向部件与金属层次 |
| 产品主图（本地研究副本） | [Mark_V_Plated_ws.jpg](https://www.divedesco.com/Content/files/ProductImages/Mark_V_Plated_ws.jpg) | 整体构造 |
| 铜色左侧（本地研究副本） | [left.jpg](https://www.divedesco.com/Content/files/ProductImages/left.jpg) | 侧窗、电话杯与前窗厚度 |
| 铜色右侧（本地研究副本） | [PICT0014.jpg](https://www.divedesco.com/Content/files/ProductImages/PICT0014.jpg) | 排气结构与后部连接件 |
| 镀锡侧面 A（本地研究副本） | [PICT0004.jpg](https://www.divedesco.com/Content/files/ProductImages/PICT0004.jpg) | 侧窗与胸板弧度 |
| 镀锡侧面 B（本地研究副本） | [PICT0005.jpg](https://www.divedesco.com/Content/files/ProductImages/PICT0005.jpg) | 排气结构与壳体轮廓 |

## 根据照片提出的布局建议

以下是对实物照片的视觉观察，用于建模，不代表已测量尺寸：

- 前视窗为大型圆窗，保护条形成中央菱形网格；不能简单替换成中心十字。边框具厚度和圆润倒角。
- 正视图右侧是竖向铰链，左侧是锁紧件。可动前窗、固定窗座、铰链轴、锁扣应独立命名并分件。
- 顶窗在壳体前上方，为横向椭圆／圆角轮廓并向前倾，防护条与前窗形式不同。
- 两侧圆窗与高位电话杯分清；不能把不透明电话杯当成第五个视窗。
- 胸板是向肩部延展的曲面，不是水平圆盘。颈口与胸板上沿应在材质和轮廓上有层次。
- 本轮可以省略或概括内部配气通道、完整螺纹和后部功能细节，但需在交付说明中保留其尚未实现状态。

## 外观样件策略

建议对精修样件使用铜壳、较明亮的铜合金边框、深色密封圈与有厚度的透明窗片，集中验证轮廓、倒角、微表面粗糙度、局部氧化和光线下的材质区别。外观磨损应作为设计解释，不应伪称来自某件特定历史实物的扫描。以 Blender 渲染和 Unity 中的运行效果分别验收；本轮渲染不能代表已通过 VR 性能与手感测试。
