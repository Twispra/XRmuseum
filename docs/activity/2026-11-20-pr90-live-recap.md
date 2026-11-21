# 2026-11-20 参观小结实时同步

在原 [#90](https://github.com/Twispra/XRmuseum/pull/90) 的 `e4a303fa` 中，中央展台小结打开后，访客仍可用射线扳机查看知识热点。新的部位会计入参观记录，小结则要重开才更新。

小结打开时订阅当前展品的观察变更，关闭时解绑。首次观察和清空记录会刷新可见的部位数和名称。重复查看同一编号保留首次记录，原有射线热点和继续／开始新参观的流程照常工作。运行时不需要按帧查找热点。

在 Windows 的 Unity 2022.3.62f3c1 中，以 Win64 为 Editor 目标载入保存的 `MuseumStart`，运行 `Museum.Diegetic.Editor.HelmetDiegeticValidation.RunVisitRecapBatch`。被测提交 `8482d8dff3284e2d081cd9f4c054bcf21938ce06`，运行前工作树干净。小结保持打开时，模拟手柄扳机触发一次热点激活，未触发选择。小结从 0/3 更新为 1/3 并显示头盔壳体，清除记录后当场回到 0/3。专项共 96 项通过，Editor 自然退出 0。开跑前 43 个 Git LFS 工程文件均已展开并核对大小及 SHA-256，退出后 1,448 个受跟踪工程文件哈希一致。

.NET 10 的独立方法夹具同时确认重复观察、关闭解绑和再次打开。简体、繁体和英文的三张单眼诊断画面已复核。真实头显、实物控制器触达、Windows Player 构建和参与者评价待验证。[专项报告与验证摘要](../../artifacts/helmet-interaction/qa/runs/20261120-pr90-live-recap/)
