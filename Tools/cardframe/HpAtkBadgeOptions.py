# -*- coding: utf-8 -*-
"""卡面 HP/ATK「数字压在徽章图案上」的四个方案对比图。

几何取自预制体 Card00_New_2D.prefab（卡 83.33 x 146.33，1 卡单位 = 缩放后 1px）：
    HealthIcon  anchoredPos (-24, -54)  sizeDelta 15x15
    HealthText  anchoredPos (-24, -55)  sizeDelta 40x20，fontSize 9.8 自动
    Frame_StatPlate 铺满卡面 -> 暗色数值条 x -37..37、y -68.2..-41.6（高 26.6）
图标与数字同锚点，所以数字正好落在图案上。
"""
import os
from PIL import Image, ImageDraw, ImageFont
import numpy as np

ROOT  = r"C:\Users\22589\Documents\GitHub\Another-World"
PLATE = os.path.join(ROOT, "Assets/_Game/Resources/Cards/Frame/Frame_StatPlate.png")
HPART = os.path.join(ROOT, "Assets/_Game/Resources/UI/Health.png")
FONT  = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf")
FONT_NUM = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf")
OUT   = os.path.join(ROOT, "Tools/cardframe/preview/hpatk-badge-options.png")

UP = 14.0                      # 1 卡单位 = 14 px
BAR = (-37.0, 37.0, -68.2, -41.6)   # 数值条：x0, x1, y0, y1


class View:
    """卡单位 -> 面板像素。"""
    def __init__(self, w, h, ox, oy):
        self.w, self.h, self.ox, self.oy = w, h, ox, oy
        self.im = Image.new("RGBA", (w, h), (14, 16, 22, 255))
        self.d = ImageDraw.Draw(self.im)

    def px(self, x, y):
        return (self.ox + x * UP, self.oy - y * UP)

    def paste(self, img, cx, cy, size_u):
        n = max(1, int(round(size_u * UP)))
        s = img.resize((n, n), Image.LANCZOS)
        x, y = self.px(cx, cy)
        self.im.alpha_composite(s, (int(round(x - n / 2)), int(round(y - n / 2))))

    def num(self, s, cx, cy, ink_u, color=(247, 243, 236, 255), stroke=0):
        f = ImageFont.truetype(FONT_NUM, max(8, int(round(ink_u * UP * 1.42))))
        x, y = self.px(cx, cy)
        self.d.text((x, y), s, font=f, fill=color, anchor="mm",
                    stroke_width=stroke, stroke_fill=(11, 13, 19, 255))

    def label(self, text, size=25, fill=(240, 234, 220, 255), xy=(22, 20)):
        f = ImageFont.truetype(FONT, size)
        self.d.text(xy, text, font=f, fill=fill, anchor="lt")


def split_badge(path):
    """把徽章 PNG 拆成「底盘（金环 + 暗盘）」与「图案（水滴/双剑）」。"""
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).astype(int)
    alpha = a[:, :, 3]
    ys, xs = np.nonzero(alpha > 40)
    cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
    R = (xs.max() - xs.min()) / 2
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    disc = a[int(cy), int(cx - 0.72 * R)][:3]
    mask = (r < 0.80 * R) & (np.abs(a[:, :, :3] - disc).sum(axis=2) > 45) & (alpha > 40)

    plate = a[:, :, :3].copy()
    plate[mask] = disc
    plate_img = Image.fromarray(plate.astype(np.uint8)).convert("RGBA")
    plate_img.putalpha(Image.fromarray(alpha.astype(np.uint8)))

    p = np.zeros_like(a)
    p[:, :, :3] = a[:, :, :3]
    p[:, :, 3] = np.where(mask, alpha, 0)
    return plate_img, Image.fromarray(p.astype(np.uint8))


def pill_image():
    im = Image.open(PLATE).convert("RGBA")
    a = np.asarray(im).astype(int)
    dark = (a[:, :, 0] < 70) & (a[:, :, 1] < 80) & (a[:, :, 2] < 110) & (a[:, :, 3] > 200)
    ys, xs = np.nonzero(dark)
    box = (xs.min() - 3, ys.min() - 3, xs.max() + 4, ys.max() + 4)
    w = int(round((BAR[1] - BAR[0]) * UP))
    h = int(round((BAR[3] - BAR[2]) * UP))
    return im.crop(box).resize((w, h), Image.LANCZOS)


PILL = pill_image()
PLATE_IMG, PICTO_IMG = split_badge(HPART)

PW, PH = 700, 700
OX, OY = 620.0, -372.0        # 面板中 x=0、y=0 的像素位置（只放数值条左半格）


def base_panel():
    v = View(PW, PH, OX, OY)
    x, y = v.px(BAR[0], BAR[3])
    v.im.alpha_composite(PILL, (int(round(x)), int(round(y))))
    return v


def panel_current():
    v = base_panel()
    v.label("① 现状：图标与数字同锚点")
    v.label("(-24,-54) vs (-24,-55) → 数字压在图案正中", 19, (168, 162, 148, 255), (22, 50))
    v.paste(PLATE_IMG, -24, -54, 15)
    v.num("7", -24, -55, 9.4)
    return v.im


def panel_a():
    v = base_panel()
    v.label("② 方案 A：数字搬到数值条上")
    v.label("徽章缩到 0.73 并外移；数字移到徽章内侧的暗条上", 19, (168, 162, 148, 255), (22, 50))
    v.paste(PLATE_IMG, -30.5, -54.5, 11)
    v.num("7", -16.5, -54.9, 12)
    return v.im


def panel_bc():
    v = base_panel()
    v.label("③ 方案 C：徽章拆两层，图案让位")
    v.label("底盘居中压数字底；水滴缩到 0.47 挪到左上角", 19, (168, 162, 148, 255), (22, 50))
    v.paste(PLATE_IMG, -24, -54, 15)
    v.paste(PICTO_IMG, -27.6, -54, 6.6)
    v.num("7", -22.4, -54.6, 9.6)
    return v.im


def panel_d():
    v = base_panel()
    v.label("④ 方案 D：最小改动（缩字 + 描边）")
    v.label("数字缩到 0.75 下移，加深色描边提可读性（重叠仍在）", 19, (168, 162, 148, 255), (22, 50))
    v.paste(PLATE_IMG, -24, -54, 15)
    v.num("7", -24, -57.6, 7.5, stroke=3)
    return v.im


def main():
    panels = [panel_current(), panel_a(), panel_bc(), panel_d()]
    gap, lab = 18, 0
    W = PW * 2 + gap + 48
    H = PH * 2 + gap + 96
    canvas = Image.new("RGBA", (W, H), (10, 12, 16, 255))
    f = ImageFont.truetype(FONT, 34)
    d = ImageDraw.Draw(canvas)
    d.text((24, 18), "HP / ATK 数字与徽章重叠 —— 四个方案（数值条左半格放大 14x）",
           font=f, fill=(240, 234, 220, 255), anchor="lt")
    pos = [(24, 66), (24 + PW + gap, 66), (24, 66 + PH + gap), (24 + PW + gap, 66 + PH + gap)]
    for p, xy in zip(panels, pos):
        frame = Image.new("RGBA", (PW + 2, PH + 2), (58, 62, 76, 255))
        frame.alpha_composite(p, (1, 1))
        canvas.alpha_composite(frame, xy)
    canvas.convert("RGB").save(OUT, quality=95)
    print("OK", OUT, canvas.size)


main()
