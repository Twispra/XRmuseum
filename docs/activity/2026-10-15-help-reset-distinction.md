# Help 中区分头盔复位与视角重置（2026-10-15）

## 目标与修改

基于已经包含 #51 视角重置与 #52 Physical Hands 的最新 `main`，继续完善访客 Help。观察页的 `Reset` 只把头盔放回原位；面板上的 `Reset View` 用于把访客视角带回起点。两者用途不同，原 Help 只解释了头盔复位，容易在视角卡住时选错。

这次在 Unity Editor 模拟器 Help 与 PC VR Help 中分别写明两个入口的用途，并同步更新英文、简体、繁体的 JSON 维护源和 Unity 保存的翻译表。没有改场景、Prefab、输入绑定、头盔模型或视角重置逻辑。

## 验证

- Unity 2022.3.62f3c1 已在本分支工程完成导入和脚本编译；`Editor.log` 未发现 C# 编译错误。
- JSON 解析通过；两个 Help 条目的三语内容在 `MuseumTranslations.json` 与 `LocalizationTable.asset` 中逐项一致；`git diff --check` 通过。
- 在已有的 `VisitorViewRecoveryValidation` 中增加三语检查：打开编辑器 Help 后检查两个 Reset 的说明与正文溢出，并检查保存的 VR Help 文案。原有的射线选择、进度保留和 Reset View 入口检查继续保留，没有另造一套重复测试。
- 命令行 Unity 验证在当前机器的 macOS 服务连接阶段停住，未生成通过报告。因此上述新增运行时断言尚不能记为通过；合并前应在 Unity Editor 运行 `Museum/Exploration/Validate visitor view recovery` 并核对报告。

## 限制

没有可用的 VR 头显；PC VR Help 的手柄文案和头显内布局还需要设备验证。此次不是用户测试，也不代表 VR 构建通过。
