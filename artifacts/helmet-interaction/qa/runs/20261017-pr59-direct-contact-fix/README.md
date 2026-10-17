# PR #59：Direct 接触修复证据

被测工程提交 `4eb54903616b2199349276d7cb6978cf3eed3d77`，Unity 2022.3.62f3c1，保存 MuseumStart。

- `direct-report.json`：完整 31 项生产 Direct 断言、57 文件输入指纹，全部通过，捕获错误 0。
- `full-report.json`：原回归完整 807 项，全部通过，捕获错误 0；保留原断言。
- `direct-process.json`、`full-process.json`：各自真实 Editor 自然退出 0，不用 GUI 启动器返回值替代。
- `source-fingerprints.json`：退出后 1,180 个工程文件与运行前 SHA-256 零差。
- `negative-history.json`：三轮负向/夹具修正过程及原始报告哈希。第一次退出卡住被停止；后二次自然退出 1。它们不算通过或累加数量。

报告保留测试时 commit、dirty 和方法说明；后续文档/证据提交不改变已测工程字节。日志及更多诊断图留本地。Direct 不覆盖所有年代/朝向；全部结果均为模拟输入，不是实机、性能、构建或用户研究通过。详见[本轮活动](../../../../../docs/activity/2026-10-17-pr59-direct-fix-and-merge.md)。
