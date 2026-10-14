# 组员首次打开与日常协作

先看[项目状态](project-state.md)，确认所需功能已经合入主线还是仍在 PR。PR #1 已于 2026-09-23 合入 main（`545229e`），以下头盔操作适用于该版本及后续版本；9 月 22 日初始化提交 `cc7605e` 只有抓取方块。

## 1. 为什么工程放在 MuseumVR 子目录

`E:\XRmuseum` 是整项作业的 Git 仓库根目录。Unity 工程放在 `E:\XRmuseum\MuseumVR`，这样报告、课程资料、每周记录不会进入 Unity 的 Assets 导入流程。
其他成员的盘符和父路径可以不同，但仓库里的 `MuseumVR` 相对目录应保持一致。

## 2. 六位成员统一环境

1. 安装 Unity Hub，并使用自己的 Unity 账号完成许可登录。
2. 使用 **2022.3.62f3c1**。这是本次获确认的本机版本，带 `c1` 后缀；不要直接换成 Unity 6、其他补丁版或不带该后缀的版本。
3. 目标平台是 Windows PC VR。通过 Meta Quest Link 使用 Quest 时，本阶段按 Windows 工程处理；只有决定制作 Quest 独立 APK 时才需要另行配置 Android。
4. 安装 GitHub Desktop，登录自己的 GitHub 账号。GitHub Desktop 自带 Git LFS。
5. 在 GitHub Desktop 的 **File > Options > Git** 中填写自己的名字和已关联 GitHub 的邮箱（或自己的 GitHub noreply 邮箱）。不能照抄其他成员身份。

## 3. 接受邀请与克隆

1. 仓库所有者在 GitHub 的 **Settings > Collaborators > Add people** 逐个邀请成员。
2. 成员登录自己的 GitHub 账号，接受邀请邮件或 GitHub 中的邀请。
3. 在 GitHub Desktop 中选择 **File > Clone repository**。
4. 选择 `Twispra/XRmuseum`；也可以在 URL 标签页输入 `https://github.com/Twispra/XRmuseum.git`。
5. 选择一个尚未存在或为空的本地目标目录，例如 `D:\CourseProjects\XRmuseum`。不要选其他已有 Unity 工程的目录。
6. 点击 **Clone**，等待普通文件与 LFS 文件下载完毕。

创建 Codex worktree 后也要检查 LFS：有的工作副本最初仅含几百字节的指针。打开 Unity 前在该仓库运行 `git lfs pull`（下载缺少的对象）和 `git lfs checkout`（展开已有对象）；确认模型/音频是真实文件。`git lfs ls-files` 的 `*` 表示工作区有完整对象，`-` 表示指针；`git lfs fsck` 不能代替此检查。曾有工作副本物理拿放通过、模型却因指针无法导入，故验收现在额外检查有效可见网格。

不要用 Download ZIP 作为团队开发工作副本，因为 ZIP 不带 Git 历史和远程配置。

## 4. 在 Unity Hub 中打开

1. 进入 **Projects**。
2. 当前这台电脑的 Hub 3.3.6 使用 **Open**；其他 Hub 版本可能显示 **Add > Add project from disk**。
3. 选择克隆目录下的 **MuseumVR**。这一层应能看到 `Assets`、`Packages`、`ProjectSettings` 三个目录。
4. 确认编辑器版本是 **2022.3.62f3c1** 后打开。
5. 首次打开需要导入资源和解析 packages；各成员会在本机生成 `Library`，无需别人发送这个文件夹。
6. 等待导入结束后查看 Console。遇到错误先解决，再开始开发或提交。

使用 GitHub 协作时，在 Hub 新建流程中无需再创建 Unity Version Control/Tuanjie Cloud 仓库。

### 打开起始场景并预览

