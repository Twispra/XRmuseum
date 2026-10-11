# 2026-10-09：SuperLite 17B 审阅回应、整盔资料与 v2 脚本

负责人账号 Ereshkigal1224，Codex 辅助。依据 [PR #37 维护者审阅](https://github.com/Twispra/XRmuseum/pull/37#issuecomment-6063779730)，本人在 [同 PR 回复](https://github.com/Twispra/XRmuseum/pull/37#issuecomment-6077497115)接受 P/N 500-010（posts）整盔资料收集与核对，继续负责面窗/侧阀外观修正；**未认领主体建模、最终总装或 Unity 接入**。PR 保持 Draft、base 保持资产汇总分支。

开工时当前工作分支 `codex/superlite17b-details-2026-10-08`，HEAD `1849b4e6f15b97d277972e6ea930fe6c5c73aacb`，工作树干净。`git fetch origin --prune` 成功，远程个人分支仍为该 HEAD，资产 base 为 `37428dff4307a31a042b8b3ed38e57d8856b502c`。主 checkout 既有的未跟踪嵌套 `XRmuseum/` 未纳入本分支、未清理。

## 本轮实际新增/更改

- 参考资料：`artifacts/helmet-refinement/eras/superlite-17b/references/whole-helmet-source-map-v2.zh-CN.md` 汇总厂家 17B 产品页、规格、2025 零件图、通信手册与 2021 压圈材料公告，逐项标注角度/零件/配置适用性、部件关系、数据来源和缺口。照片文件路径/手册年份均不当成实物制造年份；零件分解图不当尺度图；只链接厂家媒体。
- 对照页 `references/photo-model-comparison-v2.zh-CN.md` 将维护者指出的 v1 外观问题对应到厂家图片与本地 v1 同类视角，并列出 v2 目标/未确定项。因下述运行阻碍，此页不冒称已有 v2 模型渲染。
- `references/faceport-exterior-parameters-v2.json` 将压圈估计深度与新脚本统一为 0.012 m；`scripts/build_faceport_exterior_v2.py` 规划非对称面窗、不规则侧阀壳与错位的两控制钮，并将随壳体的 visor 520-054 从独立面窗件移除；`scripts/check_faceport_exterior_v2.py` 增加压圈深度/错误 visor 几何检查。均保留 v1 文件原样。
- 本 README、新活动记录和资源记录更新。没有改其他年代模型、主体总装、主场景、现用资产或课程资料。

## 检查与阻碍

`qa/faceport-exterior-v2.md` 记录本轮证据：v2 参数可解析、脚本 Python 语法编译通过、压圈前后差值 0.012 m。尚未进行 Blender 内运行、导出或渲染。下载自 Blender 官方站的便携 4.5.14，以及用户随后安装在 `D:\Blender4.5.14` 的安装版，均在本机因同一 `OpenEXR.dll` 报 `0xC0E90002`；CodeIntegrity 事件 3077/3033 指向签名级别限制。两份 DLL 的 SHA-256 相同，不能认定是单次下载损坏；没有关闭安全防护。**v2 可编辑 `.blend`、GLB、模型同角度预览和运行版 QA 仍缺失**。v1 的现有文件及其机器检查仍可读取，但维护者指出的外形不足尚未在二进制模型中修正。

下一步在可信且可正常启动的 Blender 4.5.14 环境生成 v2 独立源、GLB 与渲染，对照同版官方照片人工修形并回读检查；然后在 Issue #25 与 Draft PR #37 更新预览、提交和未完成事项。正式比例、原点和壳体接触曲面需要未来主体作者共同确认。Unity/头显/构建/用户研究未做，不能用本轮静态检查替代。
