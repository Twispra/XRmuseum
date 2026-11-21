# 2026-11-21 导航与旁白三语整合

合并候选 `e2c7fbdf1bd8900b6a4b4b4340537a096a74f27b` 的两条父线为 [#88](https://github.com/Twispra/XRmuseum/pull/88) 的 `b2c27bb2` 与其余五项已审 PR 的汇总 `c61c130d`。在独立工作树合并，解决三语表、字符清单、静态图集和资源说明的冲突。

保留三语源表原有 295 条、导航 8 条、字幕字号 2 条、关闭字幕 1 条，以及 [#91](https://github.com/Twispra/XRmuseum/pull/91) 对停止旁白的用途说明。JSON 与保存的 Unity 表共 306 条，独立复核新增或修改的 12 条值一致。两个分支的资源说明均保留。

- 用仓库 `make-charlist.py` 重新生成 635 个显示字符。用 Unity 2022.3.62f3c1、Windows、Direct3D 11 的 `TrilingualFontBaker.RunBatch` 在保存字体资产上烘焙，保留原 649 字形并补齐「台、央、往、幕」，共 653 个保存字形。
- 图集保留 4096²、110 采样、Padding 6 与 SDFAA。核对保存像素与烘焙缓冲 SHA-256 `84c989ef3ddf534766df7610b96bb4955f73ad55743fddcec74493bdae2246a3`，四个新增字形都有实际像素。源 TTF、字体／图集／材质 GUID 与 local file ID、原 meta 均已核对保留。
- 烘焙进程自然退出 0。43 项工程 LFS 文件按实体字节数和 SHA-256 核验。烘焙前后比较 1,464 个工程文件，只有受授权的 SDF 资产变化。
- 在干净的 `e2c7fbd` 中运行 `Museum.Localization.EditorTools.LocalizationValidation.RunBatch`，11/11 项通过，0 项失败，306 条保存文案，进程自然退出 0。退出后 1,464 个工程文件的 SHA-256 零差。首轮校验报告同样通过，辅助脚本误读了失败数字段，复跑补齐进程记录。

此记录覆盖保存三语表与图集。联合 Play Mode 验收由整合人完成。真实头显、性能、Windows Player 构建与用户研究尚未验证。
