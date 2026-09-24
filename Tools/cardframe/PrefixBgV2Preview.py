# -*- coding: utf-8 -*-
"""前缀底图 v2 预览：素材表 + 实机尺寸（带原画 / 无原画）+ 3x 放大。"""
import os, sys, glob, io, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.getcwd()
FR = "Assets/_Game/Art/Sprites/Generated/cardframe-v7"
BG = "Assets/_Game/Resources/Cards/PrefixArtBG"
PREV = "Tools/cardframe/preview"

ORDER = ["Psychic", "Abyss", "Mech", "Blood", "Scroll", "Common", "Spell"]
CN = {"Psychic": "\u7075\u80FD", "Abyss": "\u6E0A", "Mech": "\u673A\u68B0", "Blood": "\u8840\u6B4C",
      "Scroll": "\u795E\u7075\u753B\u5377", "Common": "\u901A\u7528\uff08\u65E0\u524D\u7F00\uFF09",
      "Spell": "\u6CD5\u672F\u901A\u7528"}
# 用真实前缀的实卡：灵能 01126 / 渊 01102 / 机械 01114 / 神灵画卷 01110；血歌目前无卡，借 01103 看色
SAMPLES = {"Psychic": ("01126", 1), "Abyss": ("01102", 1), "Mech": ("01114", 1),
           "Blood": ("01103", 1), "Scroll": ("01110", 1), "Common": ("01126", 3),
           "Spell": (None, 2)}

def font(sz):
    for p in ("C:/Windows/Fonts/msyh.ttc", "C:/Windows/Fonts/simhei.ttf", "C:/Windows/Fonts/msyhbd.ttc"):
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, sz)
            except Exception:
                pass
    return ImageFont.load_default()

def runs(v):
    out = []; s = None
    for i, b in enumerate(v):
        if b and s is None: s = i
        if not b and s is not None: out.append((s, i - 1)); s = None
    if s is not None: out.append((s, len(v) - 1))
    return out

def window_rect(path):
    a = np.array(Image.open(path).convert("RGBA"))[:, :, 3]
    cy, cx = a.shape[0] // 2, a.shape[1] // 2
    hr = runs(a[cy] > 8); vr = runs(a[:, cx] > 8)
    return (hr[0][0], vr[0][0], hr[-1][1] + 1, vr[-1][1] + 1)

def load(p):
    return Image.open(p).convert("RGBA")

def compose(tid, cost, prefix_key, spell=False, with_art=True):
    win = WSP if spell else WSU
    body = "Card_Body_Spell.png" if spell else "Card_Body.png"
    plate2 = "Card_StatPlate_Spell.png" if spell else "Card_StatPlate.png"
    aes = "Card_ArtWindow_Spell.png" if spell else "Card_ArtWindow.png"
    canvas = Image.new("RGBA", (768, 1344), (0, 0, 0, 0))
    x0, y0, x1, y1 = win
    bh = y1 - y0
    canvas.alpha_composite(load(os.path.join(BG, prefix_key + ".png")).resize((x1 - x0, bh), Image.LANCZOS), (x0, y0))
    if with_art and tid:
        ap = "Assets/_Game/Resources/Cards/Summon/Hero/%d/SummonCard_{%s}.png" % (cost, tid)
        if os.path.exists(ap):
            canvas.alpha_composite(load(ap).resize((x1 - x0, bh), Image.LANCZOS), (x0, y0))
    canvas.alpha_composite(load(os.path.join(FR, body)))
    canvas.alpha_composite(load(os.path.join(FR, "Card_NamePlate.png")))
    canvas.alpha_composite(load(os.path.join(FR, plate2)))
    canvas.alpha_composite(load(os.path.join(FR, aes)))
    canvas.alpha_composite(load(os.path.join(FR, "Card_Edge_%d.png" % cost)))
    return canvas

WSU = window_rect(os.path.join(FR, "Card_ArtWindow.png"))
WSP = window_rect(os.path.join(FR, "Card_ArtWindow_Spell.png"))
print("summon window", WSU, "spell window", WSP)

CW, CH = 300, 400
sheet = Image.new("RGBA", (CW * 7 + 16 * 8, CH + 66), (16, 18, 22, 255))
d = ImageDraw.Draw(sheet)
d.text((16, 12), "prefix BG v2  \u2014  \u6BCF\u4E2A\u524D\u7F00\u81EA\u5DF1\u7684\u5E95 + \u8FB9\u5E26/\u56DB\u89D2\u8EAB\u4EFD",
       font=font(24), fill=(235, 238, 245))
for i, k in enumerate(ORDER):
    im = load(os.path.join(BG, k + ".png")).resize((CW, CH), Image.LANCZOS)
    x = 16 + i * (CW + 16)
    sheet.alpha_composite(im, (x, 48))
    d.text((x + 4, 48 + CH + 4), CN[k], font=font(20), fill=(210, 216, 228))
sheet.convert("RGB").save(os.path.join(PREV, "prefixbg-v2-sheet.png"))

CARD_H = 192
ROWS = [("1:1 \u5E26\u539F\u753B", True, 1), ("1:1 \u65E0\u539F\u753B\uFF08\u515C\u5E95\uFF09", False, 1),
        ("3x \u5E26\u539F\u753B", True, 3)]
pad, lab = 20, 26
colw = CARD_H * 3 * 768 // 1344 + pad
Wpx = len(ORDER) * colw + pad
Hpx = 46 + sum(lab + CARD_H * s + pad + 6 for (_, _, s) in ROWS)
ons = Image.new("RGBA", (Wpx, Hpx), (18, 20, 24, 255))
d = ImageDraw.Draw(ons)
d.text((pad, 12), "prefix BG v2 \u5B9E\u673A\u5C3A\u5BF8\uFF08\u5361\u724C\u9AD8 192px\uFF09\u2014\u2014 \u7AD9\u573A\u4E0A\u80FD\u4E0D\u80FD\u4E00\u773C\u770B\u51FA\u524D\u7F00",
       font=font(24), fill=(235, 238, 245))
y = 46
for title, war, sc in ROWS:
    d.text((pad, y), title, font=font(18), fill=(160, 172, 196))
    y += lab
    ch = CARD_H * sc
    for i, k in enumerate(ORDER):
        tid, cost = SAMPLES[k]
        card = compose(tid, cost, k, spell=(k == "Spell"), with_art=war)
        card = card.resize((768 * ch // 1344, ch), Image.LANCZOS)
        x = pad + i * colw
        ons.alpha_composite(card, (x, y))
        d.text((x + 2, y + ch + 3), CN[k] + ("  (" + tid + ")" if tid else ""), font=font(15), fill=(190, 198, 214))
    y += ch + pad + 6
ons.convert("RGB").save(os.path.join(PREV, "prefixbg-v2-onscreen.png"))
print("ok", sheet.size, ons.size)