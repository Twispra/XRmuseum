# 2026-10-10：SuperLite 17B 独立资产主线整合

## 授权、来源与范围

维护者明确要求将 #37 这批独立资产进入 main。本轮由 Codex 执行整合与技术验证，原资产贡献归属沿用 Ereshkigal1224 的既有记录，不冒充另一位成员的人工批准。整合入口为 [PR #39](https://github.com/Twispra/XRmuseum/pull/39)；合并是否完成和主线 SHA 以该 PR 实际状态为准。

- 实际主线基线：`270c7b72da63f08baac85ee7bb94f7fcdb4a226d`，已包含 #35／#36。
- #37 原头：`7ebb64078d1e6a7b40bc43938b2e537bac3f7fdb`；已合入资产分支的提交：`169c643cac3c94d12f8c1cad75894bc2db2c9ab9`。
- 独立整合分支：`codex/integrate-superlite37-main-2026-10-10`。合并验证提交：`6459f081447748716fd200648182d7db903dc04e`，两个父提交精确为上述主线和资产分支。
- 仅归档 SuperLite 目录 62 文件、5 份历史活动记录，并整合资源声明／状态快照。本轮独立 QA 与活动记录追加提交，不覆盖旧报告或改写作者历史。
- 当前交付为面窗 v3、侧阀 v4 等独立分件。不是整盔装配完成，也没有添加 Unity 副本／meta／GUID。#28 草稿和 #38 构建说明不在本轮范围。

## 处理与边界

复用干净的已管理验收 worktree，在实际主线建立独立分支，以非 fast-forward merge 保留原作者历史。仅 `docs/external-resources.md` 有两处记录冲突；保留主线已有供气／通话、Mark V／Unity 整合段落，同时保留 SuperLite 四组来源与历史限制，再追加本轮 AI 范围。状态文档只在顶部增加新快照，旧日期段落不重写。

`MuseumVR`、`artifacts/helmet-refinement/eras/mark-v` 和 `artifacts/connection-demo-refinement` 与主线基线的 Git 子树完全一致；SuperLite 子树 `56d192c8f901b7339c234438c4987c49ae74d0d1` 与资产分支完全一致。具体树哈希和范围见[范围报告](../../artifacts/helmet-interaction/qa/runs/20261010-superlite-main-integration/integration-scope.json)。

原工作区 `E:/XRmuseum` 留在原本的本地 main `b4e1886c`，既有 ProjectSettings 改动和未跟踪文件原样保留，不 stash、清理、强推或批量暂存。原 ProjectSettings SHA256 为 `81c0eb3103e303a2567619e388c45709e89fa1940353cdfe4a163bbe402c5587`。此前本地主线更新已受到两份既有未跟踪 QA 文件阻挡，本次不会移动或覆盖它们。

## 本轮验证

被测 commit 为 `6459f081447748716fd200648182d7db903dc04e`；整合 worktree 在资产测试前后均无额外未提交／未跟踪文件。Windows、本地 Node v24.19.0、Three.js 0.170.0 与 three-mesh-bvh 0.8.3；验证库仅安装在项目外临时工具目录，不提交 node_modules 或新增 Unity／产品依赖。

- 合并冲突解决并提交后，`git -c http.version=HTTP/1.1 lfs pull origin --include='artifacts/helmet-refinement/eras/superlite-17b/**' --exclude=''` 退出 0。冲突未解决时的首次 LFS 调用曾报告 index needs merge，不作为通过证据。
- `git lfs fsck --pointers HEAD` 退出 0，结果为 `Git LFS fsck OK`。
- 新独立检查器读取实际展开的 7 份 blend 和 7 份 GLB，14 个文件的实体头、长度和 SHA256 与当前提交 LFS 指针一致。4 个历史 v1／v2 GLB 与修复前原始 Git 字节一致。
- 使用实际 GLB 顶点／索引、世界空间变换和三角面相交，而不只是包围盒：v3 两下接头负回归均可检出未连接；v4 供气下接头与主体及左套环相交，弯管下接头与主体及右套环相交，正回归通过。
- 检查导出的 35 个网格、UV、无 REVIEW_ONLY 节点、无外部图片 URI；独立检查器退出 0，报告 `passed=true`、测试前后工作区干净。见[实际输出报告](../../artifacts/helmet-interaction/qa/runs/20261010-superlite-main-integration/independent-glb-and-lfs.json)。
- 两个独立 AI 子任务只读审查真实 diff、树哈希、冲突合并和证据措辞，未发现技术阻塞。它们不是 GitHub 成员 APPROVE。最终追加证据提交还需核对资产子树未变化、最终头和远程合并条件；结果在 PR #39 交接段记录。
- `git diff --check` 与范围检查通过。追加 QA／活动记录不修改被测 SuperLite、Unity、Mark V 或设备内容，不能因此把其他未执行的测试写成完成。

[便携检查器](../../artifacts/helmet-interaction/qa/runs/20261010-superlite-main-integration/verify-exports.mjs) 接受干净 worktree、全 SHA、唯一输出路径与外部工具目录，不覆盖已有报告。外部目录需先安装固定版本 `three@0.170.0`、`three-mesh-bvh@0.8.3`（均为 MIT，工具本体不进仓库）。复验命令：

```powershell
node artifacts/helmet-interaction/qa/runs/20261010-superlite-main-integration/verify-exports.mjs <clean-worktree> <new-report-outside-worktree.json> <full-tested-commit> <local-tools-directory>
git lfs fsck --pointers HEAD
```

## 未验证、后续与交接

本轮没有执行 Blender 源文件回读，作者历史报告只作为既有证据；未运行 Unity、真实头显、可执行构建、性能测试或用户研究。局部连接测试证明导出几何修正，不证明制造公差、整盔装配或精密历史复原。课程 AI 许可仍需有权限的成员确认。

下一步最小动作是完成本轮证据提交后核对 PR #39 最终头，以现有 GitHub 权限和保护执行正常合并，不修改保护，不伪造成员批准。资产入主线后如需 Unity 接入，应另行认领任务、协调共享场景／Prefab／源文件；不能重跑原型生成器覆盖现有产品成果。#38 另有资源登记改动，由其负责人后续基于新 main 核对。本次不关闭尚未完成全部年代精修的 #25，不代写成员反思或周报索引。
