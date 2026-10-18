# XRmuseum

COMP5424 Extended Reality 博物馆**group2**小组作业的 Unity 工程与协作文档。

## 小组成员与仓库访问

| 姓名 | 学号 | GitHub 账号 |
| --- | --- | --- |
| 蔡子申 | 26134128g | [r2kh8sck4y-bot](https://github.com/r2kh8sck4y-bot) |
| 何梓绎 | 26069396g | [Aragorn-cyber](https://github.com/Aragorn-cyber)、[heziyi-aragon](https://github.com/heziyi-aragon) |
| 叶福涛 | 26053268g | [Fttt1003](https://github.com/Fttt1003) |
| 张非凡 | 26108425g | [nikoyeah830-wq](https://github.com/nikoyeah830-wq) |
| 赵基蘅 | 26071012g | [Twispra](https://github.com/Twispra) |
| 赵祥睿 | 26044846g | [Ereshkigal1224](https://github.com/Ereshkigal1224) |

以上身份按六份个人作业文件名登记。**2026-10-18 已核查仓库可见性为 Public（公开）**，教学团队可通过 [Twispra/XRmuseum](https://github.com/Twispra/XRmuseum) 查看项目；Canvas 项目仓库文本栏应填写此仓库 URL。

多人 / Codex 开工：先读 [AGENTS.md](AGENTS.md) → [当前状态与下一步](docs/project-state.md) → 对应 [Issue](https://github.com/Twispra/XRmuseum/issues) / PR。当前分支的功能不一定已合入 `main`；状态页区分主线、候选成果和仅本地改动。

## 打开工程

- Unity Editor：**2022.3.62f3c1**，所有成员使用相同完整版本。
- Unity 工程目录：**`MuseumVR/`**。在 Unity Hub 中选择这一层，不是仓库根目录。
- Git 仓库根目录：包含本文件、`.gitignore`、`docs/` 和 `MuseumVR/` 的目录。
- GitHub：[Twispra/XRmuseum](https://github.com/Twispra/XRmuseum)
- 目标：Windows PC VR，通过 SteamVR 或 Meta Quest Link 提供 OpenXR 运行时。

详细图形界面操作见 [组员首次打开与日常协作](docs/getting-started.zh-CN.md)。

打开后双击保存的 `Assets/Museum/Scenes/MuseumStart.unity`，新 clone / worktree 先确认 Git LFS 资产已展开。中央展项默认显示 Mark V 精细头盔（索引 1）；1839 早期档的 1881-47 外观研究模型（索引 0，PR #69）与 SuperLite 17B 精细模型（索引 2）均已接入，三档使用不同几何。

当前默认使用 [PR #59](https://github.com/Twispra/XRmuseum/pull/59) 的原位旋转：按住头盔外壳拖动，模型中心固定，松手停止；前窗由玻璃本身触发，剖面使用展台固定滑轨，保留热点与 AIR / VOICE 演示。提供物理手、简繁英、声音 / 减少动画、视角高度、展品 RESET 与 RESET VIEW。[PR #65](https://github.com/Twispra/XRmuseum/pull/65) 加入可隐藏及唤出的操作面板，唤出时朝向用户，之后固定于空间。开发时停用 `HelmetDiegeticStation` 可恢复旧自由拿放、双手协作及 FLOAT 入口。日常查看不运行原型生成器或模型安装器。

较早的 [Issue #57](https://github.com/Twispra/XRmuseum/issues/57) 修复 NEW VISIT 对探索与观察发现的完整清理，并补齐年代按钮 / 当前展签的三语说明；当轮被测版本与检查结果见[技术说明](docs/phase2-technical-note.zh-CN.md)。NEW VISIT 保留语言、年代、声音 / 动画偏好、展品位姿及视角；头盔 RESET 与 RESET VIEW 分别恢复展品和观看位置，保留本轮发现。NEW VISIT 等旧探索入口属于保留流程，当前实体 UI 默认界面不再显示它们。

[PR #67](https://github.com/Twispra/XRmuseum/pull/67) 接入左展台的 1988 照片外形研究：三语展签与左转／复位／右转按钮，原位松手确认、移开取消，独立于中央三年代。模型身份、尺寸及细节仍未核实，没有开窗、剖切或供气／通话动作；[使用说明](docs/photo-helmet-unity-study.zh-CN.md)与[复审证据](docs/activity/2026-10-18-pr67-review-and-merge.md)。右展台及 #64 的完整双台方案仍待后续评审。

快速操作见[实体 UI](docs/diegetic-ui.zh-CN.md)、[操作面板](docs/operator-panel.zh-CN.md)和[年代切换](docs/helmet-eras.zh-CN.md)；旧流程与设备反馈见[自由探索与复位](docs/helmet-exploration.zh-CN.md)、[物理手及设备反馈](docs/physical-hands.zh-CN.md)。2026-10-18 已基于最新原型 `6088c736` 完成 Windows x64 Development/Mono 构建：0 errors、2 条既有 OpenXR 可选警告，Editor 自然退出 0，1,299 个源文件前后零差；完整提交 ZIP 共 274 文件、74,726,292 bytes，CRC 及解压哈希一致。解压版完成 40 秒本机启动观察，但无可用头显，观察后主动终止测试进程；不能据此称 Player 自然退出、VR 交互或 PQ604 验收通过。[本次构建、ZIP 与限制](docs/activity/2026-10-18-phase2-windows-submission.md)。2026-10-16 的 `806993a` 构建保留为[历史记录](docs/activity/2026-10-16-visitor-flow.md)。当前 Editor 验证见[左台接入复审](docs/activity/2026-10-18-pr67-review-and-merge.md)；真实头显完整流程、PQ604、性能和用户研究仍待验证。

已配置 URP 14.0.12、OpenXR 1.14.3、XR Interaction Toolkit 2.6.4、Input System 1.14.0。模拟器仅供 Editor 预览；真实头显与模拟输入的操作边界见[协作指南](docs/getting-started.zh-CN.md)。

## 目录

```text
XRmuseum/
  MuseumVR/       Unity Hub 打开的工程
    Assets/       场景、脚本、美术资源及对应 .meta
    Packages/     package 版本与锁定文件
    ProjectSettings/
  docs/           协作说明、每周记录、外部资源记录
  artifacts/      分模块的验证证据与交接材料
  .github/        Issue / PR 模板等仓库配置
  AGENTS.md       协作约定与协作工具说明
  .gitattributes  文本文件和 Git LFS 规则
  .gitignore      排除缓存、构建产物和本地文件
```

## 贡献与归属

这是项目基础环境。Unity 官方模板、包示例以及 Codex 辅助创建的初始化内容，应与成员后续原创工作区分。
每位成员使用自己的 GitHub 账号及已关联邮箱提交；不要把共享账号或初始化提交作为个人独立贡献证据。

- [每周活动记录](docs/weekly-activity.md)
- [Phase 2 小组活动记录（提交用 PDF）](phase2小组和个人作业/COMP5424-Group2-Phase2-ActivityRecord.pdf)
- [Phase 2 原型设计与技术说明（提交 PDF）](artifacts/technical-notes/20261018-concise/phase2-prototype-design-and-technical-note.en.pdf) / [可维护文稿](docs/phase2-prototype-design-and-technical-note.en.md)
- [Phase 2 最终材料核查与提交清单](docs/activity/2026-10-18-phase2-final-submission-audit.md)
- [2026-09-23 头盔原型：实现、验证与贡献证据](docs/activity/2026-09-23-helmet-prototype.md)
- [外部资源与辅助工具记录](docs/external-resources.md)
- [初始化验证结果与尚未验证的范围](docs/initialization-checks.md)
- [课程依据与团队约定的区别](docs/course-requirements.md)
- [六人协作机制与工具保护记录](docs/activity/2026-09-23-team-collaboration.md)
