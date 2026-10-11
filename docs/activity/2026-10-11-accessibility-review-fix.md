# 2026-10-11：PR #41 可访问性走查审查修复

用户授权修复 [PR #41](https://github.com/Twispra/XRmuseum/pull/41) 的审查问题后重新核对。
本轮只有脚本、说明和证据变化，不修改产品，不启动 Unity，不把静态证据当作实机或参与者验收。

## 版本与范围

- PR 原 head：`5fd765dd75f4894adce605bc3ba463c255eacc2a`。
- 已 fetch 的主线：`a8da4e884964e8a95b8d1a6ef61fc1faf8802675`，含已合并 #40 / #43。
- 在独立分支 `codex/pr41-review-fix-2026-10-11` 正常合并主线，得到 `5ced5d155c86e53f9a1d0f06a623162b15a9933d`；没有重写历史。
- 被测组合：上述合并提交，加本轮未提交的审计脚本、`docs/accessibility.zh-CN.md`、旧活动记录顶部的更正入口、本文及新运行目录中的验证脚本。
  具体 dirty 路径、产品树和审计脚本 SHA-256 记在新报告的 `provenance`。
- 保留原 checkout 的未知改动；本轮在仓库外独立 worktree 执行。`MuseumVR/` 与该主线同树。
- 不修改 `project-state`、`external-resources`、`weekly-activity` 或已经合并的 #40 脚本。

## 修复内容

1. **Hover 不是输入**：`allowHoveredActivate` 允许向未 Select、已 Hover 的目标发送 Activate 事件；
   XRI 2.6.4 的条件仍包含 `activateInteractionState.activatedThisFrame`。热点 `OnHoverEntered` 只换色，
   Select / Activate 回调才访问热点。删除“悬停即可激活”“不必按扳机”及由此推导的省力、误触结论。
2. **材质 Color 与最终像素区分**：Color 属性以 sRGB 提供，进 Linear shader 时才转换。
   删除“序列化材质值就是线性”和“Unity 忠实解释”；只保留明确假设下的配色估算，
   不把 URP Lit 语言板的基色估算当成最终对比度通过。估算结果为语言板 16.63:1、热点正文 17.75:1、完成提示 15.07:1。
3. **按 alpha / blend / shader 区分展示阶段**：HotspotPanel 基色 alpha=1、无 BaseMap，
   GUID 与 URP 14.0.12 `Unlit.shader.meta` 一致，shader 默认白纹理，fragment alpha 为 `texColor.a * _BaseColor.a`。
   `_SrcBlend=5` / `_DstBlend=10` 为 SrcAlpha / OneMinusSrcAlpha。重新选中后 hold 30 秒覆盖后景，
   fade 1 秒才同时降低背板和文字 alpha。修正原来只凭 `_Surface=1` 判定一直透景的推论及对应建议。
4. 审计脚本采集上述材质字段、包源码相对路径 / SHA-256、Activate 原条件和 hover 回调；
   可选 `--package-cache` 只读取 manifest 指定版本。缺缓存时明确标为未核对，不猜 shader。
5. `--out` 必填，已有文件一律拒绝覆盖；保留旧报告原字节。stdout/stderr 使用无法编码字符的转义回退。
   顺便把原来的年代舒适性“通过”收敛为对象材质淡变 / 所列运动 API 的静态证据；0.35 是颜色乘数而不是全画面亮度。

## 实际验证

Windows / Python 3.12.10，使用已有本机缓存 URP 14.0.12、XRI 2.6.4；未启动 Unity。
新报告使用以下命令生成（缓存路径按本机替换；输出必须是新文件）：

```powershell
python artifacts/accessibility/qa/runs/20261010-accessibility-audit/audit_accessibility.py --package-cache E:\XRmuseum\MuseumVR\Library\PackageCache --out artifacts/accessibility/qa/runs/20261011-accessibility-review-fix/report.json
python artifacts/accessibility/qa/runs/20261011-accessibility-review-fix/verify_review_fix.py
```

- 审计退出 0，20 项观测、5 条未验证项；8 prefab、3 场景、69 材质、92 TMP / 全部自动缩放、21 个低下限、10 处字号覆盖及面板尺寸复现。
- 针对检查验证未修改观测值、条件配色数值、shader GUID / Color 声明 / 默认白纹理 / blend 声明 / fragment alpha、
  XRI Activate 输入条件、hover 回调、30+1 秒 / 复选恢复 opacity、旧报告字节不变和产品树相同。
- 验证已有报告路径被拒绝（退出 1、哈希未变化），缺 `--out` 被拒绝（退出 2）。
- 新 clone 没有包缓存的路径也独立运行：仍产出 20 项，shader / XRI 源码明确标为未核对。
- `git diff --check` 通过。新报告关键词统计包含当前走查和新增活动文件，数值不要求继续为历史 15。

当前证据：[新报告](../../artifacts/accessibility/qa/runs/20261011-accessibility-review-fix/report.json)、
[针对验证脚本](../../artifacts/accessibility/qa/runs/20261011-accessibility-review-fix/verify_review_fix.py)、
[验证结果](../../artifacts/accessibility/qa/runs/20261011-accessibility-review-fix/verification.json)。
旧 `20261010-accessibility-audit/report.json` 保留历史内容；旧活动记录只增加更正入口。

## 限制与交接

三项误判已修正；产品可读性、完整 TMP 渲染路径、Lit 光照 / 雾 / 后处理、hold/fade 的最终像素对比度、
真实设备输入、触达、舒适性和参与者研究仍未验证。LFS 在此 worktree 只为文本审计使用，没有打开工程或把指针当作资产验收。
没有新增第三方资源或包；脚本、文字修订和证据由 Codex 在本次授权范围内辅助完成，不能代替成员审批。
分支提交后由当前审查协调人处理 PR 更新与最终复审；本轮不 push、不发评论、不合并 PR。
