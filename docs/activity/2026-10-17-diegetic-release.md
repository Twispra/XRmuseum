# 2026-10-17：实体 UI 版本发布与小组交接

用户已验收当前本地版本并明确授权提交 GitHub、发布五项待认领任务。本轮由 Fttt1003 提交，Codex 辅助实现、走查、测试和记录；未替其他成员认领或写成人工批准。

## 提交范围与实际状态

基线是重新 fetch 核对的 `origin/main bc9df77a56bf6dff18af9b25c528c82b49417ca4`，工作分支为 `codex/diegetic-single-hand-ui-2026-10-14`。本次提交此前数轮已验收的累计成果，而非仅最后的高度按钮：

- 在实际外壳上按住旋转，固定几何中心，无独立旋转按钮；实际玻璃点击开合，原知识热点保留。
- 剖面开关／短轨固定在展台，拖动只改变切面位置；按下反馈、松开确认、移开取消，切换目标前需释放。
- 供气／通信就地入口及固定关闭入口；设备展开后固定在头盔旁，可按住原位旋转，机械动画与真实端口连线继续。
- 设置加入简繁英、声音、减少动画、提示及视角升降／恢复；高度步长5cm、范围±50cm，F11和展品复位保留偏好。全部为原型参数，非人体工学标准。
- 三语表、源字体、静态字体及GUID保持配套，维护说明和阶段记录一并提交。

新 station 在主场景运行时停用旧跟随面板和自由拿放入口，采用原位旋转模式；禁用 station 可恢复旧入口。这是用户选择的模式调整，不将其写成旧双手拿放在新模式下仍可使用。底层前窗、剖面、探索、年代与热点引擎继续复用。

本轮通过分支与PR交付，**没有执行合并 main**。PR、最终提交与五项任务链接发布后补入下方；以GitHub实际状态为准。

## 验证与来源

Unity 2022.3.62f3c1，保存主场景 `MuseumVR/Assets/Museum/Scenes/MuseumStart.unity`，最终 **731/731通过，Error／Exception 0**。其中402项API／字形／布局等检查、154项XRI注册、164项生产Input Actions模拟输入、11项模块生命周期检查。

- [当前完整检查和38文件来源指纹](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/current.json)
- [字体／保存图集证据](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/font-summary.json)
- [三语设置与模型旋转画面](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/review-previews/)
- [原报告SHA-256及历史快照说明](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/README.md)

测试对应 `bc9df77` 基线加当前候选文件，提交整理前38个文件与原报告逐字节匹配，源TTF另与字体摘要核对。暂存仅规范新增Unity元数据／材质空字段的尾空格和Git行尾，GUID／字段值不变，运行时源码没有改动；被测原始SHA到实际Git blob的对应见 [文件映射](../../artifacts/helmet-interaction/qa/runs/20261017-diegetic-release/published-file-map.json)。发布副本删除无关旧未跟踪清单和本机诊断图片路径，原始报告原位保留；未改变检查和结果。归档的415／479／484是历史阶段结果，不当作当前版本重跑。

本次发布整理阶段没有重复跑Unity：提交前只读复核已测源码、资源及GUID，整理证据与交接。真实头显、Direct实手可达性、舒适度／性能、完整双眼阅读及本轮Windows构建仍未验证；没有据旧构建或自动模拟宣称完成。

## 如何接手

1. 查看本PR实际是否已合并；未合并时先约定依赖分支／commit，合并后从最新main开个人分支。
2. LFS展开，用指定Unity版本直接打开保存的MuseumStart，不运行历史模型生成器。
3. 键盘模拟：Game窗口聚焦、英文输入，T／Y选手；G松开完成点击，按住G并移动模拟手旋转；鼠标左键是扳机。完整规则见[操作交接](../diegetic-ui.zh-CN.md)。
4. 保留 `HelmetConnectionDemonstration.EquipmentExhibits` 与精细设备外层展示轴；机械动画采样不得覆盖用户旋转。高度偏移不得改地面或跟踪局部姿态；新文本走[三语维护](../localization-authoring.zh-CN.md)。
5. 下一项共享写入前，在相关Issue确认范围与顺序。同一主场景、绑定代码、Prefab、三语表／字体同一时间一位写入者；Assignee不是文件锁。

## 待认领任务

将按用户要求发布：早期精细头盔接入；早期／现代提示点精确绑定及隐藏这两档供气通信入口；主面板隐藏／唤出；展览语音指引；另两个展台用途设计。五项尚未实现，具体范围和依赖以发布的Issue为准；不将维护者身份当作认领，不自动分给已有模型作者。

## 本地保留与共享边界

开工前1048个未知未跟踪文件逐项SHA-256复核，0丢失、0改变，未纳入本次提交。只暂存本功能文件、已知配套文档及挑选的发布证据；tmp、失败日志、原始巨大dirty清单、历史未知文件不上传。原模型／FBX／Prefab／公共Input Actions／Packages／ProjectSettings没有本轮改动。共享scene仅包含此前已验收的station接入，未为提交重建。资源与AI辅助范围见[资源记录](../external-resources.md)。
