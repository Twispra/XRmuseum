# 2026-10-10：把 Windows 可运行构建的步骤与坑固化成文档

用户明确授权新增一份构建操作说明。本轮**没有运行 Unity**，只把 2026-10-10 一次实际构建中
踩到的确定结论写成可复用文档；那次构建的原始证据随另一份提交保存，本记录不重复引用它。

**提交前复核（2026-10-11）**：`main` 仍为 `ac3d31cb2d`，本页引用的
`docs/getting-started.zh-CN.md`、`MuseumVR/ProjectSettings/*` 均无新变更，故按原文提交。

## 为什么要写

`docs/getting-started.zh-CN.md` 第 7 节要求"提交阶段 2 前需要…提供可运行的 Windows 构建"，
但全文只记录了 **2026-09-22 那次初始化构建**的历史位置，**没有当前版本的可复现步骤**；
仓库里也没有任何地方记下批处理构建会改动 `ProjectSettings` 这件事。
每次有人要出构建都得从零摸索，且这个坑会让 `ProjectSettings.asset` 被误提交。

## 本页覆盖什么

新增 `docs/build-windows.zh-CN.md`：

1. 构建前核验（版本、构建场景、LFS 是否已展开）；
2. 两条路径的完整命令/点击步骤：批处理与 Unity GUI，并说明正式 VR 交付推荐 GUI；
3. 三个坑：
   - **`.bat` 必须 CRLF**——用 LF 会让 `cmd.exe` 解析错乱、退出码丢失；
   - **批处理会清空 XR 的 `preloadedAssets`**——本工程批处理构建中已出现两次，提交前必须 `git checkout --` 还原；
   - **产物不进 Git**（`Builds/` 已被忽略，且不能只发一个 exe）。
4. 构建后收证据的判据（退出码 0 + `error CS` 0 条 + 产物齐全）与命令；
5. 无头显时 `XR_ERROR_FORM_FACTOR_UNAVAILABLE` 属已知现象，不是构建缺陷；
6. 与其它文档的关系。

同时在第 5 节末给出与 `docs/getting-started.zh-CN.md` 的双向入口。

## 关于坑 B 的措辞（重要）

批处理清空 `preloadedAssets` 这件事，我们**先怀疑它会破坏播放器 XR 初始化**，
后来用播放器日志否证了：`UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json` 仍报
`1 displays + 1 inputs`，子系统照常装载。

因此文档里只写确定的部分——"**会造成 ProjectSettings 的意外改动，提交前必须还原**"，
并明确写出"是否影响播放器 OpenXR 初始化**未证实**"，不把它写成"会导致 VR 起不来"。
教训：凭机制推断下结论前先找运行证据。

## 未验证与限制

- 本轮未运行 Unity，因此文档中的命令**没有在本轮重新执行**；其内容来自 2026-10-10 的实测，
  以及 `MuseumVR/ProjectSettings/EditorBuildSettings.asset` 等资产的当前内容。
- 未在第二台机器或第二台头显上复核；未验证 GUI 构建在其它编辑器补丁版下的行为。
- 未涉及课程交付口径（哪些产物、何时交）——那属于教学团队安排，不在本页范围。

## 后续

若组里愿意，可另开任务把构建步骤做成脚本或接进 CI（当前仓库没有 CI）。
本记录不改产品资产，也不需要更新 `project-state`。
