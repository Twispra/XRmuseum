"""从三语文案 JSON 生成 TMP 字符清单；运行不会重建字体资产。"""
import json
import re
from pathlib import Path
folder = Path(__file__).resolve().parent
data = json.loads((folder.parent / "Tables" / "MuseumTranslations.json").read_text(encoding="utf-8"))
chars = set("简体中文繁體中文English简 / 繁 / EN[]_—")
for row in data["entries"]:
    for field in ("chinese", "traditionalChinese", "english"):
        chars.update(re.sub(r"<[^>]*>", "", row[field]))
chars.update("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:;()%-/+=?!'\"@#&*")
text = "".join(sorted(c for c in chars if not c.isspace()))
(folder / "characters-required.txt").write_text(text, encoding="utf-8")
print(f"{len(data['entries'])} 条文案；{len(text)} 个显示字符。")
