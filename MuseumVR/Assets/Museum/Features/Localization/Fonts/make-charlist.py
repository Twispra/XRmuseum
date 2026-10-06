"""从 LocalizationSetup.cs 的种子文案里提取所有需要渲染的字符，
输出成一个 .txt 供 TMP Font Asset Creator 的 "Characters from File" 使用。"""
import os
import re
from pathlib import Path

OUT_DIR = Path(__file__).resolve().parent
SETUP = OUT_DIR.parent / "Editor" / "LocalizationSetup.cs"
OUT = os.path.join(OUT_DIR, "characters-required.txt")

with open(SETUP, encoding="utf-8") as f:
    src = f.read()

chars = set()

# Seed("key", "中文", "English", "note")
for m in re.finditer(r'new Seed\(\s*"([^"]*)"\s*,\s*"([^"]*)"\s*,\s*"([^"]*)"', src):
    for group in (m.group(2), m.group(3)):
        for c in group:
            if not c.isspace():
                chars.add(c)

# 语言按钮的原生名称
for c in "中文English":
    chars.add(c)

# 缺 key 标记与兜底 ASCII
# 富文本标签本身不渲染；这里只保留缺 key 标记与现有 ASCII 支持。
for c in "[[ ]]0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:;()%-/+=?!'\"@#&*":
    if not c.isspace():
        chars.add(c)

# TMP 内部特殊字符（下划线、删除线等），避免再报 "character used for Underline is not available"
for c in "_\u2014":
    chars.add(c)

text = "".join(sorted(chars))
with open(OUT, "w", encoding="utf-8", newline="") as f:
    f.write(text)

cjk = sum(1 for c in text if 0x4E00 <= ord(c) <= 0x9FFF)
print("字符总数: %d（其中汉字 %d 个）" % (len(text), cjk))
print("输出: %s" % OUT)
print("大小: %d 字节" % os.path.getsize(OUT))
print("前 60 个字符: %s" % text[:60])
