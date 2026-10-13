# 不启动 Unity 的 PR 基础检查

本仓库的 GitHub Actions 在 PR 和 `main` 推送时运行 `Static repository checks`。它只检验可从 Git 文本文件确认的基础问题，不下载 LFS 模型、不启动 Unity，也不需要 Unity 许可。第一次使用前，请先让另一位组员审阅工作流和脚本，再决定是否将它设为必须通过的合并条件。

在仓库根目录手动复现：

```bash
python3 -m unittest discover -s scripts/tests -v
python3 scripts/check_static_repository.py
```

检查范围：

- `MuseumTranslations.json` 可以解析，`entries` 非空，每项有非空的 key／简体／繁体／英文／用途 note；key 不重复，同一英文原文不会对应两组不同译文。
- `MuseumVR/Assets/Museum/` 内已跟踪的 Unity 资源和目录有配套 `.meta`，也不留失去对应资源的 `.meta`。仅用于占位的 `.gitkeep` 不强制配 `.meta`。
- 根 README、`docs/` 和 `artifacts/` 中已跟踪 Markdown 的相对文件／目录链接指向现有路径；外部网址和页内锚点不在此项范围。

发现问题时先看 Actions 中 `ERROR:` 后面的路径，在自己的分支修正并重新推送。工作流不会自动修复、重建或更改资源。**通过只表示上述静态检查通过**：不证明 `.meta` 的 GUID 引用正确、Unity 可以导入、字体已烤入新字、文案布局合适、XR 输入正常、头显体验可用，或 Windows 构建成功。这些仍按相应模块的验证说明处理。
