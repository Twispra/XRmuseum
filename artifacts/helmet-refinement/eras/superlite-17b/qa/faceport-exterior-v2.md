# SuperLite 17B v2 研究与脚本检查（模型运行受阻）

日期：2026-10-09；工作分支 `codex/superlite17b-details-2026-10-08`；开工 HEAD `1849b4e6f15b97d277972e6ea930fe6c5c73aacb`，工作树干净。主 checkout 原有未跟踪嵌套 `XRmuseum/`，未触碰。`git fetch origin --prune` 成功；资产 base `origin/codex/helmet-refinement-assets` 为 `37428dff4307a31a042b8b3ed38e57d8856b502c`。

## 已检查

- 只读复核 [PR #37 意见](https://github.com/Twispra/XRmuseum/pull/37#issuecomment-6063779730)、Issue #25、厂家产品/规格页和 [2025 零件图](https://kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf)。厂家 17B 产品页直接展示两张所引照片；主图仅斜前视，侧阀特写未证实 500-010/011 的通信接口。零件图位置 13 注明壳体包含位置 1 visor 520-054。
- v2 参数 JSON 可解析；Blender v2 建模/读回脚本通过 Python 语法编译。压圈脚本 `front_y=-0.011`、`back_y=0.001`，其差值为 **0.012 m**，与 v2 JSON 相同；均为估算。脚本不创建 v1 的全环 `UpperVisor`；新外形/接头方向仍须在 Blender 中观察确认。
- 厂家版权图片未进入仓库；对照页只存 URL 与 v1 现有渲染链接。v1 `.blend`、GLB、贴图、预览及历史检查记录未修改。

## 阻碍及未验证

从 [Blender 官方下载站](https://download.blender.org/release/Blender4.5/)取得便携 4.5.14，用 `--version` 启动即退出，Windows CodeIntegrity Operational 事件 3077/3033 指向 `blender.shared/OpenEXR.dll` 未满足签名级别；进程退出码 `0xC0E90002`。未关闭或降低 Smart App Control/代码完整性策略，也未尝试规避拦截。

因此 **v2 `.blend`、GLB、预览及运行版回读结果尚不存在**；`scripts/check_faceport_exterior_v2.py` 未运行。本次仅提交资料与可编辑建模脚本，不声称外形校正已经生成或通过审阅。现有 v1 自动检查结果仍有效，但维护者指出的外观和参数缺陷仍存在于 v1 文件。没有 Unity 导入、主体装配、真实尺寸、VR、构建或用户验证。

## 最小后续动作

在能正常运行的可信 Blender 4.5.14 环境新建空输出目录，先用已有 `scripts/make_original_textures_v1.py` 在其中生成两张原创贴图，接着执行 v2 构建；人工检查面窗/侧阀模型与厂家同类角度照片、螺钉和 visor 归属；再执行 v2 回读、核对局部尺寸与 UV/材质/透明度，保存 v2 分件源、GLB、渲染及新 QA 结果。不要覆盖 v1；与未来主体作者协商正式比例/原点/安装曲面后再做装配版。
