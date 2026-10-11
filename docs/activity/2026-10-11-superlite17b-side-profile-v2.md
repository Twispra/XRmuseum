# 2026-10-11：SuperLite 17B 侧面比例修正

用户明确要求对照 `04_side_rear_view.png` 修正 Blender 模型的错误比例。Codex 在同一任务分支完成独立 v2，保留旧文件与所有历史证据；不推断成员贡献或团队批准。

## 基准与范围

分支 `codex/superlite17b-whole-helmet-2026-10-11`，HEAD `ac3d31cb2db37640cf784f3447ca90d5d517b55a`。本轮开工记录了分支、HEAD 和状态，读取项目状态、协作指南及相关头盔交付说明；fetch origin 成功，origin/main 仍为同一 HEAD。没有提交、推送、合并或发布远程消息。

开工已有 `M docs/external-resources.md`、`?? artifacts/helmet-modeling/`、`?? docs/activity/2026-10-11-superlite17b-whole-helmet.md`，为上一轮本任务生成内容；`?? XRmuseum/` 为未知嵌套目录，未修改。原第一版 Blender 文件已经与上一轮 QA 哈希不同，本轮从头到尾保留 SHA256 `5bf1c30e1f8b848745e76e95111d407da110a80936484e920ee248d0154ca841`，不覆盖用户保存状态。

修改限定在 `artifacts/helmet-modeling/superlite17b-2026-10-11/revision-side-profile-v2/`、版本入口 README、本活动记录、外部资源追加记录与一个新 QA 运行目录。未认领或修改共享 Unity 场景、Prefab、Packages、ProjectSettings、既有面窗/侧阀件或旧原型生成脚本。此轮依据用户新给出的侧面照片执行，无新增 Issue/PR。

## 结果与证据

[v2 交付报告](../../artifacts/helmet-modeling/superlite17b-2026-10-11/revision-side-profile-v2/REPORT.md)记录壳体、斜颈圈、贴壳配重、宽头带、低拱提手、侧阀和排气罩改动；[前后对比](../../artifacts/helmet-modeling/superlite17b-2026-10-11/revision-side-profile-v2/comparison/proportions-before-after.png)由实际保存文件在同一相机下渲染。几何仍为基于照片的估计，约 30.1° 颈圈角度不是实测数据。

被测版本是上述 HEAD 加上本轮未提交的 v2 脚本/资产。Windows、本机 Blender 4.5.14 LTS（62c1db4208e8）；脚本 SHA256 `991c4374ff197da4e9aeeb2f13ea4a86f3429349be20ff2f7c3dc7b771412fbe`，终稿 SHA256 `54a21895b807a19ccc0345a25101cb5369c0a9c976d175a40a58a528a5b8b905`。

操作与结果：

1. `blender -b -t 6 --python-exit-code 1 -P <v2>/build_helmet.py -- --output <v2>/delivery`：独立构建保存，六阶段五视图输出完成，143 网格、50,268 三角面。临时草稿与失败输出留在忽略目录 tmp。
2. `blender -b --python-exit-code 1 -P <v2>/check_model.py -- <终稿.blend> <新运行目录>/source-readback.json <草稿回读.json>`：实际保存源回读质量通过，与同脚本草稿几何完全一致；未用生成过程的输出替代保存源检查。
3. `blender -b --python-exit-code 1 -P <v2>/check_side_profile.py -- <终稿.blend> <新运行目录>/side-profile.json`：五项指定改动检查通过；同一脚本对保留旧源的检查五项不通过，作为受影响几何的回归对照。
4. `blender -b --python-exit-code 1 -P <v2>/render_comparison.py -- <保留旧源.blend> <终稿.blend> <v2>/comparison`：回读两份实际文件完成渲染，无保存操作；Pillow 合成中文标注对比图。查看终稿四个方向与对比输出。

自动检查记录位于独立 [qa/runs/2026-10-11-superlite17b-profile-v2](../../artifacts/helmet-interaction/qa/runs/2026-10-11-superlite17b-profile-v2/)，没有覆盖已引用历史报告。仅证明网格质量、改动存在、保存一致，不能证明照片精确匹配、实物测绘、人工成员验收或性能。

## 限制与交接

交付在本地，没有 FBX/GLB、Unity 导入、真实头显、可执行构建或用户研究结果。原图仅本地观察，仓库保存来源哈希；新增程序几何/材质和检查由 Codex 辅助。下一步是打开 v2 进行外观审阅；实际尺度校准和 XR 整合另按项目协作范围执行。周索引与总状态由约定汇总人维护。
