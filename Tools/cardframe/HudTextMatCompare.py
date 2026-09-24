# -*- coding: utf-8 -*-
"""HUD 文本材质对照 —— 顶栏数字 vs 抽牌数字（真实截图像素，按字形高度归一化）
出图：Tools/cardframe/preview/hud-text-mat-compare.png
"""
import os
from PIL import Image, ImageDraw, ImageFont
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SHOT_A = os.path.join(os.environ["TEMP"], "codex-clipboard-441fceba-9a42-465d-80e7-ade0a4ab0655.png")
SHOT_B = os.path.join(os.environ["TEMP"], "codex-clipboard-b6a8575e-28d4-4052-a852-461aae5990d4.png")
PREVIEW = os.path.join(ROOT, "Tools/cardframe/preview")
FONT = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf")

CW, CH = 360, 300
GLYPH_H = 150


def grab(path, box):
    im = Image.open(path).convert("RGB").crop(box)
    a = np.array(im).astype(np.int16)
    face = (a[:, :, 0] > 200) & (a[:, :, 1] > 190) & (a[:, :, 2] > 170)
    ys, xs = np.where(face)
    k = GLYPH_H / float(ys.max() - ys.min() + 1)
    im = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)
    top = int(ys.min() * k) - 30
    im = im.crop((0, max(0, top), im.width, min(im.height, max(0, top) + CH)))
    tile = Image.new("RGB", (CW, CH), (0x1A, 0x1F, 0x27))
    tile.paste(im, ((CW - im.width) // 2, max(0, (CH - im.height) // 2)))
    return tile


def wrapped(d, text, x, y, f, fill, width):
    line = ""
    for ch in text:
        if d.textlength(line + ch, font=f) > width:
            d.text((x, y), line, fill=fill, font=f)
            y += f.size + 6
            line = ""
        line += ch
    if line:
        d.text((x, y), line, fill=fill, font=f)
        y += f.size + 6
    return y


sheet = Image.new("RGB", (CW * 2 + 60, 640), (0x1A, 0x1F, 0x27))
d = ImageDraw.Draw(sheet)
ft = ImageFont.truetype(FONT, 19)
fs = ImageFont.truetype(FONT, 15)
cols = [
    (grab(SHOT_A, (142, 120, 230, 176)), "顶栏数字（21px）",
     "Health / Energy / HandText / TurnText · 材质 TMP_Stat_Bold（已改）· 实机描边 ≈ 0.25px，归一化后几乎看不到黑影"),
    (grab(SHOT_B, (1318, 812, 1406, 900)), "抽牌数字（54px）",
     "DrawCountText · 材质 TMP_Orb_Black_Outline · 实机描边 ≈ 1.05px，黑影很明显"),
]
for i, (tile, title, sub) in enumerate(cols):
    x = 20 + i * (CW + 20)
    sheet.paste(tile, (x, 110))
    d.text((x, 16), title, fill=(0xE4, 0xCB, 0x84), font=ft)
    wrapped(d, sub, x, 42, fs, (0x8E, 0xA2, 0xB4), CW)
y = 448
for line in [
    "字形高度已归一化（两侧都放大到 150px），所以可以直接比「描边 / 字高」的比例。",
    "描边像素宽 = _OutlineWidth x _ScaleRatioA x 0.5 x scale，scale 随「字号 ÷ 字体采样尺寸」缩放：",
    "同一份 0.28 的描边，54px 下是 1.05px，21px 下只剩 0.25px —— 糊进深色顶栏底里就看不见了。",
    "材质本身两份现在逐项相同（只差 _MainTex 字体图集：字体不同，图集就不可能相同）。",
]:
    y = wrapped(d, line, 20, y, fs, (0xB9, 0xC4, 0xCE), CW * 2)

out = os.path.join(PREVIEW, "hud-text-mat-compare.png")
sheet.save(out)
print(out, sheet.size)