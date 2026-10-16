# XRmuseum

COMP5424 Extended Reality 博物馆**group2**小组作业的 Unity 工程与协作文档。

多人 / Codex 开工：先读 [AGENTS.md](AGENTS.md) → [当前状态与下一步](docs/project-state.md) → 对应 [Issue](https://github.com/Twispra/XRmuseum/issues) / PR。当前分支的功能不一定已合入 `main`；状态页区分主线、候选成果和仅本地改动。

## 打开工程

- Unity Editor：**2022.3.62f3c1**，所有成员使用相同完整版本。
- Unity 工程目录：**`MuseumVR/`**。在 Unity Hub 中选择这一层，不是仓库根目录。
- Git 仓库根目录：包含本文件、`.gitignore`、`docs/` 和 `MuseumVR/` 的目录。
- GitHub：https://github.com/Twispra/XRmuseum
- 目标：Windows PC VR，通过 SteamVR 或 Meta Quest Link 提供 OpenXR 运行时。

详细图形界面操作见 [组员首次打开与日常协作](docs/getting-started.zh-CN.md)。

打开后双击保存的 `Assets/Museum/Scenes/MuseumStart.unity`，新 clone / worktree 先确认 Git LFS 资产已展开。中央展项默认显示 Mark V 精细头盔，SuperLite 17B 精细模型也已接入，1839 早期档仍为占位。可拿放、开窗、剖面、查看三个热点与 AIR / VOICE 演示；提供物理手与双手协作、FLOAT 单手方式、简繁英、声音 / 减少动画、Help、展品 RESET 与 RESET VIEW。日常查看不运行原型生成器或模型安装器。

本轮 [Issue #57](https://github.com/Twispra/XRmuseum/issues/57) 修复 NEW VISIT 对探索与观察发现的完整清理，并补齐年代按钮 / 当前展签的三语说明；最终被测版本与检查结果见[技术说明](docs/phase2-technical-note.zh-CN.md)。NEW VISIT 保留语言、年代、声音 / 动画偏好、展品位姿及视角；头盔 RESET 与 RESET VIEW 分别恢复展品和观看位置，保留本轮发现。

快速操作见[年代切换](docs/helmet-eras.zh-CN.md)、[自由探索与复位](docs/helmet-exploration.zh-CN.md)、[物理手及设备反馈](docs/physical-hands.zh-CN.md)。2026-10-16 已有完整 Windows64 Development 构建与产物指纹，实际源码版本、目录和未启动边界见[构建记录](docs/activity/2026-10-16-issue50-shell-grab.md)。已有有限头显反馈；最新修订的完整头显流程、性能和用户研究仍需验证。

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
- [2026-09-23 头盔原型：实现、验证与贡献证据](docs/activity/2026-09-23-helmet-prototype.md)
- [外部资源与辅助工具记录](docs/external-resources.md)
- [初始化验证结果与尚未验证的范围](docs/initialization-checks.md)
- [课程依据与团队约定的区别](docs/course-requirements.md)
- [六人协作机制与工具保护记录](docs/activity/2026-09-23-team-collaboration.md)
