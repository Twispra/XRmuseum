# PR #76：2026-11-17 独立合并审查

原资产提交 `797984ede023e803b641fd46a558baa51e02bb80`，同步实际 main `62e6cb5c8630b8c33191911fbeead1de150ae13d` 后运行基底为 `52ae591706128734cf21a0d5ce08deab58e9f074`。运行时另有四项研究 README／复跑工具修改及本目录新脚本，精确脚本哈希见 [tested-scripts.json](tested-scripts.json)。不是干净原 head 的冒名复跑。

- [asset-verification.json](asset-verification.json)：5 份 .blend 均为实际 LFS 对象，大小／SHA-256 与精确 Git 指针一致；r2／backup／work 同字节。
- [saved-model-checks.json](saved-model-checks.json)：Blender 4.5.14 LTS 四进程自然退出0。v4逐对象回读与历史一致；r1 141件保留／5件新增／705组配对；r2 145件锁定及几何门通过；r3完整库存146件／152场景对象／12材质／1相机／4灯。116个未修改历史输入文件前后SHA-256零差。
- [independent-metrics.json](independent-metrics.json)：独立复算r1 24组、r2 12组边界指标及r3 mask／16点／角度，全部匹配。只核对已提交图像和标注，未重放原B照片分割。
- [output-routing-checks.json](output-routing-checks.json)：新输出成功、已有输出／嵌套目录／错图哈希／缺图／覆盖旧v4报告共6项检查。5项预期拒绝均非零退出，旧证据未变。正向用本模型图作为私有预览夹具，仅在独立夹具改注释哈希，不能当原照片再验。
- [blender-dependency.json](blender-dependency.json)：本轮独立portable依赖的官方来源、SHA-256和版本；不将软件或临时日志入库。

复跑时 Python 需 NumPy、Pillow、SciPy；本轮数值环境为 bundled Python 3.12／NumPy 2.3.5／SciPy 1.18.1／Pillow 13.0.0。以下输出须使用新路径，raw日志和完整重复回读留本地：

```text
python <本目录>/verify_assets.py --repo <仓库> --commit 797984ede023e803b641fd46a558baa51e02bb80 --output <新目录>/assets.json
python <本目录>/run_saved_checks.py --repo <仓库> --blender <Blender4.5.14> --output-root <新目录>/saved
python <本目录>/independent_metrics.py --repo <仓库> --commit 797984ede023e803b641fd46a558baa51e02bb80 --output <新目录>/metrics.json
python <本目录>/check_output_routing.py --repo <仓库> --blender <Blender4.5.14> --output-root <新目录>/routing
```

界限不变：r1下端接口仍隔8.783mm模型间距，r2仍有22对既有BVH候选；IoU0.370253低于0.93门槛。16点／可见投影L／图像方向待技术确认，Step4／5未执行。没有Unity接入、照片精度认可、真实尺寸／实机／性能／构建或用户研究验收。[活动与合并交接](../../../../../docs/activity/2026-11-17-pr76-review-and-merge.md)。
