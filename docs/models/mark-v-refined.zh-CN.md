# Mark V 精细模型交接

任务入口：[Issue #25](https://github.com/Twispra/XRmuseum/issues/25)。2026-10-07，Fttt1003通过Codex辅助完成独立标准空气式模型的资料收集和建模。源文件和导出留在 `artifacts/helmet-refinement/eras/mark-v/`，不替换主场景占位模型。

主要参照是1944年DESCO馆藏件，资料与尺寸依据见[研究说明](../../artifacts/helmet-refinement/eras/mark-v/references/research.zh-CN.md)。当前制品、查看方法、分件协作与未验证范围见[模型入口](../../artifacts/helmet-refinement/eras/mark-v/README.md)。不依赖照片样件PR #28中的源模型，也没有将未知型号的用户照片当作Mark V证据。

优先审阅：正面轮廓和顶窗倾角；1944版本与现售版本的隔离；背后通话/空气两条接头的方向；排气管与前窗可动件；胸板曲率及12个紧固点。内部说明性配件与外形实物依据分开审阅，不把“能导出”当作历史准确性证明。

后续接入时保留各组件和锚点，确定新模型的原点、单位、铰链和运行时绑定；透明件要单独配置URP材质，FBX不能保证自动还原Blender透射外观。评估面数/贴图/材质、LOD和碰撞体后，再验证原有拿放、前窗、剖切、AIR/VOICE高亮、平滑复位、年代切换和三语功能。本轮没有对这些运行时功能作新实现或宣称已测试。

三个年代的原认领指南在[该文档版本](https://github.com/Twispra/XRmuseum/blob/00a83054963b73e080c15c881fa2b0504ea95ba8/docs/models/three-era-collaboration.md)。本分支基于资产汇总分支，不需要把照片样件及其文档整份作为依赖合入。各工作PR的base仍为 `codex/helmet-refinement-assets`。
