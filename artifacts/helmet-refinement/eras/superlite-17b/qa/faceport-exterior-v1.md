# 面窗与侧阀研究件 V1 检查

检查环境：Windows，Blender 4.5.14 LTS，2026-10-08。基准 HEAD `37428dff4307a31a042b8b3ed38e57d8856b502c`（`origin/codex/helmet-refinement-assets`）；运行时只有本任务新建文件尚未提交，无既有文件改动。两份源文件、两份 GLB 和四张预览为本轮新产物。

方法：在新的空目录运行 `scripts/make_original_textures_v1.py` 与 Blender 后台 `scripts/build_faceport_exterior_v1.py`；再用 `scripts/check_faceport_exterior_v1.py` 对每个 `.blend` 和对应 GLB 分别重新打开、比对网格名称/数量、UV、打包贴图、透明材质及摄影棚排除。详细机器结果在 [回读 JSON](faceport-exterior-v1-readback.json)。

结果：面窗源/GLB 各 20 个网格；侧阀源/GLB 各 39 个网格，名称一致；源与 GLB 均无缺 UV 网格。两张原创 1024px 贴图打包在源文件；面窗聚碳酸酯材质在 GLB 中为 BLEND 透明；所有 `REVIEW_ONLY_` 预览物体均未导出。源网格三角面数为 4,284 + 1,780；应用非破坏性倒角后导出为 10,428 + 4,644。本人通过四张渲染检查正面与斜前方可辨识度，未做外部成员的造型批准。

限制：没有完整头盔装配，无法核对壳体接触、装配间隙、整体真实尺寸或共享原点。没有拿实物测量、制造图、强度/安全验证、Unity 导入、真实 VR、LOD/性能测试。模型属于外观讨论稿，不可作装备制造或安全使用依据。侧阀内部和通信接口未建模。
