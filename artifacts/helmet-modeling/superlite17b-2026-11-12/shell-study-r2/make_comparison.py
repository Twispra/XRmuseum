"""Compose only project-owned source renders for visual review."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

here=Path(__file__).resolve().parent
image=Image.new('RGB',(1840,1620),'#f2f5f8');draw=ImageDraw.Draw(image)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',32)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
draw.text((30,18),'仅外壳形状试改：其他 145 个零件锁定',font=font,fill='#132b40')
draw.text((30,64),'固定相机 / 同灯光材质；r1 完整保留；r2 为视觉候选，未证明实物尺寸精度',font=small,fill='#52697c')
for col,(version,label) in enumerate([('before','保留版 r1'),('after','仅外壳候选 r2')]):
    x=30+col*910;draw.text((x,108),label,font=font,fill='#17384f')
    for y,name,height in [(158,'front-beauty',659),(848,'side-ortho',720)]:
        source=Image.open(here/'renders'/(version+'-'+name+'.png')).convert('RGB')
        source.thumbnail((880,height),Image.Resampling.LANCZOS)
        image.paste(source,(x,y))
image.save(here/'comparison.png')
print('SHELL_R2_COMPARISON_COMPOSED 1840x1620')
