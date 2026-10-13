# 2026-10-13：PR 基础静态检查

我选择先补一套不依赖 Unity 的仓库检查，让小组在提交 PR 时更容易发现翻译表、资源 `.meta` 和文档链接的基础问题。Codex 辅助实现、测试和整理此记录；最终是否启用为必需检查，由小组审阅后决定。

## 范围与基准

- 基于 `origin/main` @ `37930ae235b6bfd00f9575805c759614a0cec6a4` 建立独立分支 `codex/static-repo-checks-2026-10-13`；开工时工作副本干净。
- 新增 `.github/workflows/static-checks.yml`、`scripts/check_static_repository.py`、独立单元测试和[使用说明](../static-checks.zh-CN.md)。不修改 Unity 场景、Prefab、模型、翻译内容、Packages 或 ProjectSettings。
- 工作流只使用官方 `actions/checkout`、`actions/setup-python` 和 Python 标准库；不下载 Git LFS 文件，也不引入 Unity 包或运行时依赖。

## 本地验证

- Python 单元测试 5 项通过，覆盖翻译字段和重复 key、缺失 `.meta` 与 `.gitkeep` 例外、损坏的文档相对链接。
- 在当前主线内容上完整扫描：翻译表、Unity `.meta` 配对和 111 份已跟踪 Markdown 的相对链接，0 项错误；加入本轮两个新文档后复测 113 份，仍为 0 项错误。
- Git 差异和新文件清单另在提交前核对；本轮未在 GitHub Actions、Unity、真实头显或 Windows 构建环境运行，不能将本地通过写作远程 CI 或产品验收通过。

## 交接

小组审阅工作流权限、检查边界及首次远程运行结果后，再决定是否设为分支保护的必需检查。若某项检查对历史文件误报，先确认实际路径或规则，不通过跳过整个工作流来掩盖资源问题。
