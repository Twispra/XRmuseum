# Windows 可运行构建（批处理与 GUI）

本页回答一件事：**怎么在当前版本的工程里产出一个可运行的 Windows 构建，以及会踩到什么坑。**

- 适用版本：编辑器 **2022.3.62f3c1**，目标平台 `StandaloneWindows64`，
  唯一构建场景 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`。
- 本页只讲**步骤与排错**。构建产物的验收结论、体积分析和运行证据另有记录，
  不在这里重复；[项目状态](project-state.md)是版本快照，当前交付以对应活动／PR 和实际日志为准。
- 「构建成功」**不等于**「XR/真机验收通过」。这两件事必须分开写，仓库其它文档也是这么要求的。

## 1. 构建前核验（不用打开 Unity）

推荐从已确认的 commit 创建**新的干净构建 worktree**，让日常工程的未提交设置和正在运行的
Unity 留在原处。下面是 **PowerShell** 命令；尖括号内容均须替换为实际绝对路径或 commit。
新工作副本不得与其他 Unity 任务共用，同一工作副本也不能同时运行 GUI 和批处理 Editor。

```powershell
git -C '<日常仓库>' worktree add --detach '<新的构建工作副本>' '<已确认的commit>'
$buildRepo = '<新的构建工作副本>'
git -C $buildRepo rev-parse HEAD
git -C $buildRepo status --short
git -C $buildRepo lfs pull
git -C $buildRepo lfs checkout
git -C $buildRepo lfs fsck --pointers HEAD
git -C $buildRepo lfs ls-files
Get-Content -LiteralPath (Join-Path $buildRepo 'MuseumVR/ProjectSettings/ProjectVersion.txt')
Get-Content -LiteralPath (Join-Path $buildRepo 'MuseumVR/ProjectSettings/EditorBuildSettings.asset')
```

继续前确认工作副本没有未知改动；`lfs fsck` 只检查对象／指针，不能代替工作区展开检查。
如果要构建尚未提交的修改，先在独立副本保留这些修改并记录来源及 dirty 清单；不要用
撤销整个设置文件的方法清理日常工作区。

构建前另保存设置副本，构建后按第 4 节比较。以下仍为 **PowerShell**，备份目录放在仓库外，
必须是新路径；目录或备份文件已存在时停止，避免覆盖已有证据。

```powershell
$backupDir = '<仓库外的新备份目录>'
if (Test-Path -LiteralPath $backupDir) { throw '备份目录已存在，请选择新目录' }
New-Item -ItemType Directory -Path $backupDir | Out-Null
$settingsPath = Join-Path $buildRepo 'MuseumVR/ProjectSettings/ProjectSettings.asset'
$settingsBackup = Join-Path $backupDir 'ProjectSettings.before-build.asset'
[IO.File]::Copy($settingsPath, $settingsBackup, $false)
Get-FileHash -LiteralPath $settingsBackup -Algorithm SHA256
```

要确认的三件事：编辑器版本是 **2022.3.62f3c1**；构建场景列表里
`MuseumStart.unity` 已启用且在第 0 位；LFS 资产（FBX / 音频等）在工作区已展开成真实文件
而不是几百字节的指针（`git lfs ls-files` 里 `*` 表示展开、`-` 表示还是指针）。

## 2. 方式一：批处理（适合自动化与留日志）

下面是 **cmd.exe 的 `.bat` 文件**语法，不要直接粘贴进 PowerShell。
先创建新的日志和输出目录，按本机安装位置替换 Unity 路径；将脚本保存后再运行。

```bat
@echo off
setlocal
"D:\Program Files (x86)\Unity\2022.3.62f3c1\Editor\Unity.exe" ^
  -batchmode -quit ^
  -buildTarget win64 ^
  -projectPath "<仓库>\MuseumVR" ^
  -logFile "<日志路径>\build.log" ^
  -buildWindows64Player "<输出目录>\MuseumVR.exe"
