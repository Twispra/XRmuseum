# 照片头盔模型：Unity 接入与对比

这份接入把照片精修第二稿的外观模型转换为 Unity 可直接使用的分体 OBJ、材质和 `PhotoHelmetStudy.prefab`，并提供独立的 `PhotoHelmetComparison.unity` 场景。第二稿基于 [PR #28 的第一版照片研究](https://github.com/Twispra/XRmuseum/pull/28)。后续把同一模型放到 `MuseumStart` 的左侧独立展台；它是照片所示实物的外观研究，不替换中央 Mark V、SuperLite 或早期占位，也不作为 [Issue #60](https://github.com/Twispra/XRmuseum/issues/60) 的早期精细模型交付。

## 主展厅的独立展品

1. 在 `MuseumStart.unity` 的层级中展开 `Exhibit Placeholders > Exhibit 01 - 1988 Photo Helmet Study`。左侧展台上的 `Photo helmet model - visual interpretation` 是照片模型实例，不属于中央展品的三年代列表。
2. Play 模式中，左展台前沿的三枚实体按钮分别将模型向左旋转 30°、回到正面、向右旋转 30°。按钮使用现有 XR 射线／直接选择入口；无头显时可在 Inspector 检查 `PhotoHelmetGalleryDisplay`，或运行编辑器定向检查。中央展品的“Reset”仍只作用于中央展品。
3. 展台前方的名称和边界说明随主场景语言通道切换简体、繁体、英文。文字明确写为外形研究，并说明细节未确认、不是实物复原。
4. 这份模型没有可核实的开窗机构、供气／通话锚点或真实尺寸，主展厅不开放它的前窗、剖切、供气与通话动作。独立对比场景中的剖切仍只是桌面解释性预览。左侧展台的用途与 [Issue #64](https://github.com/Twispra/XRmuseum/issues/64) 相关，需要小组在 PR 中确认；右侧展台保持原样。

## 打开与测试

1. 用 Unity **2022.3.62f3c1** 打开 `MuseumVR` 工程，并确认 Git LFS 已把模型文件展开。
2. 打开 `Assets/Museum/Features/PhotoHelmetTrial/Scenes/PhotoHelmetComparison.unity`，切到 Game 视图后点击 Play。
3. 左边是现有 Mark V 外观，右边是照片头盔。按 `A/D` 或左右方向键同步旋转；按 `S` 打开/关闭照片头盔的壳体剖切预览，剖切时用 `Q/E` 调整深度；按 `R` 同时回到正面并关闭剖切。
4. 若需要在其他隔离场景摆放模型，可拖入 `Assets/Museum/Features/PhotoHelmetTrial/Prefabs/PhotoHelmetStudy.prefab`。预制件约 0.45 米高、正面朝向已设定；尺寸只用于画面比较，不是实物测量值。
5. 如需从原始 OBJ 重新生成材质、预制件和对比场景，执行 Unity 菜单 `Museum > Photo helmet > Build comparison scene`。该命令会覆盖本功能的同名预制件和对比场景；运行前先保存对这两个文件的手工改动。

## 对比结果与边界

- 同样高度、同一相机和灯光下，照片模型更接近小组照片中的四孔前窗、圆顶轮廓、侧窗格栅和胸甲。现有 Mark V 是另一种外形，不能仅凭这组照片判定它错误。
- 照片模型的空心壳体已单独导出，比较场景能借用现有剖切着色器并动态生成壳体截面；这是桌面预览，不是主展厅的 XR 剖切操作。细节模型跟随裁切，但只为壳体生成截面封口，内部结构并未据实物还原。
- 目前**没有**与照片头盔几何匹配、且已核实开合方式的前窗铰链。现有 Mark V 的铰链属于另一款头盔，不能直接移植；本次不做假开窗动画。照片模型也没有空气/电话交互锚点或年代绑定。主场景只新增左展台的独立陈列与旋转按钮，不修改 `InspectableHelmet.prefab`、中央年代切换逻辑，也不把新模型标为可交互主展品。
- 模型身份、实物比例、不可见结构和表面颜色仍待馆方资料核实。材质为照片推断，不声称是实物扫描。
- Unity 编辑器 Play 测试已确认对比场景的正面显示、同步旋转、剖切开关/深度、旋转后截面对齐和重置。主场景另做了定向检查：模型和三枚 XR 按钮存在，左右旋转及复位、三语展签和中央年代不受影响均通过；退出 Play 时控制台无错误。按钮的真实头显选择、完整用户旅程、性能基准及实物尺寸校准仍未验证。

## 文件来源与交接

- 外观源：本分支的 `artifacts/helmet-refinement/photo-study-v2/exports/photo-helmet-refined-v2.glb`；`scripts/export_unity_section_obj.py` 把其中的空心壳体与其他细节分成两个 OBJ，保持原几何边界不变。当前 Unity 目录的 OBJ、两张贴图和材质是为 Unity 2022 原生导入准备的派生文件，不要把 OBJ 反向当作可编辑原稿。第二稿的参数化修改脚本仍在单独的照片模型分支，尚未并入本分支。
- 此接入使用 AI 辅助完成格式转换、测试脚本和初步画面检查；展品身份、历史准确性以及是否放进主体验仍需组员审阅。
- 左展台的独立陈列仍需小组确认用途与展示文案。若将来扩大为剖切或热点体验，应先核实馆藏身份，再建立与其几何匹配的独立交互；只有找到可信的开窗构造依据并完成枢轴/碰撞验证后才加入铰链。最后做头显验证；不要替换 Mark V 的视觉子物体来复用不匹配的铰链。
