# -*- coding: utf-8 -*-
"""己方生命 / 能量显示图案 v1 —— 生命=长方形，能量=圆形

场景（Assets/_Game/Scenes/Game.unity，SelfStatOrbs 下）：
  OrbH (300x300, scale 0.8) -> Circle(300x300) + Ring(300x300) + Health 文本(280x90 @ y+11)
  OrbE (300x300, scale 0.5) -> Circle(300x300) + Ring(300x300) + Energy 文本(280x90 @ y+6)
Circle 在 Ring 之下。Ring 上挂 StatOrbAnimator：生命 = Breath(缩放 1->1.12)，能量 = Spin(24 度/秒)。
贴图一律 300x300（与节点等大；Image 是 Simple + PreserveAspect 0，不拉伸）→ 只覆盖 PNG 即可。

画风与 CardFrameV7 / board-layers-v2 / topbar-v1 同源：
  深墨 #06090E、金 #C8A44A（亮 #E4CB84 / 暗 #8A6F2E）、
  生命 #B64848（亮 #CE979C / 暗 #76282E）、能量 #689AD6（亮 #A5C3E7 / 暗 #3C6498）
  —— 四个主色直接取自新版 Icon_Health / Icon_Energy。

生成 + 安装：仓库根目录下  python Tools/cardframe/StatOrbV1.py
"""
import os, math
import numpy as np
from PIL import Image, ImageDraw, ImageChops

DARK  = (0x06, 0x09, 0x0E)
GOLD  = (0xC8, 0xA4, 0x4A)
GOLDL = (0xE4, 0xCB, 0x84)
HP, HPL, HPD = (0xB6, 0x48, 0x48), (0xCE, 0x97, 0x9C), (0x76, 0x28, 0x2E)
EN, ENL, END = (0x68, 0x9A, 0xD6), (0xA5, 0xC3, 0xE7), (0x3C, 0x64, 0x98)
HP_TOP, HP_BOT = (0x2C, 0x15, 0x19), (0x14, 0x09, 0x0C)
EN_TOP, EN_BOT = (0x17, 0x21, 0x30), (0x09, 0x0E, 0x16)
COOL = (0x8E, 0xA2, 0xB4)
# 铂刻闪电（低对比，不与数字抢）
EN_EMB, EN_EMBL, EN_EMBD = (0x1E, 0x3A, 0x57), (0x40, 0x72, 0xA2), (0x0B, 0x10, 0x17)

SS = 4
N  = 300
OUT = "Assets/_Game/Art/Sprites/UI"
ARCHIVE = "Assets/_Game/Art/Sprites/Generated/stat-orb-v1"   # 归档 / 对照稿

DISC_R = 98                 # 圆盘半径（沿用旧盘 bbox 52~247）
ARC_RO, ARC_RI = 118, 108   # 外环带（比旧环细一档）
BAR = (16, 88, 284, 212)    # 血条外框（居中于画布，避开 y94~184 的数字文本框）


def S(v):
    return v * SS


def c(rgb, a=255):
    return (int(rgb[0]), int(rgb[1]), int(rgb[2]), int(a))


def canvas():
    return Image.new("RGBA", (N * SS, N * SS), (0, 0, 0, 0))


def vgrad(top, bot):
    h = N * SS
    a = np.linspace(0, 1, h, dtype=np.float32)[:, None, None]
    t = np.array(top, np.float32)[None, None, :]
    b = np.array(bot, np.float32)[None, None, :]
    arr = np.repeat((t * (1 - a) + b * a).astype(np.uint8), N * SS, axis=1)
    return Image.fromarray(arr, "RGB").convert("RGBA")