1. 在 Project 面板依次打开 **Assets > Museum > Scenes**，双击 **MuseumStart**。
2. 点击顶部三角形 **Play**。先点击 **Game** 画面，使键盘输入进入游戏窗口。
3. Editor 中没有真实头显或运行中的 XR 显示时，工程自动创建官方 **XR Device Simulator**。连接真实设备时停止模拟器，保留头显的位置和旋转输入；断开设备后需退出再进入 Play 才恢复桌面模拟。按 **Tab** 在头部与左右手之间切换，按 **U** 切换身体操作；以画面上的模拟器提示为准。
4. **W/A/S/D** 控制当前设备的移动，**Q/E** 调整高度；按住鼠标右键操作头部视角。按 **T/Y** 切换左/右控制器操作，**G** 模拟握持键，鼠标左键模拟扳机。为避免误按空格时突然切换操作对象，本项目将模拟器的“按住操控右手”从 **空格** 改为 **右 Shift**；日常操作右手仍推荐按 **Y** 切换。
5. 中间展台已替换为带有 **XR Grab Interactable** 的头盔简模：靠近并按住 **G** 抓取，松开后放下，按 **Home** 复位。详细说明见[头盔抓取原型](helmet-interaction.zh-CN.md)。地面带有 **Teleportation Area**，使用 XRI 的 Teleport 交互层。模拟器的 **1/3** 在摇杆输入和位置输入目标之间切换，完整映射见导入的 XR Device Simulator inputactions。
6. 再次点击 Play 退出。**Play 期间对场景的临时改动不会自动保存到编辑状态。**

**头盔实体按钮怎么点：** 射线对准 WINDOW / SECTION / FLOAT / RESET 等按钮，按 **G 并松开**。这些按钮响应 Grip → Select；鼠标左键模拟 Trigger，不会触发它们。先点击 Game、切英文输入，并确认模拟器当前选中左/右 Controller，而不是 HMD。

**语言预览：** 主场景三语版本用 **F6 简体、F7 繁体、F8 英文，L 循环切换**，也可射线选择入口的语言按钮。1／2 不再作为主场景语言快捷键。开发新面板或字幕时，按 [三语文字接入说明](localization-authoring.zh-CN.md) 同步文案、字体和验收。

如果移动方向突然变化，先松开移动键，按 **U** 回到身体移动模式，再按 **Y** 选右手；按 **Esc** 可停止当前模拟器操控。测试时保持 Game 窗口焦点，尽量不要在按住移动键时切换窗口。空格现在没有模拟器动作，不用它来跳跃或抓取。

**视角卡住时：** 在个人操作面板顶部选择 **Reset View / 重置视角 / 重設視角**，或先点击 Game 画面再按 **F11**。这会把访客相机带回展厅初始观看位置并把操作面板重新放到视野内；它不重置头盔、年代或探索记录。面板原有的 **Reset** / 键盘 **Home** 仍只用于头盔复位。若实体头显的佩戴位置或朝向需要调整，仍须在设备上重新定位并测试舒适性；目前此功能只在 Unity 编辑器模拟环境开发。

这不是普通第一人称 WASD 控制器。模拟器可以帮助没有头显的组员预览与检查输入绑定，真实手柄的定位、传送和抓取仍需要设备测试。

### 接真实 PC VR 头显

