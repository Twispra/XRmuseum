"""Create two original 1024 px interpretive material maps; no source image is copied."""

from pathlib import Path
from PIL import Image
import math
import random
import sys

root = Path(sys.argv[1]).resolve()
out = root / "textures"
out.mkdir(parents=True, exist_ok=True)
base = out / "SL17B_Exterior_BaseColor_v1.png"
rough = out / "SL17B_Exterior_Roughness_v1.png"
if base.exists() or rough.exists():
    raise SystemExit("Refusing to overwrite existing textures")
rng = random.Random(1708)
n = 1024
base_image = Image.new("RGB", (n,n))
rough_image = Image.new("L", (n,n))
bp = base_image.load()
rp = rough_image.load()
for y in range(n):
    for x in range(n):
        grain = rng.randrange(-4,5)
        wave = 2.0 * math.sin((x+y*.23) * .11)
        v = max(0, min(255, round(42 + grain + wave)))
        bp[x,y] = (v, min(255,v+2), min(255,v+5))
        rp[x,y] = max(0,min(255,round(94 + rng.randrange(-9,10) + 3*math.sin(y*.05))))
base_image.save(base, optimize=True)
rough_image.save(rough, optimize=True)
print(base, rough)
