"""把 Noto Sans SC 变量字体做成静态 + 子集，供 Unity TMP 使用。

来源: C:\\Windows\\Fonts\\NotoSansSC-VF.ttf  (SIL Open Font License 1.1)
做法: 1) 从变量字体的 wght 轴取出 Regular (400) 静态实例
      2) 只保留 ASCII + GB2312 全集 + 常用标点
"""
import os
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.subset import Subsetter, Options

SRC = r"C:\Windows\Fonts\NotoSansSC-VF.ttf"
OUT = r"C:\Users\lenovo\Desktop\XR\_tools\NotoSansSC-MuseumSubset.ttf"

os.makedirs(os.path.dirname(OUT), exist_ok=True)

print("读取源字体 ...")
font = TTFont(SRC)
print("  表:", len(font.keys()), " 大小: %.2f MB" % (os.path.getsize(SRC) / 1048576))

# 1) 变量字体 -> 静态 Regular
if "fvar" in font:
    axes = {a.axisTag: (a.minValue, a.defaultValue, a.maxValue) for a in font["fvar"].axes}
    print("  变量轴:", axes)
    instantiateVariableFont(font, {"wght": 400}, inplace=True, updateFontNames=True)
    print("  -> 已实例化为静态 Regular")
else:
    print("  非变量字体，跳过实例化")

# 2) 目标字符集
chars = set(chr(c) for c in range(0x20, 0x7F))            # ASCII 可打印
gb = 0
for hi in range(0xA1, 0xFA):
    for lo in range(0xA1, 0xFF):
        try:
            chars.add(bytes([hi, lo]).decode("gb2312"))
            gb += 1
        except Exception:
            pass
chars |= {
    "\u3000", "\u2018", "\u2019", "\u201c", "\u201d", "\u2014", "\u2026",
    "\u00b7", "\u2192", "\u00d7", "\u00b0", "\u2265", "\u2264", "\u00a0",
}
print("  字符集: %d 个（含 GB2312 解出 %d 个）" % (len(chars), gb))

# 3) 子集化
options = Options()
options.name_IDs = ["*"]
options.name_legacy = True
options.notdef_outline = True
options.recalc_bounds = True
options.recalc_timestamp = False
options.layout_features = ["*"]
options.drop_tables = ["DSIG"]
subsetter = Subsetter(options=options)
subsetter.populate(text="".join(sorted(chars)))
subsetter.subset(font)

font.save(OUT)
size = os.path.getsize(OUT)
print("写出: %s" % OUT)
print("大小: %.2f MB  (压缩比 %.1f%%)" % (size / 1048576, size * 100.0 / os.path.getsize(SRC)))

# 4) 回读校验
check = TTFont(OUT)
cmap = check.getBestCmap()
sample = "潜水头盔博物馆年代无障碍访客探索"
missing = [c for c in sample if ord(c) not in cmap]
print("回读: cmap 共 %d 个码位；中文样例缺字: %s" % (len(cmap), missing if missing else "无"))
