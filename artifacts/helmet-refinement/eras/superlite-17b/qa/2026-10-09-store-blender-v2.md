# 2026-10-09：SuperLite 17B v2 商店版 Blender 本地生成与回读

工作分支 `codex/superlite17b-details-2026-10-08`，测试开始时 HEAD `257fcf08b5594eac9bb21c20b5f33d3e4b9352c9`；本次脚本、参数、资料及生成资产均为**额外未提交的本地改动**。既有 v1 资产未覆盖。测试运行时尚未更新 GitHub PR；本报告随本轮交付提交。

## 环境与步骤

- Windows；微软商店版 Blender 4.5.14 LTS，包 `BlenderFoundation.Blender4.5LTS`，通过 Windows 应用启动接口运行后台脚本。官方便携版虽可启动、建模和渲染，但 GLB 导出时内部 `select` 模块被本机应用控制策略拦截；本轮以商店版完成生成与回读，没有关闭安全防护。
- 在新的空暂存目录生成两张原创 v1 贴图；运行 `scripts/build_faceport_exterior_v2.py`，产出面窗/侧阀各一份 `.blend` 和 `.glb`，以及各正面/斜前预览。使用厂家 [17B 零件图 #241108001](https://www.kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf)、[面窗手册 #260901001](https://www.kirbymorgan.com/sites/default/files/2026-09/face-port-port-retainer-nose-block-260901001.pdf) 和 [侧阀分解图 #230118015](https://www.kirbymorgan.com/sites/default/files/2023-01/chromed-brass-side-block-blowapart-230118015.pdf) 核对命名与分件数量；照片外形来源见 `references/README.md`。
- 用同一商店版运行 `scripts/check_faceport_exterior_v2.py`，重新打开两个 `.blend` 并重新导入两个 GLB。结果存于 [机器回读 JSON](faceport-exterior-v2-readback.json)。

## 结果

| 项目 | 面窗 | 侧阀 |
| --- | --- | --- |
| Blender 源与 GLB 网格数一致 | 16/16 | 32/32 |
| 网格命名一致、UV 均存在 | 通过 | 通过 |
| 审阅用背景/灯光未进入导出 | 通过 | 通过 |
| 贴图打包 | 两张原创贴图 | 两张原创贴图 |
| 镜片透明材质在 GLB 中 | 通过 | 不适用 |
| 压圈前后深度 | 0.012 m，与本地估算参数一致 | 不适用 |

自动回读另核对了面窗压圈 11 枚螺钉及侧阀 520-524、520-525 两种不同控制钮。两份 GLB 均有 `glTF` 文件头并可被 Blender 导入。面窗仅表示 11 枚压圈固定螺钉；另 4 枚排气翼螺钉不在此独立件中。螺钉**数量**依据厂家手册，孔位/头部大小仍为视觉估算。可编辑源与模型预览可供审阅，但本轮机器检查**不证明**轮廓测绘精度、材质实物一致性、壳体装配、Unity 运行、头显交互或潜水设备安全性。当前侧阀外观和面窗下缘仍有 [照片对照页](../references/photo-model-comparison-v2.zh-CN.md)列出的差异，需要后续人工审阅和修形。
