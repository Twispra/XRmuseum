# 中文字体来源、保存资产与维护

2026-10-06 本地体验细节分支在下述三语版本上补充声音按钮：源 TTF 为 7652 码位，保存图集为 510 字符（当前文案清单 500 字符），原 GUID、材质和图集引用保留；4096²／110／Padding 6 参数不变。具体来源、像素校验与验收见 [本轮交接](../../../../../../docs/activity/2026-10-06-helmet-experience-polish.md)。下文 7651／507／497 数字为此前 PR #18 的版本记录。

## 当前交付

- `NotoSansSC-MuseumSubset.ttf`：约 2.25 MiB，Regular 静态子集，本轮扩充繁体所需码位。
- `NotoSansSC-MuseumSubset SDF.asset`：已保存、可直接运行的静态 TMP 字体；**507 个字符，110 点采样，Padding 6，4096×4096 单张 Alpha8 图集，SDFAA**。
- `characters-required.txt`：497 个显示字符，按三语文案、固定标签、ASCII 与 TMP 特殊字符生成。
- `make-charlist.py`：按脚本自身路径定位三语 JSON，成员不需要修改作者机器路径。

日常打开工程、预览或验收**不重建字体**。直接加载保存资产，再运行 LocalizationValidation；主场景验收另用 LocalizationIntegrationValidation。

## 当前三语版本：2026-10-06

当前源 TTF 保留原子集码位并补充三语文案所需字形：7651 个 cmap 码位。保存的静态 SDF 为 4096×4096、110 / Padding 6 / SDFAA，507 个字符；三语显示字符清单当前为 497。原字体 GUID、材质与图集 localFileID 不变。字体底层打包后逐字形检查像素，并核对保存图集字节与缓冲哈希。

文案以 `../Tables/MuseumTranslations.json` 为准；make-charlist.py 已改为读取三语 JSON，不再读取旧 Setup 种子。新增缺字应明确执行字体更新任务；只在图集已有字形时不需要重烤。使用 TrilingualFontBaker / TrilingualMuseumSetup 时必须退出 Play、保存或关闭脏场景；先备份，在协调好的 checkout 操作，不能日常打开就运行。

当前源字体扩展由 fontTools 在本机完整 NotoSansSC-VF.ttf 的 wght=400 实例上完成，保留原 cmap 及新增字符。该修改版本继续沿用 NotoSansSC 名称，未使用 OFL 保留字体名 Source。完整许可不变，见 docs/licenses/noto-sans-sc-OFL.txt。OpenCC 辅助简转繁，文案和含义仍需人工复核。

下方 198 字符 / 2048² 是本日较早的入口版本记录；手动 Creator 更新本轮字体时应使用 **4096²**，其他参数不变。旧子集脚本含作者机器路径，仅保留来源流程。

## 历史入口补齐：2026-10-06

入口新文案需要「海、之、潜、水、厅、多、种、法」。本轮在原 190 字符基础上重烤为 198，沿用提交的源字体和原参数。使用 TMP 3.0.7 Font Asset Creator 的底层 FontEngine 加载、打包、字节缓冲渲染流程；在写入前检查字形像素，写入后核对磁盘图集字节与渲染缓冲相同。

原字体 GUID、图集与材质 localFileID 均保留，没有删除重建 .meta。此操作只针对已授权的字形补齐，不把一次性辅助脚本变成日常打开步骤。报告、生成脚本及诊断图由 [整合活动记录](../../../../../../docs/activity/2026-10-06-pr14-completion.md) 链接。

早期 `TMP_FontAsset.CreateFontAsset` / `TryAddCharacters` 高层路径曾产生 0×0 图集；Creator 的 Unicode Range (Hex) 也曾抛 FormatException。这些是当时失败路径，不能据此要求所有成员重建当前保存字体。当前已验证底层打包/渲染可用，Font Asset Creator 的 Characters from File 仍可用于后续明确的字体更新任务。

## 文案改变后

1. 同步三语 JSON 与保存的 LocalizationTable。
2. 用 Python 执行本目录 `make-charlist.py`；核对字符差集。
3. 在隔离 checkout、退出 Play 后重烤所需字符，保留既有字体及子资源引用。不得删除字体或重建 .meta 来绕过问题。
4. 核对保存图集的尺寸与真实像素、字符覆盖、原引用，并运行模块和受影响场景验收。

手工 Creator 参数：Source Font 为本目录提交的 subset TTF；Custom Size 110、Padding 6、2048×2048、SDFAA、Characters from File 指向 characters-required.txt。更新前备份，保存后核对原 GUID/localFileID，不能只看内存里 HasCharacter 成功或图集尺寸存在。新增字符不一定仍为 198，以本次字符清单和实际报告为准。

## 源字体、子集与许可

原始来源是本机安装的 Noto Sans SC 变量字体，**SIL Open Font License 1.1**。`subset-noto-sans-sc.py` 的原始流程在 wght 轴取 Regular (400)，再保留 ASCII、GB2312 汉字/符号与常用标点，约 7545 码位。原约 16.95 MB 的变量字体不提交；当前提交副本属于经静态实例化和子集化的修改版本。TTF 内嵌 Adobe 2014–2021 版权、保留字体名 `Source` 和许可摘要；完整 [OFL 文件](../../../../../../docs/licenses/noto-sans-sc-OFL.txt)于 2026-10-06 从 Google Fonts 的 Noto Sans SC 官方目录补入，其版权行与 TTF 一致。资源声明见 docs/external-resources.md。

源 TTF 的码位覆盖与 TMP 已烤入图集的 198 字符是两件事：即使 TTF 有该字，也必须确认静态图集已烤入。没有 GB18030 扩展字、日文或韩文完整支持的承诺。旧 check-glyph-coverage.py / subset-noto-sans-sc.py 保留原始来源流程，含作者环境路径，不作为当前保存资产验收入口。
