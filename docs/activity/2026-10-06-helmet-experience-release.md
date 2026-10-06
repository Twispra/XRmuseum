# 头盔体验完善：发布与接手

已上传 [PR #19](https://github.com/Twispra/XRmuseum/pull/19)，等待另一位成员审查，尚未合并。产品提交 `484c5d47ae6ce36aa94bfd703d36bf1290f917de`，分支 `codex/helmet-experience-polish-2026-10-06`。本文件及发布核对是随后补充的文档，不改变被测代码／资产。

## 这次交付

在原 MuseumStart 中完善前窗玻璃、开合及可关闭声音；切口根据已有金属网格生成并保留空腔；供气／通话先显示发送端动作，再沿连接传输，到达后突出接收端；手动复位有短返回动画，安全恢复仍即时。背部新增电话鹅颈：**从背后看左电话、右供气**。通话电缆、收到消息和回复对应电话实体出口，供气使用另一只接头，两者随头盔位置和旋转更新。

接头尺寸、响应环、程序声音、颜色和动画速度是展示原型选择。背部左右分工有博物馆资料依据，不能把程序几何当成实物测绘。原 FBX 没有重导出；新增结构保存在 Prefab 和独立 Mesh asset 中。

## 组员怎样打开和复查

先保存自己的改动并关闭 Unity。没有重叠未提交改动的工作副本可 Fetch 后选择上述远程功能分支；GitHub Desktop 用当前分支菜单选择它。不要在有未知改动的工作区强制切换／拉取。新 clone 要先展开 Git LFS 文件，Unity Hub 选择仓库中的 `MuseumVR`，版本 **2022.3.62f3c1**。

1. 直接打开 `Assets/Museum/Scenes/MuseumStart.unity`，进入 Play，点击 Game 获取键盘焦点。不要运行旧 Builder，也不需要运行本轮 Setup 迁移工具。
2. T／Y 选择手柄，射线对准实体按钮，**短按 G 并松开**；鼠标左键是 Trigger，不是这些按钮的 Select。
3. 打开前窗，观察玻璃与停靠动作；Help 的 Sound 可静音。打开 Section，检查不同切面和前窗变化时切口与空腔。
4. 观察 → 更多工具 → 向右转六次（每次 30°）看背部，或直接握持整盔旋转。Explore → Air 应到供气出口；Voice 应到电话出口，收到消息后接头响应，回复向电话箱返回。移动、旋转头盔后重看路线，再停止／重播，检查高亮清理。
5. 拿离展台后按 Reset／Home，观察平滑返回和松开后再抓；Help 的减少动画应让主动复位即时完成。F6／F7／F8 分别检查简体、繁体、英文。

建议审查者实际检查以上流程；现有自动结果不是人工批准。真实头显、双眼玻璃排序、声音舒适性、目标用户、可执行构建和设备性能仍未做。

## 测试对应哪个版本

四个 Unity 进程实际退出均为 0，无异常／失败断言：新体验 **76** 项，原头盔 **206** 项，原探索 **1486** 项；三语 **78760** 次断言（大量重复文字／字形检查）。新体验通过保存主场景的组件 API 检查，原输入回归由既有合成 XR 输入验证，不能等同于实体头显或系统键盘鼠标实测。

报告测试时的基准为 `d432a37` 加本轮 dirty 产品；发布前逐一核对 **186 个被测文件**的工作区 SHA256 与测试记录一致，又核对 Git 提交 blob 与暂存版本一致。Git 的标准行尾转换另计，不能把基准 SHA 误认为只测试旧代码，也不能声称在新 commit 上重跑了测试。

- [提交映射及新资产引用核对](../../artifacts/helmet-experience/rear-delivery-20261006/publication-audit.json)
- [最终四次运行、原始报告哈希、可携带报告索引](../../artifacts/helmet-experience/rear-delivery-20261006/summary.json)
- [新功能断言](../../artifacts/helmet-experience/rear-delivery-20261006/features-checks.json)、[原操作断言](../../artifacts/helmet-experience/rear-delivery-20261006/inspection-checks.json)、[原探索断言](../../artifacts/helmet-experience/rear-delivery-20261006/exploration-checks.json)、[三语断言汇总](../../artifacts/helmet-experience/rear-delivery-20261006/localization-checks.json)

携带报告由真实原始报告提取；去除大型未跟踪文件清单，三语保留分类计数及首尾示例，完整原始报告／失败日志仍在开发机。截图是保存主场景的临时诊断取景。电话加入前的 delivery-20261006 是历史证据，最新以 rear-delivery-20261006 为准。

原回归曾发现新接头扩大包围盒，让剖切中心偏移约 2 毫米；修复为它仍参加渲染／剖切，但不改变原工具定位包围盒。没有放宽原测试要求。其他失败与修复分别见[体验记录](2026-10-06-helmet-experience-polish.md)和[接头记录](2026-10-06-rear-telephone-connection.md)。发布首次提交因本工作副本未配置身份失败，恢复已核实的本成员仓库级 Git 身份后成功，没有修改全局身份。

如需复验，关闭打开的 Editor，再分别执行这些入口，使用新日志／运行目录，不覆盖已引用报告：

| 内容 | executeMethod |
| --- | --- |
| 新体验 | `Museum.Experience.EditorTools.HelmetExperienceValidation.RunBatch` |
| 原操作 | `Museum.Localization.EditorTools.TrilingualInspectionRegression.RunBatch` |
| 原探索 | `Museum.Exploration.Editor.HelmetExplorationValidation.RunBatch` |
| 三语 | `Museum.Localization.EditorTools.LocalizationIntegrationValidation.RunBatch` |

入口自身安排退出；不要在异步验收启动后立刻用 `-quit` 中断。运行结束核对真实进程退出码和新 report.json。同步／冲突解决改变产品时，应复验受影响行为。

## 合并和后续修改

**先处理 [PR #18](https://github.com/Twispra/XRmuseum/pull/18)，再把 #19 的 base 从三语分支改为 main。** 整合人核对实际差异和共享引用后，另一位成员审查再合并。本次没有合并，也没有替其他成员确认共享路径已释放。

发布 Fetch 看到另有 `add-helmet-hotspots`（8fe3142）旧基线分支，包含 MuseumStart 的新节点，目前无对应开放 PR。未把它混入本次测试／提交；以后接入需协调场景节点和三语显示，不能整份接受某一侧的场景覆盖。

接手组件和边界见[功能说明](../helmet-experience-polish.zh-CN.md)：

- `HelmetRearConnections` 保存两个出口和电话反馈；`HelmetConnectionDemonstration` 消费这些引用。改外观时同时同步出口、剖切渲染器、SectionMeshData 和 AIR 热点，不只移动可见模型。
- `SectionMeshData.asset` 目前原 78 个金属网格加电话网格，共 79 条；只描述现有几何，不等于真实内部结构。截面计算和双眼透明效果需要设备验证。
- 新文字按[三语维护说明](../localization-authoring.zh-CN.md)补齐三种语言、字形和布局；静音及减少动画不能漏接。
- 原模型源、FBX、副本、输入、Packages、平台和既有 meta/GUID 保留。新资源／AI 辅助说明已写入[资源记录](../external-resources.md)。

提交只包含本轮逐文件确认的 78 个文件，以及随后发布文档／审计。开工前 570 个未知未提交文件哈希不变、未上传；缓存、课程原件和临时输出仍留本地。它们需要后续按归属处理，不能直接勾选全部 Changes。下一步最小动作是另一位组员打开此分支的保存主场景，按上面的五步复查并审阅 PR。
