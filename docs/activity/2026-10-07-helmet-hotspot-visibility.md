# 2026-10-07：头盔热点显隐与说明淡出

本轮最终基于 `origin/main` 的 `afc3a9e56734d3c7230adb5b81a5dcdd7ddd016e`，在独立分支 `codex/helmet-hotspot-visibility` 修改头盔热点功能。原工作目录中的未知 Unity 设置改动与未跟踪文件未被修改或纳入；开发中发现远程主线前进后，先将未提交功能迁移到该最新基准，再重新验收。

热点标记默认隐藏；任意已启用的 XR Ray Interactor 命中头盔模型或隐藏热点碰撞体时，三个标记共同显示，射线离开后隐藏。热点碰撞体保持启用，因此隐藏状态仍可被射线发现和选择。信息框在每次有效热点选择后重新显示并计时，保持 30 秒后用 1 秒淡出；再次选择同一或其他热点会恢复并重新计时。信息框材质改为 URP 透明表面，Builder 同步生成相同行为。

Windows / Unity 2022.3.62f3c1 下基于上述最新主线运行 `Museum.Hotspots.Tests`，EditMode 与 PlayMode 共 15 项通过，结果位于隔离工作区的忽略目录 `tmp/helmet-hotspot-visibility-tests/latest-main-results.xml`。将同一文件集复制回原工作目录后，再次启动批处理 Unity 时进程在生成日志前以 `-1073741515` 退出，不能算作第二次通过；测试前备份的 5 个原有 XR / ProjectSettings 本地改动哈希全部未变。未做真实头显、人工视觉舒适性、Player 构建或用户研究；本轮不直接推送远程 `main`，通过独立远程分支与 PR 交付。
