# -*- coding: utf-8 -*-
"""择牌选定框：①「选定后」实装示意（1:1）②六档费用色并排 ③角部放大。"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
RES = os.path.join(ROOT, "Assets/_Game/Resources")
UI  = os.path.join(RES, "UI")
FONT = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf")
OUT1 = os.path.join(ROOT, "Tools/cardframe/preview/pickframe-installed.png")
OUT2 = os.path.join(ROOT, "Tools/cardframe/preview/pickframe-tiers.png")

CW, CH = 250, 439
PAD = 26
COST_ART = {0: "Hero/1/SummonCard_{01106}.png", 1: "Hero/1/SummonCard_{01103}.png",
            2: "Hero/3/SummonCard_{01335}.png", 3: "Hero/3/SummonCard_{01324}.png",
            4: "Hero/5/SummonCard_{01502}.png", 5: "Hero/5/SummonCard_{01504}.png"}
LABEL = ["0 费", "1 费", "2 费", "3 费", "4 费", "5 费"]
F = lambda s: ImageFont.truetype(FONT, s)


def load(rel):
    return Image.open(os.path.join(RES, rel)).convert("RGBA")


def card(cost):
    im = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    for L in ("Cards/Frame/Frame_Body.png", "Cards/Frame/Frame_NamePlate.png",
              "Cards/Frame/Frame_ArtWindow.png", "Cards/Frame/Frame_StatPlate.png",
              "Cards/Frame/Frame_Edge_%d.png" % cost):
        im.alpha_composite(load(L).resize((CW, CH), Image.LANCZOS))
    art = load("Cards/Summon/" + COST_ART[cost])
    im.alpha_composite(art.resize((int(CW * 0.77), int(CH * 0.575)), Image.LANCZOS),
                       (int(CW * 0.115), int(CH * 0.09)))
    return im


def frame(cost):
    src = Image.open(os.path.join(UI, "PickFrame_%d.png" % cost)).convert("RGBA")
    w, h, b = CW + PAD * 2, CH + PAD * 2, 32
    sw, sh = src.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.alpha_composite(src.crop((0, 0, b, b)), (0, 0))
    out.alpha_composite(src.crop((sw - b, 0, sw, b)), (w - b, 0))
    out.alpha_composite(src.crop((0, sh - b, b, sh)), (0, h - b))
    out.alpha_composite(src.crop((sw - b, sh - b, sw, sh)), (w - b, h - b))
    out.alpha_composite(src.crop((b, 0, sw - b, b)).resize((w - 2 * b, b), Image.LANCZOS), (b, 0))
    out.alpha_composite(src.crop((b, sh - b, sw - b, sh)).resize((w - 2 * b, b), Image.LANCZOS), (b, h - b))
    out.alpha_composite(src.crop((0, b, b, sh - b)).resize((b, h - 2 * b), Image.LANCZOS), (0, b))
    out.alpha_composite(src.crop((sw - b, b, sw, sh - b)).resize((b, h - 2 * b), Image.LANCZOS), (w - b, b))
    return out


def scaled(img, sc, alpha):
    if sc != 1.0:
        img = img.resize((int(img.size[0] * sc), int(img.size[1] * sc)), Image.LANCZOS)
    a = img.split()[3].point(lambda v: int(v * alpha))
    img = img.copy(); img.putalpha(a)
    return img


def paste_center(canvas, img, cx, cy):
    canvas.alpha_composite(img, (int(cx - img.size[0] / 2), int(cy - img.size[1] / 2)))


# ══ ① 选定后（1:1） ══
BG = (17, 19, 25)
W, H = 1320, 700
c = Image.new("RGBA", (W, H), BG + (255,))
d = ImageDraw.Draw(c)
d.text((30, 22), "择牌「选定后」实装示意：选定那张抬起 64px、放大到 1.20，身后亮起自己费用色的框", font=F(26), fill=(238, 233, 222))
gap = CW * (1 + 0.26)
cy = 400
for i, cost in enumerate((1, 3, 4)):
    x = W / 2 + (i - 1) * (gap + 26)
    if i == 1:
        paste_center(c, scaled(frame(cost), 1.20, 1.0), x, cy - 64)
        paste_center(c, scaled(card(cost), 1.20, 1.0), x, cy - 64)
    else:
        paste_center(c, scaled(card(cost), 0.94, 0.30), x, cy)
        bar = Image.new("RGBA", (int(CW * 0.94 * 0.8), 14), (158, 41, 38, 240))
        paste_center(c, bar, x, cy + 26)
        d.text((x - 14, cy + 20), "明弃", font=F(13), fill=(255, 245, 238))
d.text((30, 636), "① 待选（1 费）　　② 选定（3 费，蓝框）　　③ 待选（4 费）", font=F(24), fill=(150, 146, 136))
c.convert("RGB").save(OUT1)

# ══ ②③ 六档 + 角部放大 ══
W2, H2 = 1880, 1210
canvas = Image.new("RGB", (W2, H2), BG)
d = ImageDraw.Draw(canvas)
d.text((34, 26), "择牌「选定框」：框色随被选中卡的费用（0-5 费），材质与 Cards/Frame/Frame_Edge 同剖面", font=F(30), fill=(238, 233, 222))
d.text((34, 92), "① 选定框 + 卡（1:1 显示尺寸 250×439，框每边外扩 26px）", font=F(24), fill=(152, 148, 138))
x0, y0 = 60, 130
for i in range(6):
    x = x0 + i * 300
    canvas.paste(frame(i), (x, y0), frame(i))
    canvas.paste(card(i), (x + PAD, y0 + PAD), card(i))
    d.text((x + PAD + 8, y0), LABEL[i], font=F(26), fill=(226, 214, 186))
d.text((34, 740), "② 左上角 4× 放大（外带剖面 + 圈内细双线 + 锥形角撑 + 角珠）", font=F(24), fill=(152, 148, 138))
for i in range(6):
    x = x0 + i * 300
    crop = frame(i).crop((0, 0, 70, 70)).resize((280, 280), Image.NEAREST)
    canvas.paste(crop, (x, 782), crop)
for i, lab in enumerate(["0费灰", "1费象牙", "2费绿", "3费蓝", "4费紫", "5费金"]):
    d.text((x0 + i * 300, 1076), lab, font=F(22), fill=(150, 146, 136))
canvas.save(OUT2)
print("ok", OUT1)
print("ok", OUT2)
