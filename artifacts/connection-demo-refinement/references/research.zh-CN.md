# 依据核查与建模取舍

查阅日期：2026-10-07。来源地址、读取字节数与 SHA-256 见 [sources.json](sources.json)，参数性质见 [parameters.json](parameters.json)。扫描件在本机核查，仓库仅留链接和摘要。

| 编号 | 可查依据 | 用在本轮哪里 |
| --- | --- | --- |
| R01 | [U.S. Navy Department, Diving Manual, July 1916](https://diving-rov-specialists.com/index-b-documents_htm_files/history-34-us-navy_diving-manual-july-1916.pdf)，原政府手册的托管扫描件 | 印刷页 37（PDF 第 97 页）：Mark III 双缸、双作用、内径 4¼ 英寸、行程 7¼ 英寸。页 39（PDF 103）：两曲柄相差约 90°。页 41（PDF 113）：六个干电池、收发话配置、独立线路与公共回路。图版 XII–XVI：泵的外观和连接布局；XIX、L：电话装置与线路 |
| R02 | [South Georgia Museum, Diver’s suit](https://sgmuseum.gs/collection/divers-suit/)，藏品 1992.4.026／手动泵 1992.4.025 | 交叉理解软管、手动供气及水面支持。该馆泵不是本模型的已确认同型号，不取其数据混用 |
| R03 | [DESCO, Building a USN Mark V Helmet](https://www.divedesco.com/commercialdiveequipment/construction) | 空气接头、电话连接及电话杯的区分。当前制造商说明不能反推 1916 年电话设备的所有结构 |
| R04 | [U.S. Naval Undersea Museum, MK V Diving Rig](https://navalunderseamuseum.org/mk-v/) | 水面供气系统的展览语境，不能据此声明演示设备和具体 1944 年藏品配套 |

扫描文字的分数会被 OCR 漏掉。本轮对照页图，采用 **4¼ / 7¼**，而非只读取成 4 / 7。按 1 英寸 = 25.4 mm 换算，缸内径为 **107.95 mm**，活塞行程为 **184.15 mm**。这两项属于泵的内工作尺寸，不能拿来当外壳直径或整个设备高度。

结构表达采用双侧手轮驱动同一曲轴，两缸错开约 90°，让观众理解轮转为何变成活塞往复。曲柄半径取半个行程，连杆长度 290 mm 为建模估算；没有把动画解释成精确气路／阀门／泵送性能仿真。铜槽、木箱、边包、气缸等参考图版与文字配置，局部尺寸和老化材质是原创造型加工。

电话采用“单耳接听＋独立发话器”的水面装置，另列潜水员耳机／发话器示意，避免把现代双耳耳麦造型当成手册实物。六个电池和线路职责有依据，外壳尺寸、电池尺寸、插头形式及具体接线布置含估算。模型没有带序列号的假铭牌、额定电压或未经核实的仪表量程。

浏览器中的可见空气、通信信号包和发话器膜片运动，是为解释“从哪里来、到哪里去”制作的效果。所有深度、额定压力、真实传输延迟及潜水性能比较均未据此推算。三种音效在 [synthesize_audio.py](../scripts/synthesize_audio.py) 中原创合成，不借用馆藏、影视或历史音频，不把合成噪声当作历史电话实录。

目前最需要补充的证据是同型号设备的完整尺寸图、具体配装年代与电话部件细节。如果组员找到新资料，请记录藏品／页码、来源链接和测量含义，再改对应估算项；不同藏品数据不得拼成一套“实测尺寸”。
