# PR #19 主线整合验收证据

被测提交 `2fcaad12d79bf65f39ab6e27b28763818fcfc94f`，main 基准 `18d7f8a09c29bb0c757b8a80bdc245b0fe40b785`；Windows、Unity 2022.3.62f3c1、Direct3D 11。六组最终运行均退出 0。被测期间没有 tracked dirty；原始报告的 dirty 字段列出的均为先前及当前运行产生的未跟踪 QA 文件。

- [汇总](validation-summary.json)、[实际进程与完整参数](processes.json)、[267 个被测输入哈希](tested-files.json)。单项 process 文件保留实际 PID、起止时间、退出码与本地日志 SHA256。
- [体验 76 项](experience-checks.json)、[原操作 206 项](inspection-checks.json)、[探索 1486 项](exploration-checks.json)、[主场景 84346 次断言分组与样例](main-checks.json)、[模块 11 项](module-report.json)、[热点 EditMode 12 项](hotspot-tests.xml)。主场景大多是逐文本/字形重复断言，不能解释为独立测试用例数。
- [热点生产输入 768 项](hotspot-interaction.json)含 20 次 Direct/Ray 选择与[其中 322 次平滑返回共存断言](cross-module-checks.json)，已包含在主场景总数，不重复计数。
- [逐键/字形合并](localization-merge.json)、[静态范围检查](static-preflight.json)、[Unity 字体烘焙与持久身份/像素验证](font-report.json)。205 条表记录；526 个实际所需字符；536 个保存字形；源字体 cmap 7660。保留 main 202 条记录，追加源分支 3 条 key。
- [首次与第二次失败摘录](failed-runs.json)：分别为截图阻塞后窗口/复位动画起点的旧帧时间，原进程实际退出 1。两处测试协程增加帧等待后最终运行通过，产品代码、原断言和时长均未放宽。

完整原始报告和日志仍在本机各独立运行目录，未覆盖历史文件。JSON 中 originalReport 是本地来源路径，originalReportSha256 用于追溯；此目录提交的是有用摘录与选定截图，未上传完整大体积重复报告、临时日志或字体备份。

Codex 已查看本目录全部 8 张最终截图：后部左电话/右供气、打开前窗、英文/简体/繁体界面、演示及恢复状态。近处菜单遮挡、画面右缘裁切与演示/入口重叠仍可见；通过文字边界检查不代表任意观察位置均无遮挡。截图为单眼诊断/生产相机取景，无真实头显、人工声音试听、参与者/语言审查、舒适性、性能测量或 Player 构建结果。
