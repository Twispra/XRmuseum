# 2026-10-05 中英语言切换模块 —— 接入主场景

本轮把[中英语言切换模块](2026-10-04-localization.md)接进 `MuseumStart.unity`，
对应提案里用户旅程的**第一步** `Welcome and language choice`。

## 为什么单独一轮、单独一个 PR

`MuseumStart.unity` 是全组共享、需要**串行写入**的文件（AGENTS 协作规则）。
因此刻意拆成两个 PR，而不是把接入混进模块那一个：

- **PR #13**：模块本身，**纯新增**，不碰任何既有文件 → 可以被独立审查与合并
- **本轮 PR #14**：只改 `MuseumStart.unity` + 新增接入工具 → diff 小、范围清晰

如果把两者混在一起，一旦有人正在改主场景，**连不带风险的模块也会被一起卡住**。

## 接入了什么

| 动作 | 对象 | 说明 |
| --- | --- | --- |
| **新增**（唯一的"增"） | 根物体 `Language Selection` @ `(2.6, 0, 6.2)` | 展示 Prefab 的实例，面向 -Z 正对参观者来向 |
| **只挂组件** | `Museum Title` | 原本 `MUSEUM / XR` → key `museum.sign.title` |
| **只挂组件** | `Museum Subtitle` | 原本 `A SPACE TO EXPLORE` → key `museum.sign.subtitle` |
| **只挂组件** | `Welcome` | 原本 `Welcome. Take a closer look.` → key `museum.sign.welcome` |

给既有物体**只挂 `LocalizedText` 组件**，不动它们的名字、位置、旋转、缩放与文字内容。
三条新文案的英文是**逐字照抄**场景原文的 —— 否则挂上组件的瞬间就等于改掉了展陈文案。

### 位置怎么定的

用脚本解析场景 YAML 读出来的实际坐标（不是猜的）：

```
XR Origin (XR Rig)   (0, 0, 2.2)   朝向 +Z      ← 参观者出生点（相机在它内部）
Welcome Panel        (0, 2.25, 8.82)            ← 入口标识墙，正前方 6.6 米
HelmetExhibit        (0, 1.17, 3.5)
```

所以语言面板放在**右前方 4 米** `(2.6, 0, 6.2)`：出生后一眼看得到，又不挡住居中的馆名。
觉得位置不合适，接入后在 Hierarchy 里直接拖 `Language Selection` 即可。

## 安全性设计（因为改的是共享文件）

| 风险 | 应对 |
| --- | --- |
| 改坏主场景 | 执行前**自动备份**到仓库外的 `<repo>/_local-backup/MuseumStart-<时间戳>.unity`（放 Assets 外，不被 Unity 导入，也不进提交） |
| 和别人撞车 | **只增不改**，唯一新增是一个根物体，冲突面最小 |
| 改错想回退 | 一键撤销菜单 `Remove From MuseumStart` |
| 误删别人的组件 | 撤销时**只移除自己加的那几个 key 的组件**，别人挂的保留 |
| Play 模式下产物半成品 | 处于 Play 模式**直接拒绝执行**并提示 |

找不到物体 / 物体上没有 TMP 组件 / 已被别人绑定到别的 key → **都只警告，不覆盖**。

## 默认语言：改成英文

接入时发现语言通道的初始语言是中文，而 `LocalizedText.Configure()` 会立刻按当前语言写一次，
于是那三个标识当场变成中文。问题是**展厅其余 37 处文字仍是英文**，
默认外观就成了"半中半英" —— 这是**共享的主场景**，默认外观不该被单方面改掉。

因此：

- `LanguageChannel` 的字段默认值改为 **English**
- `LocalizationSetup` 新增常量 `DefaultMuseumLanguage = English`，
  并在生成通道时**把已存在资产也拉回该默认值**（通道是生成资产，初始语言属于代码级决定，
  这样"重新执行菜单"的结果可预期，不会留下没人记得的手动状态）
- 效果：**接入后展厅默认外观与接入之前完全一致**，访客主动选择中文才切换

等全馆文案补齐中文之后，把 `DefaultMuseumLanguage` 改成 `Chinese` 更合适。

## 验证与证据

- 被测 commit：本 PR 的 HEAD
- 环境与步骤：Unity 2022.3.62f3c1，Windows Editor / Direct3D 11
  1. `Museum → Localization → Create Everything and Demo Scene`
  2. `Museum → Localization → Integrate Into MuseumStart`
  3. 打开 `MuseumStart.unity` 按 ▶
- 结果：**展厅默认外观与接入前一致（英文）**；按 `1` 或点面板上的「中文」按钮后，
  入口三处标识（`MUSEUM / XR`、`A SPACE TO EXPLORE`、`Welcome. Take a closer look.`）
  切换为中文（`博物馆 / XR`、`可以随意观看`、`欢迎。请随意观看。`），语言面板同步切换
  （`2` 切回英文，`L` 循环）
- **这是 Unity 单眼渲染 + 键盘/指针输入的编辑器内检查，不是头显实测**
- 已核对文件（不只是看界面）：主场景 diff **+136 / −9 行**；
  `PrefabInstance` 数 3 → 4（正好新增一个）；场景引用 `LocalizedText` 脚本**恰好 3 次**；
  三个 key 均出现在场景中；文案表 20 条（原 17 + 新 3）
- 未验证：真实头显上的可读性、射线交互本身、射线按钮的实际抓取确认；
  未做无障碍或参与者评价；未在 PQ604A/B/C 构建

## 三条新文案

| key | 中文 | 英文（= 场景原文） |
| --- | --- | --- |
| `museum.sign.title` | 博物馆 / XR | MUSEUM / XR |
| `museum.sign.subtitle` | 可以随意观看 | A SPACE TO EXPLORE |
| `museum.sign.welcome` | 欢迎。请随意观看。 | Welcome. Take a closer look. |

**中文刻意只用当前字体图集里已烤入的汉字**（脚本核对过，三条全部命中），
所以这次接入**不需要重新生成字体资产**。这是刻意的取舍：文案自由度换"不再折腾字体"。
以后要写更自然的中文，再走一次「更新字符清单 → 重烤字体」即可（步骤见模块说明）。

## 已知限制

- 只本地化了入口三处标识。展厅其余 37 处文字仍是英文，**不是本模块的完成状态**，
  需要各模块负责人把文案搬进文案表并给文字对象挂 `LocalizedText`。
- 中文译文为**起步文案**，且受当前字体子集限制，不是最终展陈文案。
- 语言选择面板的位置是脚本给的默认值，**具体摆放应由整合人按展厅动线确认**。
- 本 PR 基于 **PR #13 的分支**；PR #13 合并后 GitHub 会把本 PR 的基准自动切到 `main`。
