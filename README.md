# XRmuseum

COMP5424 Extended Reality 博物馆小组作业的 Unity 工程与协作文档。

多人 / Codex 开工：先读 [AGENTS.md](AGENTS.md) → [当前状态与下一步](docs/project-state.md) → 对应 [Issue](https://github.com/Twispra/XRmuseum/issues) / PR。当前分支的功能不一定已合入 `main`；状态页区分主线、候选成果和仅本地改动。

## 打开工程

- Unity Editor：**2022.3.62f3c1**，所有成员使用相同完整版本。
- Unity 工程目录：**`MuseumVR/`**。在 Unity Hub 中选择这一层，不是仓库根目录。
- Git 仓库根目录：包含本文件、`.gitignore`、`docs/` 和 `MuseumVR/` 的目录。
- GitHub：https://github.com/Twispra/XRmuseum
- 目标：Windows PC VR，通过 SteamVR 或 Meta Quest Link 提供 OpenXR 运行时。

详细图形界面操作见 [组员首次打开与日常协作](docs/getting-started.zh-CN.md)。

打开后双击 `Assets/Museum/Scenes/MuseumStart.unity`。展厅包含中央可抓取的潜水头盔简模、两处占位展台、传送地面和官方 XR Origin。头盔支持拿放、碰撞与复位，使用方式见[头盔抓取原型](docs/helmet-interaction.zh-CN.md)。课程要求的完整博物馆体验与用户流程仍需继续开发。

已配置 URP 14.0.12、OpenXR 1.14.3、XR Interaction Toolkit 2.6.4、Input System 1.14.0。模拟器只在 Editor 中自动启用；接真实头显前请按操作说明关闭模拟器。

## 目录

```text
XRmuseum/
  MuseumVR/       Unity Hub 打开的工程
    Assets/       场景、脚本、美术资源及对应 .meta
    Packages/     package 版本与锁定文件
    ProjectSettings/
  docs/           协作说明、每周记录、外部资源记录
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
