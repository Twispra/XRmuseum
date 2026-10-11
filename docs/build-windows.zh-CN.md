# Windows 可运行构建（批处理与 GUI）

本页回答一件事：**怎么在当前版本的工程里产出一个可运行的 Windows 构建，以及会踩到什么坑。**

- 适用版本：编辑器 **2022.3.62f3c1**，目标平台 `StandaloneWindows64`，
  唯一构建场景 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`。
- 本页只讲**步骤与排错**。构建产物的验收结论、体积分析和运行证据另有记录，
  不在这里重复；`docs/project-state.md` 才是"当前有没有可交付构建"的判定处。
- 「构建成功」**不等于**「XR/真机验收通过」。这两件事必须分开写，仓库其它文档也是这么要求的。

## 1. 构建前核验（不用打开 Unity）

```bash
git -C <仓库> status --porcelain                                  # 记下开工前已有改动
git -C <仓库> lfs fsck --pointers HEAD                            # LFS 指针完整
grep m_EditorVersion <仓库>/MuseumVR/ProjectSettings/ProjectVersion.txt
grep -A3 m_Scenes <仓库>/MuseumVR/ProjectSettings/EditorBuildSettings.asset
```

要确认的三件事：编辑器版本是 **2022.3.62f3c1**；构建场景列表里
`MuseumStart.unity` 已启用且在第 0 位；LFS 资产（FBX / 音频等）在工作区已展开成真实文件
而不是几百字节的指针（`git lfs ls-files` 里 `*` 表示展开、`-` 表示还是指针）。

## 2. 方式一：批处理（适合自动化与留日志）

```bat
"D:\Program Files (x86)\Unity\2022.3.62f3c1\Editor\Unity.exe" ^
  -batchmode -quit ^
  -projectPath "<仓库>\MuseumVR" ^
  -logFile "<日志路径>\build.log" ^
  -buildWindows64Player "<输出目录>\MuseumVR.exe"
```

要点：

- 路径全部写**绝对路径**，不要依赖当前目录。
- 不要加 `-nographics`（桌面会话下更少意外）；确实卡住时再作为对照组尝试。
- Windows 上把这几行放进 `.bat` 更容易复现，但 **`.bat` 必须用 CRLF 换行**（见坑 A）。
- Unity 会把返回码写进日志尾部（形如 `Application will terminate with return code 0`）；
  `.bat` 里再用 `%ERRORLEVEL%` 单独落一行更保险（这也是坑 A 会伤到的地方）。

## 3. 方式二：Unity GUI（正式 VR 交付推荐）

1. 打开 Unity Hub → 选中 `MuseumVR`，确认版本仍是 2022.3.62f3c1。
2. `File > Build Settings`：目标选 **Windows / x86_64**；确认场景列表里
   `MuseumStart` 已勾选且在第 0 位（其它场景不要勾）。
3. `Build`，选一个**仓库外**的输出目录。
4. 构建后回看 `ProjectSettings/ProjectSettings.asset` 有没有被改动（见坑 B）。

另外：紧接在批处理构建之后再跑 GUI 构建，Unity 往往判定为**增量构建**
（日志出现 `player data was not rebuilt`），产物与批处理那次逐字节相同、时间戳不变。
要拿到"确实由 GUI 重新产出"的证据，得先清掉上次的输出目录或让工程内容真正变化。

GUI 构建不会改写 XR 的 `preloadedAssets`，所以正式交付建议走这条路。

## 4. 三个容易踩的地方（都在本工程实测过）

### 坑 A：`.bat` 用 LF 换行会让 `cmd.exe` 解析错乱

症状：`echo ExitCode=%RC%>> "%LOG%"` 既不写日志也不带出变量值，退出码丢失。
用 PowerShell 转成 CRLF，并校验 CR 数 == LF 数：

```powershell
$p = "<脚本>.bat"
$t = [IO.File]::ReadAllText($p) -replace "`r`n","`n" -replace "`n","`r`n"
[IO.File]::WriteAllText($p, $t, (New-Object System.Text.ASCIIEncoding))
```

### 坑 B：批处理会清空 XR 的 `preloadedAssets`

构建后 `MuseumVR/ProjectSettings/ProjectSettings.asset` 里原本的两条

```
  preloadedAssets:
  - {fileID: …, guid: 25d5a458c11343343935968c71f94483, type: 2}   # com.unity.xr.management.loader_settings
  - {fileID: …, guid: e5d58f9cc1d44cb4c9239fda248dbab1, type: 2}   # com.unity.xr.openxr.settings4
