# SuperLite 配置及后部结构复核：首版证据

产品基线 `adc06875103cc47a555dd251e3cf80d836a09558`；认领见 [#25](https://github.com/Twispra/XRmuseum/issues/25#issuecomment-6813952399)。结论和限制见[专项报告](../../eras/superlite-17b/references/config-review-2026-11-12.zh-CN.md)。

- `rear.png`、`side.png`、`rear_threequarter.png`：本轮读取已保存 v2 的原始 Blender 渲染；不含厂家图像，不是修改后的几何。
- `review-sheet.png`：三视字母标注与概念关系图；不按比例，不表示实测安装点。
- `model-readback.json`：源及两份FBX读取前后SHA、143网格/50,268三角面、实际FBX143网格及逐对象范围。
- `sources.json`：外部PDF的URL、文档号、查阅页码和SHA；原件与临时页面图不提交。
- `validation.json`：本轮文件/链接、产品无差及原工作分支保留检查。统计不当作Unity、设备或人工用户验证。

## 复跑

先从Git LFS定向展开报告中的源blend、运行导出FBX和Unity使用FBX。不要运行旧build_helmet、Setup或安装器。此脚本只读已保存源，临时改变相机和场景，结束前另开空场景导入运行版FBX；不会保存blend。建议在新的独立工作副本运行，以免覆盖已经引用的本轮输出；拷贝脚本到新的日期目录会改变其根目录解析，应保留 `artifacts/helmet-refinement/qa/<新目录>/` 层级。

```text
<Blender4.5.14> --background --disable-autoexec --python-exit-code 1 --python artifacts/helmet-refinement/qa/<独立目录>/review_saved_model.py
```

源/模型清单采用保存的v2版本；断言故意绑定该版本，新产品请另作版本化复核。`make_review_sheet.mjs` 使用 Node 与 sharp，设置 `MUSEUM_NODE_MODULES` 为已有node_modules目录即可生成标注图；不为Unity引入依赖。脚本与原始图应放同一目录。

本轮头显不可用，未运行Unity或构建；通信部件精确定位、同配置照片、真实比例与性能继续待验。
