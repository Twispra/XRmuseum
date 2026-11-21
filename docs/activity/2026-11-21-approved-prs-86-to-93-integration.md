# 2026-11-21 六项已批准 PR 的联合验证与合并交接

按用户授权，从远端 `main@5ddaab5a` 隔离整合 [#86](https://github.com/Twispra/XRmuseum/pull/86)、[#89](https://github.com/Twispra/XRmuseum/pull/89)、[#90](https://github.com/Twispra/XRmuseum/pull/90)、[#91](https://github.com/Twispra/XRmuseum/pull/91)、[#93](https://github.com/Twispra/XRmuseum/pull/93)，最后整合 [#88](https://github.com/Twispra/XRmuseum/pull/88)。六项已有 GitHub 批准。合并状态以各 PR 的远端回读为准。

由本轮整合工作树统一处理共享三语表、字符清单和字体图集。保留全部 306 条译文及 653 个保存字形，含「台、央、往、幕」。保留原字体、图集及材质身份和源 TTF，合并资源声明。范围、字体证据及静态验证见[字体整合记录](2026-11-21-navigation-narration-font-merge.md)。导航使用自己的面板，Editor 验收据此区分旧工具，并在既有的可选旧面板夹具结束后恢复导航面板。

## 联合验收

Windows / Unity 2022.3.62f3c1 / Win64 编辑器，以集成提交 `48bde5c3969d8ce5ec39a890dbb6d738fa95282f` 验证。用真实项目保存场景及自动模拟输入。每个进程自然退出 0，1,464 个项目追踪文件的 SHA-256 前后零差，43 个展开的项目 LFS 文件核对通过。

| 验证入口 | 结果 |
| --- | --- |
| `Museum.Diegetic.Editor.HelmetDiegeticValidation.RunNarrationBatch` | 328/328，通过大字切换、播完保留、收起与六条新音轨 |
| `Museum.Diegetic.Editor.HelmetDiegeticValidation.RunVisitRecapBatch` | 96/96，通过小结实时更新、清空及三语 |
| `Museum.Navigation.Editor.GalleryNavigationValidation.RunBatch` | `MuseumStart`、`DuskWorkshop` 各 145/145，包含小结阻止导航、淡出取消及离开时收起字幕 |
| `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch` | 142,805/142,805，验证默认实体站、可选旧面板的握把／扳机与恢复 |

静态三语 11/11，离线日志分析自测 38/38。导航首次启动因启动日志与验证输出使用同一目录退出，改用独立新目录后从相同源码复跑通过。三语大字和保留字幕的单眼诊断图已复核，其镜头未同时覆盖跟随导航面板。真实头显、双眼字幕和导航同框的可读性、Windows Player 构建和参与者评价待验证。