1. 先退出 Play。
2. 打开 **Edit > Project Settings > XR Plug-in Management > XR Interaction Toolkit**，取消 **Use XR Device Simulator in scenes**（对应 Automatically Instantiate Simulator Prefab）。否则模拟器可能接管真实设备输入。
3. 在同一设置窗口的 **XR Plug-in Management** 中选择桌面平台，确认 **OpenXR** 已启用。
4. 本工程启用 Oculus Touch、Valve Index、HTC Vive 和 Khronos Simple Controller profile。按实际设备确认对应 profile；这只是兼容配置，不代表这些设备都已经测试。
5. 启动你们设备对应的 SteamVR、Meta Quest Link 或 Virtual Desktop，并确认对应 OpenXR runtime，再回 Unity 进入 Play。使用 Virtual Desktop / VDXR 时，Streamer 选择 VDXR，Unity 的 **Play Mode OpenXR Runtime** 使用 **System Default**；不要为了本工程额外切换到其他 runtime。[Unity Play 模式 runtime 说明](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.14/manual/project-configuration.html#choose-an-openxr-runtime-to-use-in-play-mode)、[VDXR 开发说明](https://github.com/mbucchia/VirtualDesktop-OpenXR/wiki/Developers)。
6. 实机检查进入 Play 的站立眼睛高度、左右转头、俯仰、身体前倾和蹲下，再测试手柄。主场景保留 **Floor** 跟踪，实际眼睛高度由设备提供；**1.65 m Camera Y Offset** 用于 Device 模式，不应再给 Floor 模式叠加身高。修复和定向测试见[本轮活动](activity/2026-10-13-xr-head-tracking-fix.md)；自动合成输入不能代替实机通过。
7. 若只是自己临时切换模拟器，提交前检查 XRI 设置改动，避免无意影响其他组员。

**站着玩、地面正常但展品偏高：** 优先调整展示布局，保留设备提供的眼睛高度。本轮中央台面由 1.16 m 降至 0.96 m，头盔、年代按钮和复位点一起下移 20 cm；热点信息卡保留原阅读高度。设备体验仍需复测，详见[站姿高度调整记录](activity/2026-10-13-standing-display-height.md)。

### Unity Version Control 和 GitHub 的关系

Unity 工程可以直接由 Git 管理，因此现在的文件已经能连接 GitHub。Unity 内置的 **Version Control** 面板主要连接 Unity Version Control 服务；它不是这个 GitHub 仓库的上传按钮。

本组目前采用 **Unity 编辑并保存 → GitHub Desktop 提交和推送 → GitHub 审查合并**。无需在 Unity 再绑定同名的云端仓库。

这台电脑的拥有者无需重新克隆：在 GitHub Desktop 中用 **File > Add local repository**，选择 **E:\XRmuseum** 即可。Unity Hub 选择的则是下一层 **E:\XRmuseum\MuseumVR**。

## 5. 每次开始和结束开发

### 共同的记录入口

| 内容 | 唯一主要位置 | 何时更新 |
| --- | --- | --- |
| 稳定的 AI 工作边界 | 根 [AGENTS.md](../AGENTS.md) | 工作规则改变时，单独说明原因 |
| 当前成果、限制、目标和重要决定 | [project-state.md](project-state.md) | 合并使状态或方向改变时，由整合人归并 |
| 正在做的任务、负责人、路径和依赖 | [GitHub Issues](https://github.com/Twispra/XRmuseum/issues) | 认领、范围变化、阻塞或状态变化时 |
| 本次改动、验收和接手步骤 | 对应 PR 的模板 / 交接段 | 推送、换人、暂停或准备审查时 |
| 已发生工作与课程证据 | 独立 `docs/activity/` 文件 + [周索引](weekly-activity.md) | 实质工作完成时；每周由一人汇总 |
| 资源来源与 AI 辅助范围 | [external-resources.md](external-resources.md) | 使用新资源或辅助范围改变时 |

不另建六份总任务表、共享长聊天或统一巨大 prompt。PR 尚未建立时把交接写在原 Issue；离线中断可先保存独立活动文件，联网后提交并链接。任何仅本地且未推送的文件，对队友都不可用。

### 认领和开工

1. 成员先读 AGENTS、状态页和相关说明，在 GitHub 确认已有任务，避免重复创建。每项任务设置一位实际负责人及明确验收条件，写出预计修改路径、基础版本与未合并依赖。使用 [任务模板](../.github/ISSUE_TEMPLATE/task.md)，小型独立修订可直接在 PR 说明用户授权和范围。
2. 查看其他进行中的任务是否涉及同一路径。高冲突资源由一位轮值整合人在 Issue 确认写入者和顺序，再开始该部分。认领留言或 Assignee 不是原子文件锁；两人发生重叠时由整合人协调，不以两个 AI 都“已认领”作为依据。整合人不是必须承担全部开发的人。
3. 每人使用自己的 clone 和 Git 身份。一人同时开多个 Codex 修改任务也要用独立 worktree / clone，不能共用一个可写工程目录。Unity 同一项目一次只开一个 Editor；后台子任务只改已划清边界的文件。
4. 检查分支、HEAD 和 Changes，记下开工前已有改动的归属。先保存自己明确负责的资源；不要让 AI 自动清理别人的改动。切换分支或拉取场景/package 更新前关闭 Unity；有重叠未提交改动时先协调保存，不直接 Pull 或 reset。
5. **Fetch origin** 后确认主线状态。正常任务从最新 `main` 建分支，例如 `codex/issue-12-reset-feedback`；已授权依赖未合并 PR 时，明确写出依赖及基准 commit。不要把他人浮动功能分支当长期公共主线。

共享范围的协调信息写在对应 Issue：由整合人确认“当前写入者、具体路径、开始顺序”，完成后记录“已合并/已交接/已撤销，现已释放”。中断或待审查不自动释放；先推送已有工作并写明接手版本，再由整合人确认下一位，避免前任恢复会话后继续写入。两个 Issue 涉及同一路径时互相链接，后一个写明等待前一个释放；不额外维护分支里的认领表。

### 开发、提交和合并

1. 在约定路径开发并保存 Scene / Prefab。发现目标改变或需要改共享文件，先更新任务范围并解决重叠；可继续独立部分，无需对已授权小步骤重复询问。
2. 按受影响行为验证，留下版本与证据。共享场景或 XR 配置改变后需要相应 Unity 检查；文档修订只检查事实、链接和差异即可。
3. 在 GitHub Desktop **Changes** 逐文件审查并勾选属于本任务的内容，包含相关 `.meta`。确认未纳入开工前未知改动、缓存、大量重导入噪声或私人资料。写清 commit 内容；提交后 **Push origin**。
4. 建立 PR，填写 [PR 模板](../.github/pull_request_template.md) 的结果、被测版本、验证限制及交接。工作未完成可开 Draft PR 保存已推送的进度，不能把草稿当验收完成。
5. 另一位成员看实际 diff、资产与验证证据；同一共享路径一次合并一项。合并前同步最新目标分支并解决冲突：保留 GUID、逐项检查场景/Prefab 引用；不要整文件接受 ours/theirs。同步改变被测内容时重新验证受影响行为。
6. 整合人合并后关闭任务，按需更新项目状态、决定及周索引。其他成员 Fetch 并检查依赖，再开始下一项。阶段交付额外记录构建来源 commit、构建产物和人工设备结果。

2026-10-06 的[项目记录核对](activity/2026-10-06-project-records-audit.md)已确认五个参与账号，完整六人名单、姓名映射与正式轮值安排仍待团队确认；不据此自动分配任务。本机已有当日启用主线 PR/审批保护及后续管理员豁免的记录，但本次连接器回读保护返回 403，无法独立确认当前远程设置；需要查看有权限的 GitHub 设置页，不能再依据旧文档断言“未启用强制审批”。文档、模板和 LFS 本身均不能强制执行这些权限，日常仍按上面流程协作。

### 让新 Codex 任务接上进度

可以给自己的 Codex 这样一句话，并填入真实任务链接：

> 请先读仓库根 AGENTS.md 和 docs/project-state.md，再读取 Issue #编号 及相关 PR。核对分支、HEAD、当前改动和任务认领范围，继续该任务；从交接记录中尚未完成的最小步骤开始，保留现有未知改动。结束时按 PR 模板记录证据和下一步。

Codex 官方说明它会在运行开始时读取适用的 AGENTS 指令；根到当前工作目录的更近层规则可能覆盖前面的规则。因此保持根文件简短，避免在子目录放互相矛盾的版本。已有会话拉到新规则后显式读文件或开始新会话，不能假设其他人的 AI 已自动刷新。[OpenAI 官方说明](https://learn.chatgpt.com/docs/agent-configuration/agents-md)

GitHub 的 Issue / PR 模板合入默认分支后才成为团队默认入口；本轮仍在功能分支时可以手动复制模板，不能认为全队已经自动启用。[GitHub 官方说明](https://docs.github.com/en/communities/using-templates-to-encourage-useful-issues-and-pull-requests/about-issue-and-pull-request-templates)

## 6. Unity 协作约定

- 将团队新资源放在 `Assets/Museum/` 下，按 Scenes、Scripts、Prefabs、Materials、Models、Audio、UI 等分类。
- 在 Unity 内移动或重命名资源，使 `.meta` 和资源 GUID 一起保留。
- 同一 `.unity`、Prefab 或 `.blend` / FBX 在同一时段安排一个写入者。优先制作独立展品 Prefab、脚本、模型或测试场景，再由整合人接入 `MuseumStart.unity`。公共 XR Origin、Input Actions、`Packages/` 和 `ProjectSettings/` 同样需要先协调范围。
- 提交 `Assets`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings`。
- 不提交 `Library`、`Temp`、`Logs`、`UserSettings` 或构建输出。`.gitignore` 已排除这些目录。
- `.gitattributes` 为 FBX、Blend、PSD、WAV、MP4 等二进制文件配置了 LFS。导入新的大文件类型前检查是否需要增加规则，并让所有成员安装 LFS。
- 当前没有配置 LFS 文件锁。LFS 是存储方式，不能防止两人覆盖同一二进制资产；依靠任务认领与串行整合。后续确需锁定时先验证团队远程和客户端支持，再制定释放/接手规则。
- 不要把 `.unity`、`.prefab` 或 `.meta` 整体改成 LFS；它们需要可读的差异与稳定的引用。
- 不把 Unity 缓存、密钥、访问令牌或账号密码放进仓库。
- Blender 01–09 原型脚本保留原机器输出路径，重跑前应在隔离副本中适配并使用新输出目录/版本。灰模的导出 FBX 与 Unity 使用副本更新时校验相同，保留目标 `.meta`。
- 不把初始化工具当作“修复工程”按钮。`HelmetPrototypeSetup.Build` 会配置导入器、材质、Prefab、场景和 XR Rig，现检测已有成果即拒绝；日常打开和验证直接使用已保存资产。确需重建时作为明确任务处理，不删资产来绕过保护。
- 新测试和预览写唯一运行目录，不覆盖已引用证据。报告需说明源码 commit、dirty 文件及环境；自动模拟通过、真实设备通过、用户评价和可执行构建是不同验证层级。

## 7. 设备验证与提交范围

Editor 能打开或进入 Play Mode，不代表头显跟踪、控制器输入、传送移动和实际舒适度已经验证。
提交阶段 2 前，需要在课程目标设备上完成至少一条完整用户流程，并提供可运行的 Windows 构建、技术说明及每周记录。
最终构建放在仓库外或根目录的 `Builds/`（该目录已忽略），按照 Canvas 指定方式交付。

2026-09-22 曾生成初始化场景的 Windows Development 构建，本机位置 `E:\XRmuseum\Builds\Windows\MuseumVR.exe`。它不含随后开发的头盔成果，且不会随 clone 下载；不能当作当前版本交付。构建依赖同目录的其他文件，不能只发送单个 exe；真实 VR 运行需要对应设备和 runtime。[初始化历史验证](initialization-checks.md)

> 当前版本的可运行构建怎么出、有哪些坑，见 [Windows 可运行构建（批处理与 GUI）](build-windows.zh-CN.md)。

## 8. 本机初始化记录

- Git 仓库：`E:\XRmuseum`。
- Unity 工程：`E:\XRmuseum\MuseumVR`。
- 旧工程 `E:\XRproject\museum\museum` 不属于本仓库。
- Git remote origin 指向 `https://github.com/Twispra/XRmuseum.git`。
- 本仓库单独设置提交身份，不修改全局 Git 配置。
- 官方 VR 模板在 Hub 下载时出现 TLS 连接中断，因此以本机已安装的 3D 模板建立基础工程，再配置 XR 依赖。
- URP、线性色彩空间、4x MSAA、Windows 64 位 / Direct3D 11、OpenXR Single Pass Instanced 已配置。
- Unity 资源使用 Force Text 和 Visible Meta Files，便于 Git 查看差异和保留引用。
- 官方 XRI 2.6.4 的 Starter Assets 与 XR Device Simulator 保存在 Assets/Samples；TextMesh Pro 必需资源保留在 Assets/TextMesh Pro。
- 9 月 22 日初始化展厅的三个几何体是占位资源。随后头盔候选版本替换了中央方块，另外两处仍是占位展台；当前可用范围以所在分支和[状态页](project-state.md)为准。

## 参考

- Unity Hub 创建工程：https://docs.unity.com/hub/project-create
- Unity XR 工程配置：https://docs.unity.com/en-us/engine/6000.0/manual/xr/configuring-project-for
- GitHub Desktop 克隆仓库：https://docs.github.com/en/desktop/adding-and-cloning-repositories/cloning-and-forking-repositories-from-github-desktop
- GitHub Desktop 与 LFS：https://docs.github.com/en/desktop/configuring-and-customizing-github-desktop/about-git-large-file-storage-and-github-desktop