set "BUILD_RC=%ERRORLEVEL%"
echo ExitCode=%BUILD_RC%
exit /b %BUILD_RC%
```

要点：

- 路径全部写**绝对路径**，不要依赖当前目录。
- 不要加 `-nographics`（桌面会话下更少意外）；确实卡住时再作为对照组尝试。
- `<仓库>` 指第 1 节的构建工作副本；日志和产物均使用新的运行目录。
- `.bat` 推荐 CRLF 换行，并保留适用于当前 `cmd.exe` 的原文件编码（见坑 A）。
  caret `^` 后不要留空格，尤其要核对路径中的中文字符。
- 在 Unity 命令结束后立即保存 `%ERRORLEVEL%`；同时保留完整构建日志，退出码并非唯一判据。
- 从 PowerShell 调用已保存的脚本时可用 `& '<脚本绝对路径>.bat'`，紧接着记录
  `$buildExitCode = $LASTEXITCODE`。上述参数见 [Unity 2022.3 命令行说明](https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html)。

## 3. 方式二：Unity GUI（便于人工检查设置）

1. 在 Unity Hub 打开第 1 节构建工作副本中的 `MuseumVR`，确认版本仍是 2022.3.62f3c1。
2. `File > Build Settings`：目标选 **Windows / x86_64**；确认场景列表里
   `MuseumStart` 已勾选且在第 0 位（其它场景不要勾）。
3. `Build`，选一个**仓库外**的输出目录。
4. 构建后回看 `ProjectSettings/ProjectSettings.asset` 有没有被改动（见坑 B）。

历史构建记录曾观察到紧接批处理再进行 GUI 构建时复用 player data
（日志出现 `player data was not rebuilt`），两次产物字节及时间戳相同；不能据此推断每次
GUI 构建都会复用输出。为区分各次运行，选新的输出目录，分别保存日志、文件清单和哈希，
并如实记录增量复用；不为了制造变化而修改产品内容或删除已引用的历史产物。

历史记录中的 GUI 构建没有观察到 `preloadedAssets` 被清空；这是有限观测，不能保证以后
GUI 构建不改设置。两种方式都须比较构建前后设置，并执行相同的构建与设备验收。

## 4. 三个容易踩的地方

### 坑 A：排查 `.bat` 的换行、编码和退出码记录

历史构建时曾遇到 `echo ExitCode=%RC%>> "%LOG%"` 未留下预期记录，但没有隔离确认根因；
后续本机最小复核中，包含 caret 续行、变量和退出码的 LF-only 脚本也能正常执行。
因此不要把 LF 一律认定为解析错误。优先检查当前 shell、续行符尾部空格、变量赋值、
日志目录是否存在、路径引号及编码；需要时用不启动 Unity 的最小脚本定位。

若统一为 CRLF，在编辑器中仅改变换行并保留原编码，另存新副本后核对中文路径没有变化。
不要用 ASCII 编码覆写含中文的脚本，也不要未经确认把 UTF-8 转为 GBK；当前 shell 的
解码方式须与文件编码对应。[Microsoft 编码说明](https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding)
指出 ASCII 默认会把无法表示的字符替换为问号。

### 坑 B：比较并保留构建前的 `ProjectSettings`

历史记录称两次批处理构建后，`MuseumVR/ProjectSettings/ProjectSettings.asset` 中原有的两条

```
  preloadedAssets:
  - {fileID: …, guid: 25d5a458c11343343935968c71f94483, type: 2}   # com.unity.xr.management.loader_settings
  - {fileID: …, guid: e5d58f9cc1d44cb4c9239fda248dbab1, type: 2}   # com.unity.xr.openxr.settings4
