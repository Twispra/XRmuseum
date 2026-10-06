"""核对 Localization 模块源码里用到的每个字符，在子集字体里都有字形。"""
import os, re, sys
from fontTools.ttLib import TTFont

FONT_TTF = r"C:\Users\lenovo\Projects\XRmuseum\MuseumVR\Assets\Museum\Features\Localization\Fonts\NotoSansSC-MuseumSubset.ttf"
SRC_DIR = r"C:\Users\lenovo\Projects\XRmuseum\MuseumVR\Assets\Museum\Features\Localization"

font = TTFont(FONT_TTF)
cmap = font.getBestCmap()
print("字体 cmap 码位数: %d" % len(cmap))

# 收集所有 .cs 里的字符
chars = set()
files = []
for root, _, names in os.walk(SRC_DIR):
    for n in names:
        if n.endswith(".cs"):
            p = os.path.join(root, n)
            files.append(p)
            with open(p, encoding="utf-8") as f:
                chars |= set(f.read())

print("源码文件: %d 个，出现的不同字符: %d 个" % (len(files), len(chars)))

# 只关心真正会被渲染的字符：CJK、全角标点、拉丁、数字、常用符号
def interesting(c):
    o = ord(c)
    if c.strip() == "":
        return False
    if 0x20 <= o <= 0x7E:        # ASCII 可打印
        return True
    if 0x4E00 <= o <= 0x9FFF:    # CJK 汉字
        return True
    if 0x3000 <= o <= 0x303F:    # CJK 标点
        return True
    if 0xFF00 <= o <= 0xFFEF:    # 全角
        return True
    if o in (0x2018, 0x2019, 0x201C, 0x201D, 0x2014, 0x2026, 0x00B7, 0x2192, 0x00D7):
        return True
    return False

need = sorted(c for c in chars if interesting(c))
missing = [c for c in need if ord(c) not in cmap]

print("需要渲染的字符: %d 个" % len(need))
if missing:
    print("!! 缺字形 %d 个:" % len(missing))
    for c in missing:
        print("   '%s'  U+%04X" % (c, ord(c)))
else:
    print("缺字形: 无 (PASS)")

# 额外：确认关键样例可渲染
sample = "无障碍博物馆体验请选择界面语言欢迎坐着单手操作"
sample_missing = [c for c in sample if ord(c) not in cmap]
print("关键样例缺字: %s" % (sample_missing if sample_missing else "无"))