def round_mask(box, r):
    m = Image.new("L", (N * SS, N * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle([S(box[0]), S(box[1]), S(box[2]), S(box[3])],
                                        radius=S(r), fill=255)
    return m


def circ_mask(cx, cy, r):
    m = Image.new("L", (N * SS, N * SS), 0)
    ImageDraw.Draw(m).ellipse([S(cx - r), S(cy - r), S(cx + r), S(cy + r)], fill=255)
    return m


def shade(img, mask, color):
    img.paste(Image.new("RGBA", img.size, color), (0, 0), mask)


def heart(cx, cy, w, h, n=240):
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((S(cx + x * w / 32.0), S(cy - (y + 2.7) * h / 28.6)))
    return pts


BOLT = [(0.60, 0.00), (0.17, 0.58), (0.44, 0.58), (0.37, 1.00), (0.83, 0.40), (0.55, 0.40)]


def bolt(cx, cy, w, h):
    return [(S(cx + (px - 0.5) * w), S(cy + (py - 0.5) * h)) for (px, py) in BOLT]


def glyph(img, pts, base, light, dark, lw=6):
    """平涂字形：主色 + 一块硬边亮面（左上）+ 一块硬边暗面（右下）+ 深墨描边"""
    d = ImageDraw.Draw(img)
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    shade(img, m, c(base))
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    w, h = x1 - x0, y1 - y0
    lobe = Image.new("L", img.size, 0)
    ImageDraw.Draw(lobe).ellipse([x0 - w * 0.08, y0 - h * 0.08, x0 + w * 0.62, y0 + h * 0.54], fill=255)
    shade(img, ImageChops.multiply(m, lobe), c(light))
    tail = Image.new("L", img.size, 0)
    ImageDraw.Draw(tail).ellipse([x0 + w * 0.30, y0 + h * 0.34, x1 + w * 0.10, y1 + h * 0.10], fill=255)
    shade(img, ImageChops.multiply(m, tail), c(dark))
    d.line(pts + [pts[0]], fill=c(DARK), width=max(1, int(round(S(lw)))), joint="curve")


def arc_band(d, cx, cy, ro, ri, a0, a1, color):
    d.arc([S(cx - ro), S(cy - ro), S(cx + ro), S(cy + ro)], a0, a1, fill=color,
          width=max(1, int(round(S(ro - ri)))))
    r = (ro + ri) / 2.0
    rad = (ro - ri) / 2.0
    for a in (a0, a1):
        px = cx + r * math.cos(math.radians(a))
        py = cy + r * math.sin(math.radians(a))
        d.ellipse([S(px - rad), S(py - rad), S(px + rad), S(py + rad)], fill=color)


def gold_arc(d, cx, cy, ro, ri, a0, a1):
    arc_band(d, cx, cy, ro + 2.5, ri - 2.5, a0, a1, c(DARK))
    arc_band(d, cx, cy, ro, ri, a0, a1, c(GOLD))
    d.arc([S(cx - ro), S(cy - ro), S(cx + ro), S(cy + ro)], a0, a1, fill=c(GOLDL),
          width=max(1, int(round(S(2.2)))))


def frame_rect(d, box, r, t=7):
    x0, y0, x1, y1 = box
    d.rounded_rectangle([S(x0 - t), S(y0 - t), S(x1 + t), S(y1 + t)], radius=S(r + t),
                        outline=c(DARK), width=max(1, int(round(S(3.5)))))
    d.rounded_rectangle([S(x0), S(y0), S(x1), S(y1)], radius=S(r), outline=c(GOLD),
                        width=max(1, int(round(S(t)))))
    d.rounded_rectangle([S(x0 - 2), S(y0 - 2), S(x1 + 2), S(y1 + 2)], radius=S(r + 2),
                        outline=c(GOLDL), width=max(1, int(round(S(2.5)))))


# ══ 生命：长方形 ═══════════════════════════════════════════════════
def health_circle():
    img = canvas()
    img.paste(vgrad(HP_TOP, HP_BOT), (0, 0), round_mask(BAR, 22))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([S(BAR[0] + 13), S(BAR[1] + 13), S(BAR[2] - 13), S(BAR[3] - 13)],
                        radius=S(13), outline=c(GOLD, 92), width=max(1, int(round(S(2.5)))))
    d.line([(S(BAR[0] + 24), S(BAR[1] + 16)), (S(BAR[2] - 24), S(BAR[1] + 16))],
           fill=c(COOL, 74), width=max(1, int(round(S(3)))))
    glyph(img, heart(58, 150, 62, 58), HP, HPL, HPD, lw=6)
    d.line([(S(104), S(112)), (S(104), S(188))], fill=c(GOLD, 128),
           width=max(1, int(round(S(3)))))
    return img


def health_ring():
    img = canvas()
    frame_rect(ImageDraw.Draw(img), BAR, 22, t=7)
    return img


# ══ 能量：圆形 ═════════════════════════════════════════════════════
def energy_circle():
    img = canvas()
    img.paste(vgrad(EN_TOP, EN_BOT), (0, 0), circ_mask(150, 150, DISC_R))
    d = ImageDraw.Draw(img)
    d.ellipse([S(150 - DISC_R), S(150 - DISC_R), S(150 + DISC_R), S(150 + DISC_R)],
              outline=c(DARK), width=max(1, int(round(S(4)))))
    d.ellipse([S(150 - 88), S(150 - 88), S(150 + 88), S(150 + 88)],
              outline=c(GOLD, 96), width=max(1, int(round(S(2.5)))))
    d.arc([S(150 - 82), S(150 - 82), S(150 + 82), S(150 + 82)], 200, 340,
          fill=c(COOL, 70), width=max(1, int(round(S(3)))))
    glyph(img, bolt(150, 150, 56, 86), EN_EMB, EN_EMBL, EN_EMBD, lw=3)
    return img


def energy_ring():
    img = canvas()
    d = ImageDraw.Draw(img)
    # 三段不等长金弧 + 一枚菱形标记 —— 让 StatOrbAnimator 的 24 度/秒自转看得出来
    for (a0, a1) in ((-58, 58), (96, 158), (214, 244)):
        gold_arc(d, 150, 150, ARC_RO, ARC_RI, a0, a1)
    mx = 150 + 113 * math.cos(math.radians(286))
    my = 150 + 113 * math.sin(math.radians(286))
    dia = [(S(mx), S(my - 15)), (S(mx + 15), S(my)), (S(mx), S(my + 15)), (S(mx - 15), S(my))]
    d.polygon(dia, fill=c(GOLDL))
    d.line(dia + [dia[0]], fill=c(DARK), width=max(1, int(round(S(3)))))
    return img


FILES = {"SelfHealthCircle.png": health_circle, "SelfHealthRing.png": health_ring,
         "SelfEnergyCircle.png": energy_circle, "SelfEnergyRing.png": energy_ring}


def main():
    for d in (OUT, ARCHIVE):
        os.makedirs(d, exist_ok=True)
    for name, fn in FILES.items():
        im = fn().resize((N, N), Image.LANCZOS)
        for d in (OUT, ARCHIVE):
            im.save(os.path.join(d, name))
        print("  %-24s %s" % (name, os.path.join(OUT, name)))
    print("done", len(FILES))


if __name__ == "__main__":
    main()