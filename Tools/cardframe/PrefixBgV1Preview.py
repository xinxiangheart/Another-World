# -*- coding: utf-8 -*-
"""前缀底图预览：单张对照表 + 放进 v7 卡框的实机模拟（带原画）"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
BG = os.path.join(ROOT, "Assets/_Game/Resources/Cards/PrefixArtBG")
FR = os.path.join(ROOT, "Assets/_Game/Resources/Cards/Frame")
ART = os.path.join(ROOT, "Assets/_Game/Resources/Cards/Summon/Hero/1/SummonCard_{01103}.png")
PR = os.path.join(ROOT, "Tools/cardframe/preview")
CW, CH = 768, 1344
AW = (88, 268, 681, 1043)
try:
    F = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 22)
    F2 = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 26)
except Exception:
    F = F2 = ImageFont.load_default()

def load(p):
    return Image.open(p).convert("RGBA")

def ldr(n):
    return load(os.path.join(FR, n + ".png"))

ORDER = [("Psychic", "灵能"), ("Abyss", "渊"), ("Mech", "机械"), ("Blood", "血歌"),
         ("Scroll", "神灵画卷"), ("Common", "通用（无前缀）"), ("Spell", "法术通用")]

# ── 1) 单张对照表 ─────────────────────────────────────────────
PW, PH = 300, 400
sheet = Image.new("RGB", (7 * PW + 8, PH + 46), (18, 18, 20))
d = ImageDraw.Draw(sheet)
d.text((8, 10), "前缀底图 v1（768×1024，3:4）—— 暗底 + 低对比母题", fill=(235, 235, 235), font=F2)
for i, (k, cn) in enumerate(ORDER):
    im = load(os.path.join(BG, k + ".png")).resize((PW - 20, PH - 20), Image.LANCZOS)
    x = i * PW + 10
    sheet.paste(im, (x, 40), im)
    d.text((x + 2, 40 + PH - 16), cn, fill=(210, 210, 210), font=F)
sheet.save(os.path.join(PR, "prefixbg-v1-sheet.png"))

# ── 2) 放进 v7 卡框（带原画 / 无原画兜底） ────────────────────
art = load(ART)
print("art size", art.size, "has_alpha", art.mode, "alpha_min", np.array(art)[:, :, 3].min())
art_r = art.resize((AW[2] - AW[0], AW[3] - AW[1]), Image.LANCZOS)
S3 = [("common", "Common"), ("abyss", "Abyss"), ("psychic", "Psychic")]
cells = []
for tag, key in S3:
    for with_art in (True, False):
        c = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
        bg = load(os.path.join(BG, key + ".png")).resize((AW[2] - AW[0], AW[3] - AW[1]), Image.LANCZOS)
        c.paste(bg, (AW[0], AW[1]), bg)
        c = Image.alpha_composite(Image.new("RGBA", (CW, CH), (0, 0, 0, 0)), c)
        body = ldr("Frame_Body"); c = Image.alpha_composite(c, body)
        if with_art:
            c = Image.alpha_composite(c, Image.new("RGBA", (CW, CH), (0, 0, 0, 0)))
            tmp = Image.new("RGBA", (CW, CH), (0, 0, 0, 0)); tmp.paste(art_r, (AW[0], AW[1]), art_r)
            c = Image.alpha_composite(c, tmp)
        for n in ("Frame_ArtWindow", "Frame_NamePlate", "Frame_StatPlate"):
            L = ldr(n)
            if n == "Frame_ArtWindow":
                L = L.copy(); L.alpha_composite(Image.new("RGBA", (CW, CH), (0, 0, 0, 0)))
            c = Image.alpha_composite(c, L)
        e = ldr("Frame_Edge_3"); c = Image.alpha_composite(c, e)
        cells.append((c, "%s%s" % (key, "  +原画" if with_art else "  无原画")))

sc = 0.46
w, h = int(CW * sc), int(CH * sc)
sheet2 = Image.new("RGB", (len(cells) * (w + 14) + 14, h + 52), (18, 18, 20))
d2 = ImageDraw.Draw(sheet2)
d2.text((14, 12), "前缀底图 v1 装进 v7 卡框：底图会不会抢原画？", fill=(235, 235, 235), font=F2)
for i, (c, lab) in enumerate(cells):
    x = 14 + i * (w + 14)
    flat = Image.new("RGB", (CW, CH), (10, 13, 18)); flat.paste(c, (0, 0), c)
    sheet2.paste(flat.resize((w, h), Image.LANCZOS), (x, 46))
    d2.text((x + 2, 46 + h + 3), lab, fill=(200, 200, 200), font=F)
sheet2.save(os.path.join(PR, "prefixbg-v1-onscreen.png"))
print("saved previews")
