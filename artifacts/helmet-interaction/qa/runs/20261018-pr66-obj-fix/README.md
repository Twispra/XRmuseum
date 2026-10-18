# PR #66：OBJ 修复证据

最终被测产品提交：`d9c40a095bc3ba86dc9e44e1eb2952e57c4424e8`；基线 `da52091d1c0ad46abb717a89e10a3afd1facce3a`。

- `static-validation.json`：34 项静态检查，含旧 OBJ 的 18,159 面越界负向控制。
- `geometry-integrity.json`：GLB 与保存 OBJ 的 117,304 面和边界对照（最大边界差 `3.573e-9`），两张 1024² 贴图与 GLB 原图的解码像素完全一致。
- `validate-export.py`：从仓库根目录运行的复查脚本。使用独立 Python 环境安装 NumPy、Pillow、`trimesh==5.1.1`；原提交的 LFS details OBJ 也需在对象库中，可用 `git lfs fetch origin da52091d1c0ad46abb717a89e10a3afd1facce3a --include="MuseumVR/Assets/Museum/Features/PhotoHelmetTrial/Models/*.obj"` 获取。
- `first-import/`：首次实际导入和 Play 检查的完整断言报告、进程记录及源码哈希。Unity 退出 0，但八个材质被规范化，所以文件守卫拒绝本轮。
- `material-normalization.json`：这八行兼容 `_Color` 值的审查；其他序列化字段及 `_BaseColor` 未变。
- `final/`：规范化后重新强制同步导入并验证，318 项通过，报告无错误／警告，自然退出 0；1,253 个已跟踪工程文件哈希零差。六张 JPG 为实际 Unity 渲染，`comparison-contact-sheet.jpg` 仅把它们缩放排成总览。

环境：Windows，Unity 2022.3.62f3c1，batchmode 保留图形设备，合成键盘输入。独立 E 盘 Library 复用已关闭的基础缓存；PhotoHelmetTrial 在基础缓存中不存在。本次读取保存场景和 Prefab，未运行生成器。原始完整日志留在本地，路径和哈希见各 `run-summary.json`。

桌面结果的适用范围与剩余设备／构建／历史资料检查见 [活动记录](../../../../../docs/activity/2026-10-18-pr66-obj-fix.md)。
