"""Typeset the model names for the two small original presentation markings."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def font(size,bold=False):
    for family in (("segoeuib.ttf" if bold else "segoeui.ttf"),
                   ("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf")):
        try: return ImageFont.truetype(family,size)
        except OSError: pass
    return ImageFont.load_default(size=size)


def make(style,output):
    if style=="sideblock":
        img=Image.new("RGB",(768,320),(169,177,178));d=ImageDraw.Draw(img)
        d.rounded_rectangle((9,9,758,310),radius=15,outline=(69,77,81),width=4)
        d.text((384,91),"KIRBY MORGAN",font=font(40),fill=(41,50,54),anchor="mm")
        d.line((91,146,677,146),fill=(87,94,94),width=3)
        d.text((384,214),"SUPERLITE 17B",font=font(65,True),fill=(28,36,38),anchor="mm")
    elif style=="purge":
        img=Image.new("RGB",(512,512),(42,44,44));d=ImageDraw.Draw(img)
        d.ellipse((17,17,494,494),outline=(146,151,148),width=5)
        d.text((256,210),"KIRBY MORGAN",font=font(42),fill=(214,217,208),anchor="mm")
        d.line((115,253,397,253),fill=(171,175,169),width=3)
        d.text((256,314),"SuperFlow",font=font(67,True),fill=(232,233,223),anchor="mm")
    else: raise ValueError(style)
    with output.open("xb") as handle: img.save(handle,format="PNG")


if __name__=="__main__":
    make(sys.argv[1],Path(sys.argv[2]))
