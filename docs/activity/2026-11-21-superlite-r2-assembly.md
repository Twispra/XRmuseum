# SuperLite 17B · R2 黄壳整盔装配

2026-11-21，关联 [Issue #25 三个时代精细模型合作](https://github.com/Twispra/XRmuseum/issues/25)。用户在[黄壳 R2 审阅](2026-11-21-superlite-shell-render-review.md)后认可右列新版，并授权组装完整头盔。本次交付供外观审阅的 500-010 双通信柱配置。可直接打开[完整 Blender 源文件](../../artifacts/helmet-modeling/superlite17b-r2-assembly-2026-11-21/SuperLite17B-R2-assembled.blend)、[独立 GLB](../../artifacts/helmet-modeling/superlite17b-r2-assembly-2026-11-21/SuperLite17B-R2-assembled.glb)或[四视图](../../artifacts/helmet-modeling/superlite17b-r2-assembly-2026-11-21/SuperLite17B-assembly-views.png)。

## 配置与资料

厂家产品页将 500-010 列为带通信柱的 17B，500-011 配备多线水密接头。本次以厂版整盔分解图 **#241108001** 的部件表和插图确定分件。用户先前提供的四张整盔渲染与转台用于外形比较。它们没有给出具体子型号、测绘尺寸或安装公差。[Kirby Morgan 产品页](https://www.kirbymorgan.com/products/helmets/superlite-17b) · [厂家整盔分解图，第 1～2 页](https://www.kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf)

| 模块 | 装配内容和依据 |
| --- | --- |
| 面窗与提手 | 520-004 透明曲面镜片、510-260 密封圈与 560-070 压圈按保存的 R2 窗缘贴合。11 枚压圈螺钉中，上方 3 枚同时固定 560-014 黄铜提手；另有 4 枚排气翼紧固螺钉、4 枚 550-061 隔套、两片肾形压片及鼻阻件。提手后端定位到壳顶。见 [FCPRT-2～3](https://www.kirbymorgan.com/sites/default/files/2026-09/face-port-port-retainer-nose-block-260901001.pdf#page=2)、[17BHDL-1～2](https://www.kirbymorgan.com/sites/default/files/2026-05/17b-handle-and-weights-260519005.pdf)。 |
| 供气与排气 | 镀铬侧阀总成 505-024 包括稳流、应急控制与单向阀。555-155 弯管连接侧阀出口与 SuperFlow 调节器 505-027 的进气端。两侧中空排气翼连接 510-760 总腔并由面窗下缘的肾形压片固定。两个蓝件是陈列状态下的防尘帽。见 [SB 侧阀模块](https://www.kirbymorgan.com/sites/default/files/2026-07/brass-chromed-brass-side-block-260701004.pdf)、[BNT 弯管](https://www.kirbymorgan.com/sites/default/files/2024-09/bent-tube-240906001.pdf)、[SF-12～14](https://kirbymorgan.com/sites/default/files/2024-07/superflow-regulator.pdf#page=12)、[QUAD 排气模块](https://www.kirbymorgan.com/sites/default/files/2025-01/quad-valve-and-tri-valve-exhaust-250114005.pdf)。 |
| 配重与颈部 | 连续后配重及两件异形侧配重沿实际壳面成形；后部凸缘套筒接后铰片，后铰销横穿轭铰。金属 505-055 颈夹和聚氨酯 520-117 U 形轭分别建模，底部可见软颈套的收口、下颏带与轭保留带。见 [17BHDL-2～3](https://www.kirbymorgan.com/sites/default/files/2026-05/17b-handle-and-weights-260519005.pdf#page=2)、[505-008 颈夹和轭分解图 #250918001](https://www.kirbymorgan.com/sites/default/files/2025-09/p-n-505-008-sl-17a-b-neck-clamp-yoke-assembly-250918001.pdf)、[17BNK-6 后铰](https://www.kirbymorgan.com/sites/default/files/2026-05/superlite-17b-neck-clamp-area-including-internal-chin-strap-and-yoke-260519003.pdf#page=6)。 |
| 通信与内装 | 佩戴者右侧前下颊设两根 515-035 通信柱和中间封堵，内侧接双耳机与麦克风。头垫按两侧耳垫、顶垫和后颈卷分件。窗内另有除雾管、鼻阻杆及 510-724 低容积口鼻罩。通信柱区域与走线按厂家图定位，具体间距和隐藏线路为展示估算。见 [17COM-2、10～11](https://www.kirbymorgan.com/sites/default/files/2025-01/communications-on-superLite-17b-and-17c-helmets-kmb-18-and-28-bandmasks-250114001.pdf#page=11)、[17BHC-1](https://www.kirbymorgan.com/sites/default/files/2024-07/17b-head-cushion.pdf)、[口鼻罩变更公告 #10/2024](https://www.kirbymorgan.com/sites/default/files/2024-11/Bulletin-10-2024.pdf)。 |

本次采用资料图及参考外观对应的镀铬黄铜弯管。厂家在 2026-06-05 公告逐步切换为不锈钢件 555-079／555-080，因而此模型展示的是有文献依据的一种配置。铭牌和按钮上的字样为本项目重新排版，仅使用型号与产品名称。[变更公告 #5/2026](https://www.kirbymorgan.com/sites/default/files/2026-06/Bulletin-5-2026.pdf)

## 保存文件验证

- **Blender 4.5.14 LTS / Windows。** 直接重开完整源和获批裸壳，逐项对比壳体几何、面连接、UV、内外壁属性、材质、顶点组及世界变换，指纹一致。整盔共 172 个可编辑网格、261,156 个求值后三角面。逐件检查未检出非流形、退化或非相邻面自交；两张原创标记纹理已打包。[综合回读](../../artifacts/helmet-interaction/qa/runs/20261121-issue25-r2-assembled/assembly-readback.json)
- **接口。** 首轮发现弯管局部被橡胶翼遮挡、阀件与窗框互穿，以及除雾管进气脚碰镜片。重新定位侧阀、调节器横向接口及进气脚，依照肾片背面重建翼片上缘。最终检查了 8,280 个指定范围内的唯一对象对。弯管两端接合，57 个横截面从阀侧前斜观察均可见，至排气翼的近似最小净距为 13.01 mm（模型值）。金属压片正面可见，两翼接合对应的法兰。九项接口检查通过。[接口及测量边界](../../artifacts/helmet-interaction/qa/runs/20261121-issue25-r2-assembled/interfaces.json)
- **GLB。** 导出后在空场景重新导入，172 个名称、逐件位置和包围盒、261,156 个三角面、UV 均匹配。按面检查 R2 黄胶衣与深色内壁的分区，镜片保留透射与折射率，两张嵌入纹理逐像素匹配。三款织物／橡胶的程序微凹凸保存在 Blender 源中。[导出回读](../../artifacts/helmet-interaction/qa/runs/20261121-issue25-r2-assembled/glb-readback.json)

真实零件的身份与连接依据如上；局部外形、密封压缩量、侧配重背部螺纹、隐藏阀芯与安装公差均未做工程核验。此轮交付独立展示资产，Unity 场景、交互、性能、实机和新构建留待单独接入任务。

## 版本与交接

原主 checkout 为 `86715c9`，含已有资源登记、本地试验、QA 和未跟踪目录。本次实际建模在独立工作树，以已批准 R2 源提交 `8f6f5ae` 与当时主线 `e4a43db` 为上下文，加本轮研究文件验证。裸壳 SHA-256 为 `800f5175684cf9a31f83344bb03c16af2a079f7e77391a27ca1b17bdbccf777e`；整盔为 `78b865dff91b116138fe0b6ae9bf7df43cd75422c43997d3261e04e0a1b793f1`；GLB 为 `8ff44c9bb5d02515e2d4ff554cd8741fc14c0292f2890e4175f088ef195e59d9`。本次按 #25 的独立资产流程提交审阅。用户批准的是 R2 黄壳，整盔尚待外观审阅。

普通查看可直接打开保存源。需复查时，令 `$d` 为本次资产目录、`$b` 为 Blender 4.5.14 可执行文件、`$shell` 为上一版的 R2 裸壳路径、`$out` 为一个新建的空输出目录，然后运行：

```powershell
$model = "$d/SuperLite17B-R2-assembled.blend"
& $b -b --python-exit-code 2 -P "$d/check_assembly.py" -- --shell $shell --model $model --report "$out/geometry.json"
& $b -b --python-exit-code 2 -P "$d/check_interfaces.py" -- --model $model --report "$out/interfaces.json"
& $b -b --python-exit-code 2 -P "$d/export_review.py" -- --model $model --output "$out/check.glb" --report "$out/export.json"
```

发布整理以资产分支基准 `39e895f9c5bd449cccd8c02c41fdb4f2c2eb423b` 为父版本，从本地建模提交 `b13e838`、`8f6f5ae`、`51d3fb5` 收入本次裸壳、R2 与整盔的独立资产和证据。仓库内的相对链接可在本 PR 分支直接打开。提交目标为 `codex/helmet-refinement-assets`，本次不替换主线 Unity 展品。