```

会被改成 `preloadedAssets: []`。

处理：

1. **提交前必须还原**，别让它混进提交：

   ```bash
   git -C <仓库> checkout -- MuseumVR/ProjectSettings/ProjectSettings.asset
   ```

2. 在构建记录里如实写明"构建时存在该未提交改动"。
3. 正式 VR 交付改用 GUI 构建，构建后确认 `preloadedAssets` 仍是 2 条。

**关于危害，要说准**：这个改动在本工程的批处理构建里**已经出现两次**（两次都已还原）；
但它是否真的影响播放器启动时的 OpenXR 初始化，我们**没有证实**——
实际播放器日志里 `UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json` 仍报
`1 displays + 1 inputs`，也就是子系统照常装载。
所以这条的正确写法是"**会造成 ProjectSettings 的意外改动，必须还原**"，
不该写成"会导致 VR 起不来"。（凭机制推断下结论前先找运行证据。）

### 坑 C：构建产物不要提交

仓库 `.gitignore` 已忽略 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 与根目录 `Builds/`。
产物放仓库外或根目录 `Builds/`，**不要**提交进 Git；也不要只发一个 exe，
它依赖同目录的 `*_Data/`、`MonoBleedingEdge/`、`UnityPlayer.dll`。

## 5. 构建后收证据（判据要写清）

```bash
ls -la "<输出目录>"                    # exe / <名字>_Data / MonoBleedingEdge / UnityPlayer.dll
du -sh "<输出目录>"                    # 完整体积
certutil.exe -hashfile "<输出目录>\MuseumVR.exe" SHA256
grep -c "error CS" "<日志>"            # 编译错误数，应为 0
tail -n 3 "<日志>"                     # 应出现 return code 0
git -C <仓库> status --porcelain       # 记录 Unity 改动了哪些文件
```

判定"构建成功"的三个条件缺一不可：**进程退出码 0 + `error CS` 0 条 + 产物文件齐全**。
另外两条容易漏：

- 构建日志里的 **OpenXR 弃用警告**（`PoseControl` / `Vector2Control`）是官方可选警告，不是错误；
  它出现反而说明 OpenXR 构建校验确实跑过。
- 记录被测 commit 与额外未提交改动（本次构建相对哪个 `main`、有哪些 dirty 文件）。

## 6. 本机没有头显时的已知现象（不是构建缺陷）

双击产物能进主场景、能正常渲染，但没有头显时**不可操作**。播放器日志
（`%USERPROFILE%\AppData\LocalLow\<公司名>\MuseumVR\Player.log`）会给出原因：

```
xrGetSystem: XR_ERROR_FORM_FACTOR_UNAVAILABLE
```

这是"本机没有头显"，不是工程或构建的问题。要在本机试交互，
得手动把 `MuseumVR/Assets/Samples/XR Interaction Toolkit/2.6.4/XR Device Simulator/XR Device Simulator.prefab`
拖进场景按 Play（**临时用，别保存场景、别提交**）；主场景本身不含该 prefab。
桌面操作键位见 `docs/helmet-interaction.zh-CN.md`。

## 7. 与其它文档的关系

- 首次打开工程、GitHub Desktop 流程、XR 模拟器快捷键：[组员首次打开与日常协作](getting-started.zh-CN.md)
- 课程对 Phase 2 交付物的要求：[课程依据与团队约定](course-requirements.md)
- 协作规则（谁在什么时候可以写共享文件）：仓库根 [AGENTS.md](../AGENTS.md)
