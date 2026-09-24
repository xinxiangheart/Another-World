# -*- coding: utf-8 -*-
"""实装预览：真卡框 5 层 + 真卡图 + 真徽章素材，1u = 12px，按预制体坐标摆。"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
RES = os.path.join(ROOT, "Assets/_Game/Resources")
OUT = os.path.join(ROOT, "Tools/cardframe/preview/badge-installed.png")
S = 12.0                       # px / 卡单位
CW, CH = 83.33, 146.33
FONT = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf")
TMP = r"C:\Users\22589\AppData\Local\Temp"


def load(rel):
    return Image.open(os.path.join(RES, rel)).convert("RGBA")


def paste_center(canvas, img, cx_u, cy_u, w_u, h_u, ox, oy):
    w, h = int(round(w_u * S)), int(round(h_u * S))
    im = img.resize((w, h), Image.LANCZOS)
    x = int(round(ox + cx_u * S - w / 2))
    y = int(round(oy - cy_u * S - h / 2))
    canvas.alpha_composite(im, (x, y))


def fit_ink(font_path, text, ink_px):
    """二分找到使墨迹高度 ≈ ink_px 的字号。"""
    lo, hi = 4, 400
    for _ in range(24):
        mid = (lo + hi) / 2
        f = ImageFont.truetype(font_path, int(round(mid)))
        im = Image.new("L", (400, 400), 0)
        ImageDraw.Draw(im).text((200, 200), text, font=f, fill=255, anchor="mm")
        ys, _ = np.nonzero(np.asarray(im))
        h = (ys.max() - ys.min() + 1) if len(ys) else 0
        if h < ink_px:
            lo = mid
        else:
            hi = mid
    return int(round((lo + hi) / 2))


def draw_card(with_new, badge_src, text_off):
    W, H = int(CW * S) + 60, int(CH * S) + 60
    ox, oy = 30 + CW * S / 2, 30 + CH * S / 2     # 卡原点(0,0)在画布上的位置
    base = Image.new("RGBA", (W, H), (18, 20, 26, 255))
    for layer in ("Cards/Frame/Frame_Body.png", "Cards/Frame/Frame_NamePlate.png",
                  "Cards/Frame/Frame_ArtWindow.png", "Cards/Frame/Frame_StatPlate.png",
                  "Cards/Frame/Frame_Edge_1.png"):
        paste_center(base, load(layer), 0, 0, CW, CH, ox, oy)
    paste_center(base, load("Cards/Summon/Hero/1/SummonCard_{01103}.png"), 0, 1.8, 64, 84, ox, oy)
    d = ImageDraw.Draw(base)
    return base, d, ox, oy


def render(new):
    W, H = int(CW * S) + 60, int(CH * S) + 60
    im, d, ox, oy = draw_card(new, None, None)
    name_font = ImageFont.truetype(FONT, fit_ink(FONT, "腐化之心", 8.2 * S))
    d.text((ox + 0 * S, oy - 58.5 * S + int(8.2 * S * 0.62)), "腐化之心", font=name_font,
           fill=(232, 209, 138), anchor="lm")
    for (name, cx, off) in (("Health", -24.0, -2.5), ("Attack", 24.0, -2.5)):
        src = os.path.join(TMP if not new else os.path.join(RES, "UI"), f"{name}_orig.png" if not new else f"{name}.png")
        size = 15.0 if not new else 19.0
        paste_center(im, Image.open(src).convert("RGBA"), cx, -54.0, size, size, ox, oy)
        ink = 9.0 * S
        f = ImageFont.truetype(FONT, fit_ink(FONT, "7", ink))
        tx = cx + (0.0 if not new else off)
        ty = -55.0 if not new else -56.3
        d.text((ox + tx * S, oy - ty * S), "7" if name == "Health" else "4", font=f,
               fill=(247, 243, 236), anchor="mm")
    return im


old = render(False)
new = render(True)
W, H = old.size
canvas = Image.new("RGB", (W * 2 + 18, H), (10, 12, 16))
canvas.paste(old.convert("RGB"), (6, 0))
canvas.paste(new.convert("RGB"), (W + 12, 0))
d = ImageDraw.Draw(canvas)
f = ImageFont.truetype(FONT, 30)
d.text((W + 12 + 14, 10), "实装后", font=f, fill=(240, 234, 220))
d.text((14, 10), "现状", font=f, fill=(240, 234, 220))
# 数值条放大（下缘 30u 高）
z = 3.0
crop_h = int(33 * S)
OY = 30 + CH * S / 2
box = (0, int(OY + 38 * S), W, int(OY + 38 * S) + crop_h)
z1 = old.crop(box).resize((int(W * z / 2), int(crop_h * z / 2)), Image.LANCZOS)
z2 = new.crop(box).resize((int(W * z / 2), int(crop_h * z / 2)), Image.LANCZOS)
out = Image.new("RGB", (z1.size[0] * 2 + 18, H + z1.size[1] + 12), (10, 12, 16))
out.paste(canvas, (0, 0))
out.paste(z1.convert("RGB"), (6, H + 6))
out.paste(z2.convert("RGB"), (z1.size[0] + 12, H + 6))
out.save(OUT)
print("ok", OUT, out.size)
