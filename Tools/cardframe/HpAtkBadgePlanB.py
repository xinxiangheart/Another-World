# -*- coding: utf-8 -*-
"""HP/ATK 徽章：数字落在数值盘上，图案缩小让到右上角（只被盘遮住左下 1/4）。

图层顺序（与预制体一致）：底盘(金环+暗盘) -> 图案(水滴/双剑，缩到右上角) -> 数值盘 -> 数字
卡单位：卡 83.33x146.33；徽章中心 (-24,-54)  [HP] / (24,-54)  [ATK]
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import numpy as np

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
FONT_NUM = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf")
FONT = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf")
OUT = os.path.join(ROOT, "Tools/cardframe/preview/hpatk-badge-planB.png")

PLATE = (30, 42, 60)
PLATE_RIM = (62, 82, 108)
INK = (247, 243, 236, 255)


def load_badge(name):
    im = Image.open(os.path.join(ROOT, f"Assets/_Game/Resources/UI/{name}.png")).convert("RGBA")
    a = np.asarray(im).astype(int)
    al = a[:, :, 3]
    ys, xs = np.nonzero(al > 40)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    R = (x1 - x0 + 1) / 2
    disc = a[int(cy), int(round(cx - 0.5 * R))][:3].copy()
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    m = (r < 0.80 * R) & (np.abs(a[:, :, :3] - disc).sum(axis=2) > 60) & (al > 50)
    base = a[:, :, :3].copy(); base[m] = disc
    base = np.dstack([base, al]).astype(np.uint8)
    mark = np.zeros_like(a); mark[:, :, :3] = a[:, :, :3]; mark[:, :, 3] = np.where(m, al, 0)
    my, mx = np.nonzero(m)
    return (Image.fromarray(base), Image.fromarray(mark.astype(np.uint8)),
            dict(w=im.size[0], half_src=R / 0.94, mbb=(mx.min(), mx.max(), my.min(), my.max())))


class Panel:
    def __init__(self, w, h, k):
        self.w, self.h, self.k = w, h, k
        self.bx, self.by = w / 2, h * 0.52
        self.im = Image.new("RGBA", (w, h), (13, 15, 20, 255))
        self.d = ImageDraw.Draw(self.im)

    def u2p(self, x, y):
        return (self.bx + x * self.k, self.by - y * self.k)

    def put(self, img, cx, cy, size_u, clip_disc=None):
        n = max(1, int(round(size_u * self.k)))
        s = img.resize((n, n), Image.LANCZOS)
        x, y = self.u2p(cx, cy)
        ox, oy = int(round(x - n / 2)), int(round(y - n / 2))
        if clip_disc is not None:
            layer = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
            layer.alpha_composite(s, (ox, oy))
            ccx, ccy = self.u2p(0, 0); rr = clip_disc * self.k
            mask = Image.new("L", (self.w, self.h), 0)
            ImageDraw.Draw(mask).ellipse([ccx - rr, ccy - rr, ccx + rr, ccy + rr], fill=255)
            layer.putalpha(Image.fromarray(np.minimum(np.asarray(layer.split()[3]),
                                                      np.asarray(mask)).astype(np.uint8)))
            self.im.alpha_composite(layer)
            return
        self.im.alpha_composite(s, (ox, oy))

    def plate(self, rect, disc_r, clip=True, radius_u=1.0):
        x0, y0, x1, y1 = rect
        px0, py0 = self.u2p(x0, y1); px1, py1 = self.u2p(x1, y0)
        r = radius_u * self.k
        lay = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        dl = ImageDraw.Draw(lay)
        dl.rounded_rectangle([px0 + 2.5, py0 + 4.5, px1 + 2.5, py1 + 4.5],
                             radius=r, fill=(0, 0, 0, 120))
        sh = lay.filter(ImageFilter.GaussianBlur(3.5))
        lay2 = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        ImageDraw.Draw(lay2).rounded_rectangle([px0, py0, px1, py1], radius=r,
                                               fill=PLATE + (255,), outline=PLATE_RIM + (255,),
                                               width=max(1, int(self.k * 0.13)))
        if clip:
            cx, cy = self.u2p(0, 0); rr = disc_r * self.k
            mask = Image.new("L", (self.w, self.h), 0)
            ImageDraw.Draw(mask).ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=255)
            cut = np.minimum(np.asarray(lay2.split()[3]), np.asarray(mask)).astype(np.uint8)
            lay2.putalpha(Image.fromarray(cut))
            sh.putalpha(Image.fromarray(np.minimum(np.asarray(sh.split()[3]),
                                                   np.asarray(mask)).astype(np.uint8)))
        self.im.alpha_composite(sh)
        self.im.alpha_composite(lay2)

    def num(self, s, cx, cy, ink_u):
        f = ImageFont.truetype(FONT_NUM, max(8, int(round(ink_u * self.k * 1.44))))
        x, y = self.u2p(cx, cy)
        self.d.text((x, y), s, font=f, fill=INK, anchor="mm")

    def label(self, t, size=25, fill=(240, 234, 220, 255), xy=(20, 14)):
        self.d.text(xy, t, font=ImageFont.truetype(FONT, size), fill=fill, anchor="lt")


# ── 布局参数（卡单位）──
S = 19.0            # 徽章边长（现状 15）
DISC_R = 0.86 * S / 2
MARK = 9.5          # 图案方形边长
MARK_C = (4.3, 2.4)  # 图案中心（右上）
PLATE_RECT = (-8.2, -7.4, 4.3, 2.4)
NUM_C = (-1.7, -2.25)
NUM_INK = 8.6


def scal(panel, base, mark, s=S, mark_u=MARK, mark_c=MARK_C, plate_rect=PLATE_RECT,
         num_ink=NUM_INK, num_c=NUM_C, text="7", clip=True):
    panel.put(base, 0, 0, s)
    panel.put(mark, mark_c[0], mark_c[1], mark_u, clip_disc=0.86 * s / 2)
    panel.plate(plate_rect, 0.86 * s / 2, clip=clip)
    panel.num(text, num_c[0], num_c[1], num_ink)


def main():
    hp_base, hp_mark, hp_i = load_badge("Health")
    at_base, at_mark, at_i = load_badge("Attack")
    PW, PH, GAP = 760, 760, 16
    panels = []

    p = Panel(PW, PH, 21)
    p.label("① 现状：徽章 15u，数字 9u 直接压在图案正中")
    p.put(hp_base, 0, 0, 15); p.put(hp_mark, 0, 0, 15)
    p.num("7", 0, -1.0, 9.0)
    panels.append(p.im)

    p = Panel(PW, PH, 21)
    p.label("② 新方案：徽章 19u；图案缩到右上角，数值盘压住它左下 1/4")
    scal(p, hp_base, hp_mark)
    panels.append(p.im)

    p = Panel(PW, PH, 21)
    p.label("③ 同一布局，数值盘加高一点并压到金环上（外挂铭牌式）")
    scal(p, hp_base, hp_mark, clip=False)
    panels.append(p.im)

    p = Panel(PW, PH, 21)
    p.label("④ 攻击力徽章同样处理（双剑让到右上角）")
    scal(p, at_base, at_mark, mark_u=10.0, text="4")
    panels.append(p.im)

    # 真实尺寸对照
    strip = Image.new("RGBA", (PW * 2 + GAP + 48, 330), (10, 12, 16, 255))
    d = ImageDraw.Draw(strip)
    f = ImageFont.truetype(FONT, 26)
    for idx, new in enumerate((False, True)):
        for j, k in enumerate((1, 2, 4)):
            cell = Panel(240, 230, k)
            if new:
                scal(cell, hp_base, hp_mark)
            else:
                cell.put(hp_base, 0, 0, 15); cell.put(hp_mark, 0, 0, 15)
                cell.num("7", 0, -1.0, 9.0)
            x = 24 + idx * (PW + GAP // 2) + j * 240
            strip.alpha_composite(cell.im, (x, 70))
            d.text((x + 100, 40), f"x{k}", font=f, fill=(150, 145, 132, 255), anchor="lt")
        d.text((24 + idx * (PW + GAP // 2), 8), "现状 15u" if not new else "新方案 19u",
               font=ImageFont.truetype(FONT, 30), fill=(240, 234, 220, 255), anchor="lt")
    d.text((24, 302), "真实卡面尺寸（x1 = 屏幕 1px = 1 卡单位）", font=f,
           fill=(150, 145, 132, 255), anchor="lt")

    W = PW * 2 + GAP + 48
    H = 96 + (PH + GAP) * 2 + 16 + 330
    canvas = Image.new("RGBA", (W, H), (10, 12, 16, 255))
    ImageDraw.Draw(canvas).text((24, 22),
        "数值盘方案：数字落在数值盘上，图案缩小让到右上角（盘压住其左下 1/4）",
        font=ImageFont.truetype(FONT, 32), fill=(240, 234, 220, 255), anchor="lt")
    pos = [(24, 74), (24 + PW + GAP, 74), (24, 74 + PH + GAP), (24 + PW + GAP, 74 + PH + GAP)]
    for pim, xy in zip(panels, pos):
        fr = Image.new("RGBA", (PW + 2, PH + 2), (58, 62, 76, 255))
        fr.alpha_composite(pim, (1, 1))
        canvas.alpha_composite(fr, xy)
    canvas.alpha_composite(strip, (0, 74 + (PH + GAP) * 2 + 8))
    canvas.convert("RGB").save(OUT, quality=95)
    print("OK", OUT, canvas.size)


main()
