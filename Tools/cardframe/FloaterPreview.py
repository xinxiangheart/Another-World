# -*- coding: utf-8 -*-
"""伤害飘字预览：材质（before/after）+ 随机弹出方向。用真字体 + 真卡面。"""
import os, math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
RES = os.path.join(ROOT, "Assets/_Game/Resources")
FONTS = os.path.join(ROOT, "Assets/_Game/Fonts")
OUT = os.path.join(ROOT, "Tools/cardframe/preview/floater-pop.png")
UI = lambda s: ImageFont.truetype(os.path.join(FONTS, "NotoSerifCJKsc-Black.otf"), s)
BG = (17, 19, 25)


def text_layer(text, size, face, rim, rim_px, shadow, sh_off):
    """按 TMP 的做法合成：柔投影 → 描边 → 字面。"""
    pad = size
    box = Image.new("RGBA", (size * 4 + pad * 2, size * 2 + pad * 2), (0, 0, 0, 0))
    f = UI(size)
    cx, cy = box.size[0] // 2, box.size[1] // 2
    if shadow is not None:
        sh = Image.new("RGBA", box.size, (0, 0, 0, 0))
        ImageDraw.Draw(sh).text((cx + sh_off, cy + sh_off), text, font=f, fill=shadow, anchor="mm",
                                stroke_width=int(rim_px), stroke_fill=shadow)
        box.alpha_composite(sh.filter(ImageFilter.GaussianBlur(2.4)))
    ImageDraw.Draw(box).text((cx, cy), text, font=f, fill=face, anchor="mm",
                             stroke_width=int(round(rim_px)), stroke_fill=rim)
    return box.crop(box.getbbox())


art = Image.open(os.path.join(RES, "Cards/Summon/Hero/3/SummonCard_{01324}.png")).convert("RGBA").resize((420, 560), Image.LANCZOS)

W, H = 1280, 1180
c = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(c)
d.text((30, 20), "伤害飘字：材质前后 + 随机弹出方向（真字体 NotoSerifCJKsc-Black + 真卡面）", font=UI(28), fill=(238, 233, 222))


def panel(x0, title, before):
    y0 = 60
    c.paste(art, (x0, y0 + 52), art)
    d.text((x0, y0 + 16), title, font=UI(22), fill=(152, 148, 138))
    if before:
        for dy in (150, 320):
            im = text_layer("-7", 36, (255, 51, 51, 255), (255, 51, 51, 255), 0, None, 0)
            c.paste(im, (x0 + 210 - im.size[0] // 2, y0 + 52 + dy), im)
    else:
        for txt, dy in (("-7", 140), ("-12", 310)):
            im = text_layer(txt, 58, (255, 107, 105, 255), (77, 17, 15, 255), 5, (0, 0, 0, 205), 3)
            c.paste(im, (x0 + 210 - im.size[0] // 2, y0 + 52 + dy), im)


panel(50, "① 现在（TMP 默认字体 / 配置失效 / 无描边）", True)
panel(500, "② 改后（Serif Black 58 + 描边 + 柔投影）", False)

for i, (txt, face, rim) in enumerate((("+5", (120, 255, 140, 255), (20, 74, 28, 255)),
                                      ("+3", (255, 226, 100, 255), (74, 60, 6, 255)),
                                      ("抵挡!", (120, 170, 255, 255), (14, 32, 74, 255)))):
    im = text_layer(txt, 46, face, rim, 4, (0, 0, 0, 205), 3)
    c.paste(im, (1010 - im.size[0] // 2, 170 + i * 120), im)

# ── 随机方向扇形（初速方向 + 落点）──
d.text((30, 700), "③ 随机弹出：出生点散开（蓝点）+ 初速方向 ±50° + 抛物线下坠，10 次弹出落点各不相同",
       font=UI(22), fill=(152, 148, 138))
ox, oy, K = 400, 1020, 2.4
d.rectangle([ox - 34, oy - 10, ox + 34, oy + 10], outline=(120, 124, 138), width=2)
d.text((ox - 30, oy + 16), "卡牌", font=UI(17), fill=(150, 146, 134))
random.seed(11)
for k in range(10):
    ang = math.radians(random.uniform(-50, 50))
    spd = 1.6 * (1 + random.uniform(-0.22, 0.22))
    vx, vy = math.sin(ang) * spd, math.cos(ang) * spd
    sx = ox + random.uniform(-0.3, 0.3) * 100 * K
    sy = oy - 30 + random.uniform(-0.18, 0.18) * 100 * K
    wx, wy = sx, sy
    pts = []
    for i in range(30):
        dt = 1.2 / 29
        vy -= 2.6 * dt
        vx -= min(abs(vx), 1.6 * dt) * (1 if vx > 0 else -1)
        wx += vx * dt * 100 * K
        wy -= vy * dt * 100 * K
        pts.append((wx, wy))
    d.line(pts, fill=(150, 74, 72), width=2)
    d.ellipse([sx - 4, sy - 4, sx + 4, sy + 4], fill=(120, 200, 255))
    sc = 1.0 + random.uniform(-0.1, 0.1)
    im = text_layer("-" + str(random.randint(1, 9)), int(58 * sc * 0.5),
                    (255, 107, 105, 255), (77, 17, 15, 255), 4, (0, 0, 0, 205), 3)
    c.paste(im, (int(pts[-1][0] - im.size[0] / 2), int(pts[-1][1] - im.size[1] / 2)), im)
    d.ellipse([pts[-1][0] - 3, pts[-1][1] - 3, pts[-1][0] + 3, pts[-1][1] + 3], outline=(120, 200, 255), width=1)
c.save(OUT)
print("ok", OUT)
