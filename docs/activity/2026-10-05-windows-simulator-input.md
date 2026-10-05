# Windows 模拟器空格键误触修复

- 日期：2026-10-05
- 基线：`b40eb9f652a3935595575e6a5ec0f287c09777b5`；开始时 `main` 工作区干净，`git fetch origin --prune` 后与 `origin/main` 一致。
- 范围：官方 XR Device Simulator 的本项目导入版输入配置，以及入门操作说明。未修改场景、XR Rig、运行时代码或 Unity Packages。

## 发现与修改

用户报告 Windows 编辑器模拟时按空格等键后出现卡顿、偏移或类似卡键的现象。检查 `XR Device Simulator Controls.inputactions` 发现空格直接绑定 `Manipulate Right`。XRI 2.6.4 的 `XRDeviceSimulator` 在该动作按下时将右手加入当前操控对象，松开时移除；WASD 的作用对象随之改变，容易被误认为视角或移动发生偏移。该绑定并非跳跃键。

将 `Manipulate Right` 从空格改为右 Shift。原有 Y 键切换右手、T 键切换左手、U 键回到身体移动及 Esc 停止操控保持不变。操作说明增加了这些恢复步骤与 Game 窗口焦点提示。

## 验证与限制

- 修改后 JSON 可解析，`Manipulate Right` 仅绑定 `<Keyboard>/rightShift`，配置中不再有 `<Keyboard>/space`。
- `git diff --check` 通过；未触及场景和资产 GUID。
- 用户在自己的 Windows 编辑器中试用修改后反馈“好像确实没有之前的问题了”；未提供逐键步骤、帧率或日志，记为初步人工反馈，不把它扩展成所有输入路径的验收。
- Codex 本次未亲自运行 Unity Play 模式，也未复现窗口焦点切换或测量帧率；真实头显及完整按键回归仍未验证。后续可确认：空格不改变视角/右手位置，Y 与右 Shift 分别仍能切换/暂时操控右手，WASD 松键后停止移动。