```

变成了 `preloadedAssets: []`，随后已恢复。这是特定版本和运行环境的观测，不是所有
批处理必然发生、GUI 必然免疫的规律。本轮文档修正没有重新运行 Unity 验证该现象。

处理：

1. 构建结束后关闭 Unity，保留本次日志、dirty 清单及构建后设置副本。
2. 将第 1 节构建前副本与当前文件逐项比较，只恢复**确认由此次构建引入**的差异；
   原有 Player 设置修改和其他人的改动必须保留。不要按 Git 的 index／HEAD 整文件覆盖。
   构建期间若有人修改同一文件，先协调重叠部分，不能把其改动当成构建噪声撤销。
3. 两种构建方式都核对 `preloadedAssets` 的实际条目与构建前副本一致；当前保存基线是上面
   两条，但应以此次构建前的已确认设置为准。
4. 在构建记录中注明准确 commit、构建时的额外改动，以及后来恢复了哪些差异。

历史播放器日志仍报告 `UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json` 的
`1 displays + 1 inputs`。这说明当时注册了相关子系统，不能单独证明真实头显会话、跟踪或输入
正常，也不能据此排除设置差异的影响。该影响仍未证实；不要直接写成“VR 起不来”或“已无影响”。

### 坑 C：构建产物不要提交

仓库 `.gitignore` 已忽略 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 与根目录 `Builds/`。
产物放仓库外或根目录 `Builds/`，**不要**提交进 Git；也不要只发一个 exe，
它依赖同目录的 `*_Data/`、`MonoBleedingEdge/`、`UnityPlayer.dll`。

## 5. 构建后收证据（判据要写清）

下面是 **PowerShell** 的只读收集命令；沿用第 1 节 `$buildRepo`，填入本次输出与日志路径。

```powershell
$buildOutputDir = '<本次输出目录>'
$buildLog = '<本次完整构建日志>'
Get-ChildItem -LiteralPath $buildOutputDir
(Get-ChildItem -LiteralPath $buildOutputDir -File -Recurse | Measure-Object -Property Length -Sum).Sum
Get-FileHash -LiteralPath (Join-Path $buildOutputDir 'MuseumVR.exe') -Algorithm SHA256
Select-String -LiteralPath $buildLog -Pattern 'Build succeeded|Build completed with a result of|error CS|return code'
Get-Content -LiteralPath $buildLog -Tail 20
git -C $buildRepo status --short
git -C $buildRepo diff -- MuseumVR/ProjectSettings/ProjectSettings.asset
```

构建日志必须明确给出本次 **`Build succeeded`／`Build completed with a result of 'Succeeded'`**
或相应的成功 BuildReport，再结合批处理进程退出码 0、没有编译／构建错误、完整且对应本次
运行的产物作判断。成功状态见 [Unity BuildResult.Succeeded](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Build.Reporting.BuildResult.Succeeded.html)。
**退出码 0、`error CS` 为 0 或目录中已有 exe 都不能单独证明此次构建成功**；缺少明确结果时
保留完整日志进一步检查。GUI 也须保留构建结果，不能只看窗口正常关闭。

核对 exe、`*_Data/`、`UnityPlayer.dll` 等运行依赖；当前 Mono 后端还需 `MonoBleedingEdge/`，
以本次后端和完整文件清单为准。另外两条容易漏：

- `PoseControl` / `Vector2Control` 弃用警告不是编译错误，但要保留具体日志并确认是否还有
  构建校验错误；警告的存在不能证明整个 OpenXR 构建校验或运行初始化通过。
- 记录被测 commit 与额外未提交改动（本次构建相对哪个 `main`、有哪些 dirty 文件）。

## 6. 无头显启动现象与 Editor 模拟器

历史运行在没有可用头显时进入并渲染主场景，但未能执行 VR 交互。播放器日志
（`%USERPROFILE%\AppData\LocalLow\<公司名>\MuseumVR\Player.log`）当时记录了：

```
xrGetSystem: XR_ERROR_FORM_FACTOR_UNAVAILABLE
```

按 [OpenXR 返回码定义](https://registry.khronos.org/OpenXR/specs/1.0/man/html/XrResult.html)，
所请求形态受支持，但设备当前不可用，例如未连接或未开机；需结合设备和运行时环境检查。
它不能单独归因为构建失败，也不能证明构建没有其他缺陷。

本机预览交互直接在 Editor 打开已有 `MuseumStart` 并按 Play：当前保存的
`Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` 已启用
`m_AutomaticallyInstantiateSimulatorPrefab=1` 和 `m_AutomaticallyInstantiateInEditorOnly=1`，
会在 Editor 自动创建 XR Device Simulator，无需手动拖入 prefab；该设置不会在播放器中
自动创建模拟器。若没有出现，检查 `Edit > Project Settings > XR Plug-in Management >
XR Interaction Toolkit` 的 `Use XR Device Simulator in scenes` 与 `Instantiate In Editor Only`，
按[首次打开指南](getting-started.zh-CN.md)核对。桌面键位见[头盔交互说明](helmet-interaction.zh-CN.md)。
模拟输入通过仍不能代替真实头显验收。

## 7. 与其它文档的关系

- 首次打开工程、GitHub Desktop 流程、XR 模拟器快捷键：[组员首次打开与日常协作](getting-started.zh-CN.md)
- 课程对 Phase 2 交付物的要求：[课程依据与团队约定](course-requirements.md)
- 协作规则（谁在什么时候可以写共享文件）：仓库根 [AGENTS.md](../AGENTS.md)
