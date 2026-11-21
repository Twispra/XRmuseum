# SuperLite 17B 黄色裸壳形状研究

2026-11-21 · [Issue #25](https://github.com/Twispra/XRmuseum/issues/25)。用户希望单独查看黄色玻璃钢壳。本次从空场景建立截面，交付文件中只有一个可编辑网格。可直接打开 [Blender 源文件](../../artifacts/helmet-modeling/superlite17b-bare-shell-2026-11-21/SuperLite17B-yellow-shell.blend)，也可看[斜前、侧、正前和底后四视图](../../artifacts/helmet-modeling/superlite17b-bare-shell-2026-11-21/preview.png)。

## 资料如何影响造型

| 已查看的资料 | 对应观察 | 在文件中的处理 |
| --- | --- | --- |
| [Kirby Morgan 17B 整盔分解图 #241108001，第 1–2 页](https://kirbymorgan.com/sites/default/files/2025-01/sl-17b-blowapart-241108001.pdf#page=2) | 第 13 号是 520-065 壳体。窗边的黄色宽台、侧下方成形的凸台、窗下有两层开口的下颌与壳体连在一起。面窗、压圈、配重、提手和颈部总成有各自编号。零件表说明供货的壳体包含 1 号遮阳件。 | 单独建玻璃钢本体、黄壳窗座和前下脸；外置件均未装入。 |
| [17B Handle and Weights #260519005，17BHDL-2～3](https://www.kirbymorgan.com/sites/default/files/2026-05/17b-handle-and-weights-260519005.pdf#page=2) | 第 2 页把配重拆离，显出宽拱形窗孔、下缘中央上抬、较厚的窗座肩和斜向颈口。第 3 页右下实拍中，取下后配重后，圆顶连续过渡至较直的下后壁，可见两个横向分布的小孔。 | 先塑后脑和眉部过渡，再收后侧肩和前颈缘；保留后壳孔，避免把金属配重边线刻进黄壳。第 2 页的独立裸件用作同机位轮廓对照。 |
| [17B Neck Clamp Area #260519003，17BNK-17](https://www.kirbymorgan.com/sites/default/files/2026-05/superlite-17b-neck-clamp-area-including-internal-chin-strap-and-yoke-260519003.pdf#page=17) | 底部实拍和文字可区分黄壳的环槽及独立 O 形圈 510-446。[厂家产品页](https://www.kirbymorgan.com/products/helmets/superlite-17b)也说明颈套和轭另成总成。 | 建一圈倾斜、连续的窄唇和浅槽，颈口通向头腔。 |
| [Bonds Nautical 裸壳实拍，SKU 2577](https://www.bonds-nautical-antiques.co.uk/product-page/kirby-morgan-diving-helmet-shell) | 同一件藏品提供偏上正前、底部及背部视角，可交叉观察宽窗、窗下收窄的下颌、较暗的内壁及椭圆颈口。卖家记录为 Superlite Mk17，具体子型号与通信配置未知。 | 用来复核宽高关系和内外壁辨识。孔径、衬件、磨损、底口局部缺口及藏品序号不作现售 17B 的复原依据。 |
| [厂版面窗模块 #260901001](https://www.kirbymorgan.com/sites/default/files/2026-09/face-port-port-retainer-nose-block-260901001.pdf#page=3)及已有两张整盔实物参考 | 裸窗座周围可见固定位置和密封圈座。整盔侧后照中，织带、黄铜配重和排气翼遮挡了一部分黄壳轮廓。 | 额顶与前窗之间留浅凹；周边设浅孔窝。图中 16 个位置的分布是外观估计，未建金属螺钉、螺纹或嵌件。 |

## 建模与检查

采用 `-Y` 朝前、`+Z` 向上。从手写的面窗边线、纵向截面和颈口断面过渡出连续壳壁。窗座与颈缘各有命名顶点组。下颌上、下两处分层开孔，另有侧壁孔和后部小孔。外壁胶衣和内壁的深暖灰色、较高粗糙度由同一材质的面角属性控制。所有具体尺寸、内腔厚度、嵌孔位置与局部曲率都是展示估算。本研究没有使用 V4、r3 或第三方网格作为输入。[可复建的截面和生成脚本](../../artifacts/helmet-modeling/superlite17b-bare-shell-2026-11-21/build_shell.py)

在 Windows / Blender 4.5.14 LTS 中重新打开最终文件。首稿在颈缘和眉部的内外壁曾相交，后续重新排布截面、沿表面法线建立窗后内壁，并在下颌曲面开孔前局部三角化。最终网格为单一连通体，检查到的非流形边、退化三角面和非相邻面自交均为零。三条从面窗穿入、经颈口离开的折线路径均畅通。[最终几何及材质回读](../../artifacts/helmet-interaction/qa/runs/20261121-issue25-bare-shell/geometry.json)

用厂家 17BHDL-2 第 2 页约 301 × 296 像素的裸件图，分别取外轮廓和净窗孔。在第一稿拟合正交机位、位置和缩放，定稿沿用此机位。外轮廓投影交并比由 **0.882** 提高到 **0.906**，窗孔由 **0.813** 提高到 **0.888**。定稿的双向平均边界距离分别是 **6.91 px、3.48 px**，最大偏差分别为 **19.42 px、9.22 px**。前下颊仍有局部差异。两圈数据均针对这一个插图视角；侧后被遮挡的形状、实物内壁和毫米尺度仍缺测绘依据。此前整盔侧照使用另一张图和遮挡定义，其指标单独保留。[轮廓线对照](../../artifacts/helmet-modeling/superlite17b-bare-shell-2026-11-21/outline-comparison.png) · [相机、掩码与测量报告](../../artifacts/helmet-interaction/qa/runs/20261121-issue25-bare-shell/outline-check.json)

## 复查与交接

原 checkout 开工为 `codex/superlite17b-unity-integration-2026-10-11@86715c9`，其中已有其他活动记录、本地模型候选、资源文档改动及未跟踪目录。上一轮面窗分支为 `c24ef7d`。本次独立分支 `codex/issue25-bare-shell-study` 基于 fetch 后的 `origin/main@e4a43dbe87c826118e81c5bbc77ff15003f8eeb3` 加本轮新增文件。最终裸壳 SHA-256 为 `09766af684372e1a47bc5ecb810e2d6f9d30cc54fca3f51912768e733575ce09`。厂家图片仅用于查看和测量，交付预览由本模型渲染。

在工作树根目录，以下命令可分别新建研究副本、检查保存文件、生成四视图。渲染排版使用系统 Python 和 Pillow。轮廓复测需从上表厂家 PDF 生成指定的第 2 页图，并使用含 NumPy、SciPy、Pillow 的 Python；命令和文件身份校验见 [outline_check.py](../../artifacts/helmet-modeling/superlite17b-bare-shell-2026-11-21/outline_check.py)。普通查看只需打开已保存的 Blender 文件。此版本仅供外形讨论，尚未整盔装配、导入 Unity 或进行实物对比测量。

```powershell
$dir = 'artifacts/helmet-modeling/superlite17b-bare-shell-2026-11-21'
$blender = 'D:/Blender4.5.14/blender-4.5.14-windows-x64/blender.exe'
$out = Join-Path 'tmp' ('shell-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $out -Force | Out-Null
& $blender -b --python-exit-code 1 -P "$dir/build_shell.py" -- "$out/rebuilt.blend"
& $blender -b --python-exit-code 1 -P "$dir/inspect_shell.py" -- --model "$dir/SuperLite17B-yellow-shell.blend" --report "$out/check.json" --preview "$out/preview.png"
```
