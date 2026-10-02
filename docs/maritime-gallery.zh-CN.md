# 潜水展厅环境说明

2026-10-02；对应 [Issue #4](https://github.com/Twispra/XRmuseum/issues/4)。本地实现位于 `codex/museum-atmosphere-2026-10-02`，基于 `74556aadae39607b23b8ac2b8fd35e8042019393`。交付分支通过 PR 审查，尚未合并 main；10 月 3 日的新回归与接手方法见[发布交接](activity/2026-10-03-gallery-release.md)。

## 设计与内容

围绕现有可交互头盔建立安静、现代的海事博物馆氛围，面向包括较年长者在内的成人 VR 新手，支持坐姿、单手探索的设计方向。深海军蓝展墙、暖白文字、铜色细部和少量青色标记形成统一层次；中央头盔仍是观看与操作中心。

前方有主题墙，左右有观察与连接主题，回头可见入口说明，抬头有顶棚与静态灯环，低头可见连续地面和细嵌线。主要操作仍使用原头盔与个人面板，不要求访客依次操作所有方向。

两侧原占位物已停用，替换为静态舷窗造型及标有 `AIR / VOICE` 的双通道造型，展签注明 `INTERPRETIVE DISPLAY`。它们是解释性形式，没有对应某件真实馆藏，也没有实现气流、语音传播、管路连接或教学动画。中央展签使用 `MARK V-INSPIRED STUDY`，模型来源与简化边界见[头盔参考说明](helmet-prototype-reference.md)。后墙线纹是装饰浮雕，不是海底地图或测量数据。

参考只用于提炼展陈方法；本次未引入参考页面的照片、视频或第三方模型：

| 官方来源 | 参考内容与本项目改编 |
| --- | --- |
| [BIG：Danish Maritime Museum](https://big.dk/projects/danish-maritime-museum-1699) | 围绕旧干船坞组织现代建筑。借鉴历史对象与现代空间的主次关系，未复刻馆舍。 |
| [Kossmanndejong：Maritime Stories of Denmark](https://kossmanndejong.nl/project/maritime-museum-denmark/) | 通过舷窗展柜、物件与空间视角组织海事故事。本项目以舷窗圆环和连接主题回应头盔特色，没有复制其素材或展陈布局。 |
| [Royal Museums Greenwich：Sea Things](https://www.rmg.co.uk/national-maritime-museum/attractions/sea-things-gallery) | 物件、互动与自主参观路线。借鉴“先观察、按兴趣继续”的节奏，未把实体展馆尺寸直接作为 VR 标准。 |

## 场景整合与编辑入口

直接打开已保存的 [MuseumStart.unity](../MuseumVR/Assets/Museum/Scenes/MuseumStart.unity)。场景新增独立根对象 `Maritime Gallery Environment`，引用 [MaritimeGallery.prefab](../MuseumVR/Assets/Museum/Features/MaritimeGallery/Prefabs/MaritimeGallery.prefab)。环境使用自己的 `Materials/`、`Meshes/` 和 `Editor/`，均在 `Assets/Museum/Features/MaritimeGallery/` 下。

原地面、墙面和展台换用环境材质，原环境光及主灯色调调整；旧欢迎文字、编号等 Renderer 隐藏。两个 `Replace With Museum Exhibit` 对象保留但停用，连同它们的占位碰撞体。新环境只为原缺口增加前墙与顶棚碰撞体，装饰模型没有添加抓取、刚体或 XR 目标。

原 XR Rig、摄像头、输入配置、传送地面组件、头盔资产与运行时交互代码继续沿用；场景中的头盔归位姿态及 RESET 关系应由基线比较确认。具体检查结果与限制见 [QA 记录](../artifacts/maritime-gallery/qa/README.zh-CN.md)，不能把“保留原逻辑”解释为本次已经完成实机回归。

调整文字、灯光和摆放可编辑专属 Prefab；调整配色可编辑专属材质。若需要修改程序几何，入口是 [MaritimeGalleryBuilder.cs](../MuseumVR/Assets/Museum/Features/MaritimeGallery/Editor/MaritimeGalleryBuilder.cs) 的 `BuildEnvelope`、`BuildCeiling`、`BuildFeatureWall`、`BuildSideGalleries`、`BuildArrivalWall`、`BuildPlinthDetails`。已经保存的 Prefab 是普通打开与预览的入口。

**日常打开、预览或验收不要运行 Builder，也不要运行旧头盔初始化工具。** `Install` 仅用于首次安装，已有专属 Prefab 时会拒绝。开发修订入口 `ReviseOwnedEnvironment` 没有菜单，也不会自动运行；明确修订时，它先把主场景保存前文件以及功能目录内全部文件（包括资源和对应 `.meta`）复制到仓库 `tmp/maritime-install-时间戳/`，再更新本功能资产。修订会覆盖专属 Prefab、材质和网格，因此应先核对人工改动与写入者，保留 GUID；不能把修订当作修复其他成员资产的按钮。

## 人工检查与边界

在正常使用流程中检查以下视角，不必为了看齐装饰而强迫访客转身或仰头：

1. 从原始起点与坐姿靠近头盔：检查展品是否清楚、主题文字是否遮挡轮廓、个人面板是否被新几何挡住；再检查拿起、转动、前窗、剖切和归位。
2. 分别看左、右、后方，再从展品后方回看：确认墙面有完整构图，文字朝向正确，侧面装置不会被误认成可操作的新展品。
3. 抬头与低头：检查顶棚、边界、灯环、地面接缝是否穿插、闪烁或产生干扰；近看低位展签，确认坐姿下可辨认。
4. 使用目标 PC VR 设备检查单手流程、实际文字清晰度、双眼显示与帧时间；分别记录问题。编辑器截图和自动检查不能替代这些结果。

当前使用静态造型与灯光，没有动态海水、闪烁灯带或新增镜头运动。部分材质采用 Unlit，新增聚光灯不投射阴影，装饰网格按组组合；这些是视觉与成本取舍，并不表示已测得性能达标，也不代表完整物理光照或可随意碰撞的房间模拟。碰撞、边缘可见性和传送仍需按实际使用路线复查。

本任务保持 Windows PC VR / OpenXR 方向，未迁移平台。真实头显、成年新手与较年长访客试用、可执行构建，以及空气／通信教学内容的进一步实现，均须有各自证据后再声明完成。
