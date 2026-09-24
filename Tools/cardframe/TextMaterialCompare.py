# -*- coding: utf-8 -*-
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
OTF = r"Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf"
LBL = r"Assets/_Game/Fonts/NotoSerifCJKsc-Regular.otf"
OUT = r"Tools/cardframe/preview/text-material-outline.png"
PLATE = (64, 52, 43)
TXT = "场上己方英雄 +2+1（智者退场后"
EM = 30

def render(stroke, halo, w=430, h=52):
    img = Image.new("RGB", (w, h), PLATE)
    if halo > 0:
        mask = Image.new("L", (w, h), 0)
        ImageDraw.Draw(mask).text((8, 8), TXT, font=ImageFont.truetype(OTF, EM), fill=255, stroke_width=stroke, stroke_fill=255)
        m = mask.filter(ImageFilter.MaxFilter(2 * halo + 1)).filter(ImageFilter.GaussianBlur(halo))
        img = Image.composite(Image.new("RGB", (w, h), (0, 0, 0)), img, m.point(lambda v: int(v * 0.5)))
    ImageDraw.Draw(img).text((8, 8), TXT, font=ImageFont.truetype(OTF, EM), fill=(253, 250, 244),
                             stroke_width=stroke, stroke_fill=(0, 0, 0))
    return img

rows = [("现在：_OutlineWidth 0.2 + Underlay 0.25（一圈黑晕）", render(2, 4)),
        ("改后：_OutlineWidth 0.08，Underlay 归零  ← 当前状态", render(1, 0)),
        ("可选：_OutlineWidth 0（完全不要描边）", render(0, 0))]
Z = 3; pad, lab = 18, 30
W = rows[0][1].width * Z + pad * 2
notes = ["实机底板取色 #40342B；字色 #FDFAF4；图为 3 倍放大",
         "悬停标签 TagLabel.prefab 字号 26；同一改动作用于：Serif Black 字库材质、Serif Bold 字库材质、TMP_Menu_Bold_Outline、TMP_Orb_Black_Outline",
         "描边像素宽 ≈ _OutlineWidth × _ScaleRatioA(0.8333) × 0.5 × 字号 → 0.2 约 2px、0.08 约 1px（字号 30 时）"]
H = pad + len(rows) * (lab + 52 * Z + 12) + 30 + len(notes) * 24
cv = Image.new("RGB", (W, H), (22, 23, 26)); d = ImageDraw.Draw(cv)
f1 = ImageFont.truetype(LBL, 17); f2 = ImageFont.truetype(LBL, 15)
y = pad
for t, img in rows:
    d.text((pad, y), t, font=f1, fill=(210, 211, 215)); y += lab
    big = img.resize((img.width * Z, img.height * Z), Image.NEAREST)
    cv.paste(big, (pad, y)); y += big.height + 12
y += 8
for n in notes:
    d.text((pad, y), n, font=f2, fill=(150, 152, 158)); y += 24
cv.save(OUT); print(OUT, cv.size)