# 头盔交互验收记录

2026-09-23 原验收结果见 `playmode-validation.json`，对应完整日志 `playmode-editor-final.log` 留在原本机；JSON 已随源码保存。场景和资产导入尺寸见 `setup.json`。原静态渲染位于 `../preview/`，这些均作为历史证据保留。

原报告未采集 Git 版本；来自有未提交 `ProjectSettings.asset` 差异的工作区，不能回填一个 SHA 冒充当时已绑定版本。后来新增的验证运行见 `runs/`，各次结果是否适用于当前代码需看其版本、哈希和限制，不能只取文件日期最新的一份。

PR #1 的修复代码版本为 `f3548158e1a844e2cf7687ace0acc5db969ee387`；对应的 90 项检查和两张预览由后续提交 `880dd83` 保存，并随 PR #2 纳入 `runs/`。请从[修复记录与固定版本证据](../../../docs/activity/2026-09-23-pr1-review-fixes.md)查看，不要把根目录的旧报告当作新版验收结果。

测试使用项目中已有的输入动作、ActionBasedController、直接抓取器、XRGrabInteractable 和物理系统。合成 XR 设备事件经过正常玩家输入更新，不直接调用选择 API。Home 使用独立虚拟键盘；RESET 按钮通过控制器抓握选中，调用场景序列化事件。测试不会保存 Play Mode 中的场景变化。

验收包括十次拿起、旋转、放回、松手后的稳定性和穿透检查；每轮还要求自动复位次数为零，防止复位掩盖放置失败。另检查抓取中 Home、松开后的再次抓取、自由状态 Home、清除旧速度、RESET 按钮和掉落复位。

新版在原 89 项基础上增加“头盔可见网格已有效导入”，共 90 项。原因是一次新 worktree 只展开了 Git 文件、未展开 LFS 资产，物理碰撞体仍能通过而视觉预览失败。新检查在进入拿放循环前拒绝这种情况；先确认 LFS 资产完整，再打开 Unity 导入。

## 测试环境修正

- 无 Game 画面的批处理可达到每秒数千帧。本项目 XRI 2.6.4 在帧间隔小于 1 ms 时跳过投掷速度采样，间歇采样会产生异常松手速度。批处理测试现按约 90 Hz 节奏运行；没有因此关闭产品投掷或放宽通过标准。`diagnostic-uncapped-input.json` 保留原始异常样本。
- 后台窗口曾禁用预先创建的虚拟键盘。测试先设置后台输入行为、显式启用测试设备，再在玩家 Dynamic 输入更新中排队事件。`diagnostic-editor-keyboard.json` 和 `input-probe.json` 是定位过程记录。
- 这是模拟交互验收，不是 VR 帧率测试。真实头显追踪、延迟、控制器按键手感，以及官方模拟器的整套键鼠导航仍需人工检查。

## 重跑

关闭同一项目的其他 Unity Editor 实例后，用 Unity 2022.3.62f3c1 启动。以下为参数格式，替换成自己的绝对路径，不要照抄其他成员盘符：

```text
"<Unity安装目录>/Unity.exe" -batchmode -projectPath "<仓库绝对路径>/MuseumVR" -executeMethod Museum.Editor.HelmetPrototypeValidation.RunBatch -logFile "<本机日志目录>/<本次运行名>.log"
```

不要添加 `-quit`：脚本需等 Play Mode 验收完成、返回 Edit Mode 并导出静态预览后才请求退出。不要添加 `-nographics`，预览需要图形设备。也可在 Unity 菜单运行 `Museum > Validate Helmet Interaction (Play Mode)`；交互验收结束后回到 Edit Mode。

本轮干净 worktree 的报告和预览已完成，但 Unity 在本机原生退出阶段未自行结束，收集证据后终止了对应独立进程。脚本会请求退出；目前不能保证该环境的批处理正常返回。未来接 CI 时必须验证退出并配置合理超时，不能只凭 JSON 的 `passed` 宣称整个命令成功。详见[修复记录与验收限制](../../../docs/activity/2026-09-23-pr1-review-fixes.md)及[协作验收记录](../../../docs/activity/2026-09-23-team-collaboration.md)。

新报告写入 `runs/<UTC时间-唯一标识>/playmode-validation.json`；批量验收成功后的截图和 `capture.json` 写同一目录下 `preview/`。独立调用 `HelmetPrototypePreview.Capture` 也生成唯一目录。指定已有预览输出文件的目录会被拒绝，不能用复测覆写原验收图。

运行开始前采集 commit、branch、`git status --porcelain`、Unity 版本及关键 packages、场景、FBX、Prefab、脚本和输入配置共 14 个文件的 SHA256，跨 Play Mode 重载保存后写入报告。Git 不可用、目录不对或查询超时时明确为 `unknown`。记录说明“开始时状态”，不证明运行中没有外部修改；测试期间仍须保证工作区独占。

提交有用的运行证据及活动摘要，不批量提交每次失败的庞大临时日志。初始化工具无需重跑；它现在会拒绝覆盖现有头盔资产。
