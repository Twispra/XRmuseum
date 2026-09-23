# 组员首次打开与日常协作

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
3. 本工程开启了官方 **XR Device Simulator**，只在 Editor 中自动创建。按 **Tab** 在头部与左右手之间切换，按 **U** 切换身体操作；以画面上的模拟器提示为准。
4. **W/A/S/D** 控制当前设备的移动，**Q/E** 调整高度；按住鼠标右键操作头部视角。按 **T/Y** 切换左/右控制器操作，**G** 模拟握持键，鼠标左键模拟扳机。
5. 中间展台已替换为带有 **XR Grab Interactable** 的头盔简模：靠近并按住 **G** 抓取，松开后放下，按 **Home** 复位。详细说明见[头盔抓取原型](helmet-interaction.zh-CN.md)。地面带有 **Teleportation Area**，使用 XRI 的 Teleport 交互层。模拟器的 **1/3** 在摇杆输入和位置输入目标之间切换，完整映射见导入的 XR Device Simulator inputactions。
6. 再次点击 Play 退出。**Play 期间对场景的临时改动不会自动保存到编辑状态。**

这不是普通第一人称 WASD 控制器。模拟器可以帮助没有头显的组员预览与检查输入绑定，真实手柄的定位、传送和抓取仍需要设备测试。

### 接真实 PC VR 头显

1. 先退出 Play。
2. 打开 **Edit > Project Settings > XR Plug-in Management > XR Interaction Toolkit**，取消 **Use XR Device Simulator in scenes**（对应 Automatically Instantiate Simulator Prefab）。否则模拟器可能接管真实设备输入。
3. 在同一设置窗口的 **XR Plug-in Management** 中选择桌面平台，确认 **OpenXR** 已启用。
4. 本工程启用 Oculus Touch、Valve Index、HTC Vive 和 Khronos Simple Controller profile。按实际设备确认对应 profile；这只是兼容配置，不代表这些设备都已经测试。
5. 启动你们设备对应的 SteamVR 或 Meta Quest Link，并将它设为当前 OpenXR runtime，再回 Unity 进入 Play。
6. 若只是自己临时切换模拟器，提交前检查 XRI 设置改动，避免无意影响其他组员。

### Unity Version Control 和 GitHub 的关系

Unity 工程可以直接由 Git 管理，因此现在的文件已经能连接 GitHub。Unity 内置的 **Version Control** 面板主要连接 Unity Version Control 服务；它不是这个 GitHub 仓库的上传按钮。

本组目前采用 **Unity 编辑并保存 → GitHub Desktop 提交和推送 → GitHub 审查合并**。无需在 Unity 再绑定同名的云端仓库。

这台电脑的拥有者无需重新克隆：在 GitHub Desktop 中用 **File > Add local repository**，选择 **E:\XRmuseum** 即可。Unity Hub 选择的则是下一层 **E:\XRmuseum\MuseumVR**。

## 5. 每次开始和结束开发

1. 开始前先保存现有改动。需要切换分支、拉取包含场景或 package 的改动时，建议先关闭 Unity。
2. 在 GitHub Desktop 点击 **Fetch origin**，有更新时点击 **Pull origin**。
3. 从最新 `main` 创建自己的分支，例如 `feature/名字-exhibit-interaction`。
4. 在 Unity 内完成修改并保存 Scene、Prefab 等资源。
5. 回到 GitHub Desktop 的 Changes，检查每个文件是否属于本次任务，确认新资源的 `.meta` 也已包含。
6. 写清楚改动内容，点击 **Commit to 当前分支**。Commit 只保存到本地。
7. 点击 **Push origin**（新分支可能显示 Publish branch），把提交同步到 GitHub。
8. 点击 **Create Pull Request**，说明改了什么、怎样验证，再由另一位组员检查后合并。

## 6. Unity 协作约定

- 将团队新资源放在 `Assets/Museum/` 下，按 Scenes、Scripts、Prefabs、Materials、Models、Audio、UI 等分类。
- 在 Unity 内移动或重命名资源，使 `.meta` 和资源 GUID 一起保留。
- 两人尽量不要同时修改同一个 `.unity` 场景；按展区分工，或把展品制作成独立 Prefab 再由场景负责人整合。
- 提交 `Assets`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings`。
- 不提交 `Library`、`Temp`、`Logs`、`UserSettings` 或构建输出。`.gitignore` 已排除这些目录。
- `.gitattributes` 为 FBX、Blend、PSD、WAV、MP4 等二进制文件配置了 LFS。导入新的大文件类型前检查是否需要增加规则，并让所有成员安装 LFS。
- 不要把 `.unity`、`.prefab` 或 `.meta` 整体改成 LFS；它们需要可读的差异与稳定的引用。
- 不把 Unity 缓存、密钥、访问令牌或账号密码放进仓库。
- 每周更新 `docs/weekly-activity.md`，记录实际任务、负责人、问题和 commit/PR 链接。

## 7. 设备验证与提交范围

Editor 能打开或进入 Play Mode，不代表头显跟踪、控制器输入、传送移动和实际舒适度已经验证。
提交阶段 2 前，需要在课程目标设备上完成至少一条完整用户流程，并提供可运行的 Windows 构建、技术说明及每周记录。
最终构建放在仓库外或根目录的 `Builds/`（该目录已忽略），按照 Canvas 指定方式交付。

本次已生成基础场景的 Windows Development 构建：`E:\XRmuseum\Builds\Windows\MuseumVR.exe`。它依赖同目录的其他文件，不能只发送单个 exe；真实 VR 运行需要接入对应设备和 runtime。当前初始化验证见 [验证记录](initialization-checks.md)。

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
- 展厅与测试 Prefab 保存在 Assets/Museum。三个几何体是占位资源，不能作为已完成的正式展品申报。

## 参考

- Unity Hub 创建工程：https://docs.unity.com/hub/project-create
- Unity XR 工程配置：https://docs.unity.com/en-us/engine/6000.0/manual/xr/configuring-project-for
- GitHub Desktop 克隆仓库：https://docs.github.com/en/desktop/adding-and-cloning-repositories/cloning-and-forking-repositories-from-github-desktop
- GitHub Desktop 与 LFS：https://docs.github.com/en/desktop/configuring-and-customizing-github-desktop/about-git-large-file-storage-and-github-desktop
