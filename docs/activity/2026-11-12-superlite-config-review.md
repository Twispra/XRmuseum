# 2026-11-12：SuperLite通信配置与后部附件首轮复核

用户说明头显暂不可用，授权先试做推荐的SuperLite专项复核并代为认领。以GitHub现有登录Ereshkigal1224在[#25认领子范围](https://github.com/Twispra/XRmuseum/issues/25#issuecomment-6813952399)，保留Fttt1003总体负责人及原贡献记录。

## 基准与范围

原工作目录 `D:/XR_documents/XRmuseum` 位于 `codex/superlite17b-unity-integration-2026-10-11` / `86715c9`；已有external-resources修改、未跟踪XRmuseum及模型/QA/活动/output目录均保留。fetch后main为 `adc06875103cc47a555dd251e3cf80d836a09558`，包含#72/#73/#74/#75。无待合并产品依赖；#71只作背景，未纳入本轮。工作在独立managed worktree，分支 `codex/issue25-superlite-config-review-2026-11-12`。

只新增本轮研究文档、三视、回读/查源清单与活动，并对资源登记作独立追加。没有修改保存的源模型、FBX、Unity、GUID、场景或设置。研究为当前会话单一写入；子代理仅独立查源与复审。没有运行历史生成器或安装器。

## 结果

[首版对照与下一步](../../artifacts/helmet-refinement/eras/superlite-17b/references/config-review-2026-11-12.zh-CN.md)交付：500-010研究目标与销售/可选配置区别、后配重—定位套筒—后铰片连接、现有v2简化点，以及两个可独立精修的小范围。新增核到通信手册#250114001和2006后铰片公告；未取得同配置整盔背视/尺寸，不把局部维修图当测绘。

## 本轮验证

被测产品为干净基准 `adc0687`；额外未提交文件只有本轮文档/图像/脚本及本地忽略诊断，无产品修改。Windows、Blender4.5.14 LTS；定向`git lfs checkout`展开源与两份FBX。以`--background --disable-autoexec --python-exit-code 1 --python artifacts/helmet-refinement/qa/20261112-superlite-config-review/review_saved_model.py`运行，不保存blend。源143/50268、FBX143，三资产前后SHA不变、两份FBX相同，进程自然退出0。[原始数值](../../artifacts/helmet-refinement/qa/20261112-superlite-config-review/model-readback.json)。首次脚本多余的当前文件library-load报错，移除后完整复跑；失败stdout留本地，不将其计作通过。

厂家PDF用Poppler查看相关9页，逐页检查与文字一致；原件和页面图只保留本地tmp。可提交的[来源/哈希清单](../../artifacts/helmet-refinement/qa/20261112-superlite-config-review/sources.json)不含厂家图像。自有模型三视及标注图逐张由Codex检查，属于AI视觉审阅，不是另一成员人工批准。资源与AI范围见[external-resources](../external-resources.md)。文档链接、diff及原目录保留检查见[本轮validation记录](../../artifacts/helmet-refinement/qa/20261112-superlite-config-review/validation.json)。[独立AI复核](../../artifacts/helmet-refinement/qa/20261112-superlite-config-review/independent-review.md)未发现技术阻断，收尾建议已落实；不解释为成员人工批准。

## 交接

第一阶段证据复核完成，尚未实施几何精修。本轮不关闭整个#25，不修改项目目标或共享规则，不更新汇总人的周索引。建议下一轮先做独立后配重/套筒/后铰片研究样件，尺寸保持估算，另登记文件范围再导出或接入。头显不可用；Unity运行、实机/性能/新构建、同件实物尺寸和用户研究均未验证。
