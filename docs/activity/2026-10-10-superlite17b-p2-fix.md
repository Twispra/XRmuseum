# SuperLite 17B P2 修复与复审交接（2026-10-10）

Ereshkigal1224 负责，Codex 辅助，关联 [#25](https://github.com/Twispra/XRmuseum/issues/25) 与 [PR #37](https://github.com/Twispra/XRmuseum/pull/37)。继续当前分支 `codex/superlite17b-details-2026-10-08`，base 为 `codex/helmet-refinement-assets`；开工 HEAD b6f61346bc0c0b34327157d8419af2e80cbf2ff1，工作树干净，已 fetch。修复资产提交 **6979e3718c28b599b03753ba27dffa6c824d4575**。

1. 侧阀新增独立 v4，两个下接头上移以衔接阀体；面窗 v3 保持当前交付。旧 v1/v2/v3 及原报告保留。新增求值后源与实际导出 GLB 的世界坐标三角表面交叉断言；旧 v3 预期失败退出 1，v4 通过退出 0。
2. 四份旧 v1/v2 GLB 仅将 Git 存储表示规范为 LFS 指针，上传对象；原始 Git blob 与修复后全新远程 checkout 展开的 SHA-256 全部相同。全新 checkout 本任务 14 个 LFS 资产已展开，指针检查退出 0，工作树干净。
3. 修正 2026-10-09 v2 活动记录的相对资料/QA 链接，更新本任务入口。PR 正文以当前面窗 v3/侧阀 v4 与本轮实际检查为准，保持 Draft。

[验证步骤、被测版本与限制](../../artifacts/helmet-refinement/eras/superlite-17b/qa/2026-10-10-p2-fix.md)、[v4 正面](../../artifacts/helmet-refinement/eras/superlite-17b/previews/SL17B_SideBlock_front_v4.png)、[v4 斜前](../../artifacts/helmet-refinement/eras/superlite-17b/previews/SL17B_SideBlock_threequarter_v4.png)。未新增外部资料或媒体，AI 辅助范围沿用 [资源记录](../external-resources.md) 的 SuperLite 17B 声明。资料检索继续暂停；原厂事实、照片观察、估计及未知项沿用现有资料目录。

等待另一成员复审后再讨论合入资产分支。未扩大到整盔主体、总装、Unity 或实机；网格相交不等于实物精度或机械配合验收。
