# 头显旋转恢复后的高度反馈

关联 [Issue #48](https://github.com/Twispra/XRmuseum/issues/48) / [Draft PR #49](https://github.com/Twispra/XRmuseum/pull/49)。2026-10-13，用户在本聊天反馈修复后“转动没问题了”，但“视角还是有点低”。这是用户设备反馈，未取得实测眼睛高度、站姿/坐姿、地面校准或性能数据，不能写成全部实机验收通过。

本轮只读核对当前原工程：main / `4578e66`，本地保留上一轮 XR 修复镜像与原有 ProjectSettings 修改；fetch 后 origin/main 相同，#49 仍 Draft、未合并。没有启动 Unity、改相机/场景或设置统一身高补偿。

保存场景的 XR Origin 根 Y 为 0，地面表面 Y 为 0，采用 Floor tracking。Camera Y Offset 1.65 m 只用于 Device 模式，Floor 模式由设备姿态提供离地高度。中央展示台面的保存几何约为 1.16 m，头盔根节点约为 1.172 m。上一轮 gate 不改这些位置或高度。[Unity XR Origin 高度定义](https://docs.unity3d.com/Packages/com.unity.xr.core-utils@2.5/manual/xr-origin-reference.html)。

下一步先区分用户姿势和偏低表现：站着且虚拟地面离眼睛太近，先核对设备/runtime 地面校准；地面正常而坐姿下展台偏高，评估可调坐姿辅助或展示高度适配；站姿地面正常而展品偏高，检查展品空间布局。已询问用户这一区别，未收到姿势确认前不把任何一种解释写成根因，也不改动所有访客的高度。若需要代码调整，继续在 #48 / #49 明确实际范围后实现并验证，保留实时头部位置和旋转跟踪。
