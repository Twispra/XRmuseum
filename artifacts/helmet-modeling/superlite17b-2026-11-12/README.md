# SuperLite 17B：以 v4 为基准的精度改进工作

用户于 2026-11-12 明确选择本地 v4，并要求先规划提高精准度的方法，再按 #25 认领范围逐步修改。[执行计划](../../../docs/activity/2026-11-12-superlite-v4-accuracy-plan.md)

## 已完成的准备

- [基准模型](baseline-v4/superlite17b-shell-v4.blend)是原本地 v4 的逐字节副本，SHA256 为 `6e85f7f048b3a6b8706428854ab8934099da25ff21d427e87fde9de930b1c8a2`。原件保留；v5 临时候选未混入。
- [来源清单](baseline-v4/baseline-manifest.json)记录 v2 → v3 → v4 的指纹；[实际回读](baseline-v4/readback.json)由 Blender 4.5.14 LTS 重新打开该副本产生，144 个模型网格、61,400 个三角面。
- [参考图清单](reference-inventory.json)核对本机 12 张原图哈希，全部与原交付清单一致。图片本体、厂家页面和旧原始任务书不复制入库；原始任务书中的猜测不作为新增事实。
- 本步骤没有改变模型几何、材质或运行时，未重跑旧生成器，未改 Unity。v4 旧报告的 178 项仍属于历史运行，本次只做保存源清单与指纹回读。

## 当前成果

已交[相机匹配与误差基线](camera-baseline-r1/README.md)，发现视角仍有歧义，保留全壳不作照片形变。随后从 v4 派生了[后部连接研究首版](rear-study-r1/README.md)：[可编辑源](sources/superlite17b-rear-study-r1.blend)集中修改 #25 的后配重、定位套筒和后铰片。其余141个原部件受指纹保护。新样件下端与原横销接口仍有间隔，尺寸和位置为研究估算；未证明照片精度提升，也未接入Unity。

用户随后要求保留r1、仅尝试外壳形状，已另存[外壳视觉候选r2](shell-study-r2/README.md)：[可编辑源](sources/superlite17b-shell-study-r2.blend)。其他145件保持一致，r1不覆盖；同机位实际轮廓有改善和未改善区域，高光折转仍待外观反馈，不把候选称最终精准复原。

基准文件保持只读使用，后续修改必须另存。复核可用：

```text
<Blender4.5.14> --background --disable-autoexec --python-exit-code 1 --python artifacts/helmet-modeling/superlite17b-2026-11-12/audit_v4_baseline.py -- --output <新运行目录>/v4-readback.json
```

脚本只读取基准，输出逐对象顶点/拓扑指纹、世界变换和材质名称，供后续限定修改范围。它不验证工厂尺寸、水密性或设备体验。

## r3 与本次合并范围（2026-11-17）

[r3 完整备份、mask 基线和16点确认表](shell-rebuild-r3/README.md)已交付步骤0–3，工作副本仍与完整 r2 同字节。新壳的 S1–S5 放样、附件重新贴合尚未执行；16点、暂定可见投影 L=801 及图像方向目标仍待技术确认。本次用户授权将已完成资料和独立研究源经审查后合并，不把归档合并视为形状门槛通过或 Unity 接入，也不关闭 #25。旧 r1/r2、原报告及限制保留。[本轮审查和交接](../../../docs/activity/2026-11-17-pr76-review-and-merge.md)。

基准与备份的只读属性只存在于设置过该属性的本机，Git/LFS 不传播 Windows 只读保护。复跑须校验哈希并使用新的报告/输出位置；上述审计脚本拒绝覆盖既有报告。
