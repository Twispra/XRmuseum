# R1 检查记录 · 2026-10-07

基线 `0c0aeee6337607bf9d8230e6575c7ed69fde75f1`，分支 `codex/connection-demo-assets-2026-10-07`；被测内容为该基线加本任务文件，提交时的文件哈希见 `delivery-manifest.json`。没有修改 `MuseumVR/`。

| 检查 | 环境／证据 | 结果 |
| --- | --- | --- |
| 源模型运动 | Blender 4.5.14 LTS，`verify_equipment.py`；`export-readback.json` | 61 帧循环；两活塞行程均 0.18415 m；连杆长度及端点一致；原始贴图均已打包 |
| 双格式导出 | 三套设备各回读 GLB / FBX | 三角形数、原点状态包围尺寸与源一致；0 / 0.5 / 1 / 1.5 / 2 秒的运动节点姿态匹配。GLB 必须启用所有运动 clip，FBX 按实际 take 起点帧 2 对齐 |
| 源网格 | `source-geometry.json` | 431 meshes、95,452 triangles，非流形边 0；装饰字体有 10 个极小面，阈值 `1e-12 m²` |
| 流程边界／方向 | Node.js 24.19.0，`node scripts/test_timeline.mjs` | 3,282 个断言通过；含步骤起点、首次到达、回复反向、结束后停止及所有信号包位置范围 |
| 实际浏览器 | Codex in-app browser、本地 HTTP；`browser-controls.json`、三张 `browser-*.png` | AIR / VOICE 播放、重播、暂停冻结、拖动暂停、复位、镜头与设备展开、三语、低动态通过；通信回复在 12.3 秒读到 arrived / reply；检查时无 console warn / error |
| 音轨 | `audio/manifest.json`、浏览器音效状态 | 六条 16 s、48 kHz mono PCM16，零数字削波；三方案可启动／切换，暂停取消音效。仅验证音频数据与播放状态，未进行人工听感评分 |
| 文件一致性 | `python scripts/verify_delivery.py` | 源／导出／观察副本哈希，内嵌模型和音轨与交付原文件一致；没有外部脚本／CDN，保留 Three.js MIT 声明 |

复核命令使用本机已安装的 Blender、Node、Python，工具路径由使用者提供，不在仓库硬编码个人路径。Blender 示例：

```text
blender -b --python scripts/verify_equipment.py -- --root <本资产目录绝对路径>
node scripts/test_timeline.mjs
python scripts/verify_delivery.py
```

重新构建浏览器演示：Python 运行 `scripts/build_viewer.py --esbuild <已安装esbuild可执行路径> --node-modules <含three的node_modules目录>`。本轮 Three.js 0.180.0、esbuild 0.25.10。没有向 Unity 增加 npm 或运行时依赖。

未验证：真实 VR / Unity 导入与运行、帧率／GPU 成本、实机音量／听感、访客理解及舒适度、同型号设备的完整实测外形。浏览器工具拒绝 file 协议，未绕过；本轮测试用本机 HTTP，因此组员双击单文件离线打开仍待复核。

模型、动画和数字音效的检查不等于历史复原认证，也不等于小组人工审批。后续改动源文件或导出后需另作检查，不复用本报告冒充新版本验收。
