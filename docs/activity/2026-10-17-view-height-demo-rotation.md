# 2026-10-17：视角高度和演示设备观察

本轮按用户要求继续本地验收，不提交、不推送、不更新远程 Issue / PR。基线 `bc9df77a56bf6dff18af9b25c528c82b49417ca4`，分支 `codex/diegetic-single-hand-ui-2026-10-14`；开工 fetch 后 origin/main 与基线相同。此前实体 UI、固定剖面滑轨与整盔旋转候选改动保留。

## 做了什么

- 设置新增“降低视角／恢复高度／升高视角”，三语同步。步长 5 cm、相对跟踪高度 ±50 cm 是原型配置。高度仅在本次运行保留；F11、展品复位不清除偏好。
- `VisitorViewHeight` 在 Camera Offset 下添加运行时偏移父节点，一起偏移相机和手柄，不覆盖跟踪局部姿态、CameraYOffset 或物理地面；停用后可还原原层级并保留实时跟踪与移动。
- 演示设备在展开时按展台初始方向摆在头盔旁边，不再随用户视角或头盔旋转重排。模型保留三维朝向，只有短说明朝向访客。
- 直接按住设备表面，使用整盔的同一用户视角滚动球映射旋转，固定几何中心、松手停住；纵深伸手不引入隐藏滚转，起步有 4 mm 容差。关闭重开恢复初始摆放。
- 精细设备外层展示轴与 FBX 机械采样根分开；转动设备时泵轮／振膜、声音、注意力动画继续，软管／电缆端点仍读取旋转后的真实接口。
- 关闭演示、复位、换年代、视角高度改变、F11 或模块停用先取消手势；按住的输入不能直接转移到其他目标，需松开再按。设备隐藏／模块停用恢复原悬停高亮。

## 文件与交接

`HelmetDiegeticStation.cs` 接命令、设置及生命周期；`HelmetDiegeticEquipment.cs` 负责演示设备的命中、反馈和互斥；`HelmetGestureInteractable.ConfigureEquipment` 复用现有旋转算法。设备描述来自 `HelmetConnectionDemonstration.EquipmentExhibits`，精细设备展示轴公开在 `RefinedConnectionEquipment`。不要把 Position 放回每帧更新，也不要将展示旋转改到 FBX 采样根内部。

命中区只给主要表面加代理，忽略最大尺寸小于 18 mm 的细节独立碰撞盒，原模型网格不变。代理在所属网格子层级中跟随机械动作；悬停材质仅在状态改变时更新。18 mm 是本轮实现参数，不是无障碍标准。

设置区只显示按钮名称，删除每个按钮重复的“点击选择”说明；开启提示常显时也不挤占相邻按钮。

新增四条三语文案，表共 255 条、标准字符清单 574 个显示字符；原源 TTF cmap 7671 个码位不变。静态 SDF 原位补烤为 585 字符，保留历史字形及原 GUID／子资源身份；磁盘图集与渲染缓冲一致。

## 验证记录

最终保存主场景回归 **731/731 通过**，错误／异常 0，38 个被测源码／资源指纹与运行结束后磁盘内容一致。分层：public-api 402、xri-registration 154、production-input 164、module-lifecycle 11。报告：[report.json](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/current.json)、[摘要](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/current.json)、[字体证据](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/font-summary.json)。

测试命令：Unity 2022.3.62f3c1 `-batchmode -projectPath <checkout>/MuseumVR -executeMethod Museum.Diegetic.Editor.HelmetDiegeticValidation.RunBatch -logFile <local-output>/validation.log`。输入通过正常 Dynamic Input System 更新及原有 Input Actions／XRRayInteractor；没有直接调用 SelectEnter、手工 InputSystem.Update、保存测试后的场景或重建原型。

首轮实现通过 1012 项检查；减少细节碰撞代理后，泵 86 个、表面电话 43 个选择代理，逐代理注册断言数量随之减少，原回归及行为断言均保留。本轮另加五项高度相对姿态／高亮恢复断言。期间一个新增验证函数曾发生局部变量重名编译错误，修正在验证脚本内，未放宽产品要求。

验证覆盖实际射线点击三种高度按钮、上下限、F11 保留偏好、头手相对姿态／地面／展品不变、设备不随头盔／头部移动、实际按住和松手、动画继续、管线两端绑定，以及取消／年代切换／模块停用。原 1048 个未知未跟踪文件内容与基线全部相同；场景安装器只造成空白变化，核实语义一致后恢复开工前场景字节，没有重建或替换展品、Prefab、输入或项目设置。

三语设置及设备旋转前后画面已人工查看：[中文设置](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/review-previews/15-height-settings-cn.png)、[繁体设置](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/review-previews/16-height-settings-tc.png)、[英文设置](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/review-previews/17-height-settings-en.png)、[设备转动前](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/review-previews/13-pump-fixed-beside-helmet.png)／[转动后](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/review-previews/14-pump-rotated-in-place.png)。这些是运行主场景的普通摄像机审阅图，不是真实头显截图。

真实头显／控制器、Direct 实手可达性、双眼可读性、舒适度、VR 性能和可执行构建未验证。模拟输入与 AI 走查不等于真实用户或小组人工验收。

## 验收步骤

1. 打开现有 MuseumStart，运行后打开设置；依次点升高、降低和恢复高度，看展台不动而视角与手一起变化。
2. 改高度后按 F11 或点展品复位，高度应保留；“恢复高度”回到当前跟踪基准。
3. 点击供气／通信接口展开设备，移动头部／从侧面观察，设备应留在原位。直接按住主要表面，左右和上下移动手，松手停住。
4. 旋转设备时观察内部机械动画和真实端口处的管线，再关闭重开，检查默认角度恢复。
5. 切换三种语言、年代，打开前窗和剖面滑轨；检查原功能仍可操作。

键盘模拟仍使用当前项目的 G 选择键（按住移动为旋转），不是鼠标左键扳机。完整操作与维护说明见 [实体 UI 交接](../diegetic-ui.zh-CN.md)。

发布补记：上文为最后一轮本地验收快照。用户随后明确同意提交，当前发布与五项待认领任务见 [发布交接](2026-10-17-diegetic-release.md)。原始报告保留，公开链接为注明原报告SHA的发布副本。
