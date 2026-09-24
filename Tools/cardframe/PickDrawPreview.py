# -*- coding: utf-8 -*-
"""择牌改动示意：滑入起点更靠近终点 + 选定反馈加强。用真卡框按比例画。"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
RES = os.path.join(ROOT, "Assets/_Game/Resources")
FONT = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf")
OUT = os.path.join(ROOT, "Tools/cardframe/preview/pickdraw-tune.png")

CW, CH = 100, 176           # 示意用卡尺寸


def card():
    im = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    for layer in ("Cards/Frame/Frame_Body.png", "Cards/Frame/Frame_NamePlate.png",
                  "Cards/Frame/Frame_ArtWindow.png", "Cards/Frame/Frame_StatPlate.png",
                  "Cards/Frame/Frame_Edge_1.png"):
        im.alpha_composite(Image.open(os.path.join(RES, layer)).convert("RGBA").resize((CW, CH), Image.LANCZOS))
    art = Image.open(os.path.join(RES, "Cards/Summon/Hero/1/SummonCard_{01103}.png")).convert("RGBA")
    im.alpha_composite(art.resize((int(CW * 0.77), int(CH * 0.575)), Image.LANCZOS), (int(CW * 0.115), int(CH * 0.09)))
    return im


CARD = card()
W, H = 1120, 780
canvas = Image.new("RGB", (W, H), (12, 14, 19))
d = ImageDraw.Draw(canvas)
F = lambda s: ImageFont.truetype(FONT, s)


def paste(img, x, y, alpha=1.0, scale=1.0):
    c = img
    if scale != 1.0:
        c = img.resize((int(img.size[0] * scale), int(img.size[1] * scale)), Image.LANCZOS)
    if alpha < 1.0:
        a = c.split()[3].point(lambda v: int(v * alpha))
        c = c.copy(); c.putalpha(a)
    canvas.paste(c, (int(x - c.size[0] / 2), int(y - c.size[1] / 2)), c)
    return (c.size[0], c.size[1])


# ── 上：滑入起点 ──
d.text((30, 24), "① 滑入起点更靠近终点：riseRatio 0.50 → 0.22", font=F(30), fill=(240, 234, 220))
rest_y = 250
bx = 250
paste(CARD, bx, rest_y, 0.30, 0.86)
d.rectangle([bx - 47, rest_y - 82, bx + 47, rest_y + 82], outline=(120, 124, 138), width=2)
d.text((bx - 150, rest_y + 96), "终点（落位）", font=F(22), fill=(150, 146, 134))
for off, col, lab in ((0.50, (196, 92, 88), "旧起点 50% 卡高"), (0.22, (120, 190, 130), "新起点 22% 卡高")):
    y = rest_y + 176 * off + 30
    paste(CARD, bx, y, 0.34, 0.86)
    d.rectangle([bx - 47, y - 82, bx + 47, y + 82], outline=col, width=2)
    d.text((bx + 90, y - 12), lab, font=F(24), fill=col)
    d.line([bx, y - 86, bx, rest_y + 82], fill=col, width=2)

# ── 下：选定前 / 选定后 ──
def row(cx, cy, mode):
    gap = 126
    for i, dx in enumerate((-gap, 0, gap)):
        chosen = (mode == "after" and i == 1)
        other = (mode == "after" and i != 1)
        sc = 1.20 if chosen else (0.94 if other else 1.0)
        al = 0.30 if other else 1.0
        dy = -26 if chosen else 0
        if chosen:
            hl = Image.new("RGBA", (int(CW * sc) + 20, int(CH * sc) + 20), (0, 0, 0, 0))
            ImageDraw.Draw(hl).rectangle([0, 0, hl.size[0] - 1, hl.size[1] - 1], fill=(255, 214, 117, 87))
            canvas.paste(hl, (int(cx + dx - hl.size[0] / 2), int(cy + dy - hl.size[1] / 2)), hl)
        paste(CARD, cx + dx, cy + dy, al, sc)
        if other:
            bar = Image.new("RGBA", (int(CW * 0.8), 14), (158, 41, 38, 240))
            canvas.paste(bar, (int(cx + dx - bar.size[0] / 2), int(cy + 22)), bar)
            d.text((cx + dx - 14, cy + 16), "明弃", font=F(13), fill=(255, 245, 238))


d.text((30, 458), "② 选定反馈：抬起 26→64px、冲一下 1.34 后落到 1.20、身后金框淡入；其余压暗 0.42→0.30 并缩到 0.94",
       font=F(26), fill=(240, 234, 220))
d.text((165, 520), "选定前", font=F(24), fill=(150, 146, 134))
row(300, 628, "before")
d.text((668, 520), "选定后", font=F(24), fill=(150, 146, 134))
row(830, 628, "after")
canvas.save(OUT)
print("ok", OUT)
