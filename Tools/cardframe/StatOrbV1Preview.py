# -*- coding: utf-8 -*-
"""生命 / 能量显示图案 v1 预览：旧 vs 新（1:1 实机尺寸）+ 2x 放大 + 能量环自转帧。"""
import os, math
from PIL import Image, ImageDraw, ImageFont

UI = "Assets/_Game/Art/Sprites/UI"
OLD = os.path.join(os.environ["TEMP"], "selforb-backup-20260924")
PREV = "Tools/cardframe/preview"
CARD = {1: 0.8, 2: 0.5}          # OrbH / OrbE 的父级 scale


def font(sz):
    for p in ("C:/Windows/Fonts/msyhbd.ttc", "C:/Windows/Fonts/msyh.ttc", "C:/Windows/Fonts/simhei.ttf"):
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, sz)
            except Exception:
                pass
    return ImageFont.load_default()


def load(p):
    return Image.open(p).convert("RGBA")


def orb(kind, new=True, scale=1.0, rot=0.0):
    base = UI if new else OLD
    circ = load(os.path.join(base, "Self%sCircle.png" % kind))
    ring = load(os.path.join(base, "Self%sRing.png" % kind))
    if rot:
        ring = ring.rotate(rot, resample=Image.BICUBIC, center=(150, 150))
    im = Image.alpha_composite(circ, ring)
    w = max(1, int(round(300 * scale)))
    return im.resize((w, w), Image.LANCZOS)


def number(im, text, scale, dy=0):
    d = ImageDraw.Draw(im)
    f = font(max(10, int(round(78 * scale))))
    bb = d.textbbox((0, 0), text, font=f)
    x = im.size[0] / 2 - (bb[2] - bb[0]) / 2 - bb[0]
    y = im.size[1] / 2 - (bb[3] - bb[1]) / 2 - bb[1] + dy
    for ox, oy in ((-2, 0), (2, 0), (0, -2), (0, 2)):
        d.text((x + ox, y + oy), text, font=f, fill=(6, 9, 14, 235))
    d.text((x, y), text, font=f, fill=(0xEC, 0xE9, 0xDF, 255))
    return im


def backdrop(w, h, base=(10, 14, 20)):
    im = Image.new("RGBA", (w, h), base + (255,))
    d = ImageDraw.Draw(im)
    for i in range(0, h, 4):
        k = 1.0 - i / float(h) * 0.35
        d.line([(0, i), (w, i)], fill=(int(10 * k) + 3, int(14 * k) + 4, int(20 * k) + 6, 255))
    return im


TITLE = font(26)
LAB = font(17)

# ── 布局 ────────────────────────────────────────────────────────
H1 = 240 + 30        # 1:1 行
H2 = 600 + 30        # 2x 行
H3 = 170 + 30        # 自转行
W = 1500
img = Image.new("RGBA", (W, 60 + H1 + H2 + H3 + 40), (16, 18, 22, 255))
d = ImageDraw.Draw(img)
d.text((22, 14), "\u5DF1\u65B9\u751F\u547D / \u80FD\u91CF\u56FE\u6848 v1 \u2014\u2014 \u751F\u547D\uFF1D\u957F\u65B9\u5F62\uFF0C\u80FD\u91CF\uFF1D\u5706\u5F62",
       font=TITLE, fill=(235, 238, 245))

# 行1：1:1 旧 vs 新
y = 60
d.text((22, y), "1:1 \u5B9E\u673A\u5C3A\u5BF8\uFF08\u5DE6\uFF1D\u65E7 / \u53F3\uFF1D\u65B0\uFF09 \u6570\u5B57\u4EC5\u4E3A\u793A\u610F\uFF0C\u672A\u6539", font=LAB, fill=(160, 172, 196))
y += 30
x = 30
for kind, sc, txt in (("Health", 0.8, "20"), ("Energy", 0.5, "6")):
    for new in (False, True):
        o = orb(kind, new, sc)
        number(o, txt, sc, dy=-3 * sc)
        img.alpha_composite(o, (x, y + (240 - o.size[1]) // 2))
        x += o.size[0] + 26
    x += 40
y += H1

# 行2：2x 放大
d.text((22, y), "2x \u653E\u5927", font=LAB, fill=(160, 172, 196))
y += 30
for kind, txt in (("Health", "20"), ("Energy", "6")):
    o = orb(kind, True, 2.0)
    number(o, txt, 2.0, dy=-9)
    img.alpha_composite(o, (30 + (0 if kind == "Health" else 640), y))

# 行3：能量环自转（不对称设计 → 转得出来）
d.text((22, y), "\u80FD\u91CF\u73AF\u81EA\u8F6C\uFF08StatOrbAnimator Spin 24\u00B0/\u79D2\uFF09\u2014\u2014 \u4E0D\u5BF9\u79F0\u8BBE\u8BA1\u624D\u770B\u5F97\u51FA\u8F6C\u52A8",
       font=LAB, fill=(160, 172, 196))
y += 30
for i, rot in enumerate((0, 45, 90, 135)):
    o = orb("Energy", True, 0.5, rot=rot)
    img.alpha_composite(o, (30 + i * 190, y + 8))
    d.text((30 + i * 190, y + 168), "%d\u00B0" % rot, font=LAB, fill=(190, 198, 214))

img.convert("RGB").save(os.path.join(PREV, "stat-orb-v1.png"))
print("ok", img.size)