# -*- coding: utf-8 -*-
"""HUD 圆形按钮 v1 —— 抽牌 DrawCircle / 隐藏手牌 EyeCircle

场景（Assets/_Game/Scenes/Game.unity，CardCanvas 下）：
  DrawCardButton   (180x120 @ 810,-420)     Image -> DrawCircle.png  + 子节点 DrawCountText(数字)
  ToggleHandButton (180x120 @ 808.5,-253.5) Image -> EyeCircle.png
  两个 Image 都是 Simple / PreserveAspect 0 / 无 9-slice；768x512 与节点 180x120 比例一致，不拉伸。
  ToggleHand.cs 不改图也不改色 -> 单态图标即可（悬停只是 Button ColorTint 相乘）。

尺寸口径：贴图 768 贴图像素 <-> 屏幕 180px（1 贴图像素 = 0.234 屏幕像素）。
  盘直径沿用旧图 bbox：494 贴图像素 = 115.6 屏幕像素，盘心/轮廓位置一律不动。
  * 数字 DrawCountText 居中压在盘心（实机量：约占盘径 0.21~0.32）-> 盘心让给数字：
    抽牌母题做成**压在盘底的小卡对**（不跟数字抢中心），编号 / 刻度留在盘环上。
  * 眼睛：旧图巩膜 196x110 居中、虹膜直径约 70，本版沿用同尺寸同位置。

画风与 CardFrameV7 / topbar-v1 / stat-orb-v1 同源：深蓝黑石面 + 金细线。
  墨 #06090E、金 #C8A44A（亮 #E4CB84 / 暗 #8A6F2E）、钢 #8EA2B4、
  盘面渐变 #1E2938 -> #0C111A（同 topbar-v1 顶栏面）、
  奶油三色取自新版 Icon_CardCount：#D6C298 / #E0D2B0 / #9A8664。

生成 + 安装：仓库根目录下  python Tools/cardframe/HudButtonV1.py
"""
import os, math
import numpy as np
from PIL import Image, ImageDraw, ImageChops

W, H = 768, 512
SS = 4
CW, CH = W * SS, H * SS
CX, CY = W / 2.0, H / 2.0

R_OUT = 247.0     # 轮廓半径（旧图 bbox 494 / 2）
W_INK = 14.0      # 墨边（由外向内描：234..247）
R_RING = 230.0    # 金环外沿
W_RING = 9.0      # 金环宽
R_HAIR = 208.0    # 内侧淡金发丝线
W_HAIR = 3.0
R_ARC = 196.0     # 钢色顶弧

DARK = (0x06, 0x09, 0x0E)
GOLD = (0xC8, 0xA4, 0x4A)
GOLDL = (0xE4, 0xCB, 0x84)
GOLDD = (0x8A, 0x6F, 0x2E)
COOL = (0x8E, 0xA2, 0xB4)
FACE_TOP = (0x1E, 0x29, 0x38)
FACE_BOT = (0x0C, 0x11, 0x1A)
CREAM = (0xD6, 0xC2, 0x98)
CREAML = (0xE0, 0xD2, 0xB0)
CREAMD = (0x9A, 0x86, 0x64)
CREAMDD = (0x7E, 0x6C, 0x4E)
SCLERA = (0xC6, 0xBD, 0x9F)   # 眼白：骨白（比描边暗一档）

OUT = "Assets/_Game/Resources/UI/HUD"
ARCHIVE = "Assets/_Game/Art/Sprites/Generated/hud-btn-v1"


def S(v):
    return int(round(v * SS))


def c(rgb, a=255):
    return (int(rgb[0]), int(rgb[1]), int(rgb[2]), int(a))


def canvas():
    return Image.new("RGBA", (CW, CH), (0, 0, 0, 0))


def ellipse_box(r):
    return [S(CX - r), S(CY - r), S(CX + r), S(CY + r)]


def circ_mask(r):
    m = Image.new("L", (CW, CH), 0)
    ImageDraw.Draw(m).ellipse(ellipse_box(r), fill=255)
    return m


def vgrad(top, bot):
    a = np.linspace(0, 1, CH, dtype=np.float32)[:, None, None]
    t = np.array(top, np.float32)[None, None, :]
    b = np.array(bot, np.float32)[None, None, :]
    arr = np.repeat((t * (1 - a) + b * a).astype(np.uint8), CW, axis=1)
    return Image.fromarray(arr, "RGB")


def vignette(strength=0.30, power=2.2):
    y, x = np.mgrid[0:CH, 0:CW].astype(np.float32)
    d = np.sqrt(((x - CX * SS) / (R_OUT * SS)) ** 2 + ((y - CY * SS) / (R_OUT * SS)) ** 2)
    v = np.clip(1.0 - strength * np.clip(d, 0, 1) ** power, 0, 1) * 255.0
    return Image.fromarray(v.astype(np.uint8), "L")


def shade(img, mask, color):
    img.paste(Image.new("RGBA", img.size, color), (0, 0), mask)


def diamond(d, cx, cy, r, fill, outline=DARK, lw=3.0):
    pts = [(S(cx), S(cy - r)), (S(cx + r), S(cy)), (S(cx), S(cy + r)), (S(cx - r), S(cy))]
    d.polygon(pts, fill=c(fill))
    d.line(pts + [pts[0]], fill=c(outline), width=max(1, S(lw)), joint="curve")


def base_disc():
    img = canvas()
    vig = vignette()
    face = ImageChops.multiply(vgrad(FACE_TOP, FACE_BOT), Image.merge("RGB", (vig, vig, vig)))
    img.paste(face, (0, 0), circ_mask(R_OUT))
    d = ImageDraw.Draw(img)
    d.arc(ellipse_box(R_ARC), 205, 335, fill=c(COOL, 78), width=max(1, S(4)))
    d.ellipse(ellipse_box(R_HAIR), outline=c(GOLDL, 88), width=max(1, S(W_HAIR)))
    for k in range(8):
        a = math.radians(22.5 + 45.0 * k)
        for rr in (R_HAIR - 12, R_HAIR - 22):
            px, py = CX + rr * math.cos(a), CY + rr * math.sin(a)
            d.line([(S(px), S(py - 5)), (S(px), S(py + 5))], fill=c(GOLDL, 120), width=max(1, S(2.6)))
    for a_deg in (45, 135, 225, 315):
        a = math.radians(a_deg)
        diamond(d, CX + (R_HAIR + 11) * math.cos(a), CY + (R_HAIR + 11) * math.sin(a), 15.0, GOLDL, DARK, 3.0)
    gx0, gy0, gx1, gy1 = ellipse_box(R_RING)
    d.ellipse([gx0 - S(4), gy0 - S(4), gx1 + S(4), gy1 + S(4)], outline=c(DARK, 210), width=max(1, S(3)))
    d.ellipse([gx0, gy0, gx1, gy1], outline=c(GOLD), width=S(W_RING))
    d.ellipse([gx0, gy0, gx1, gy1], outline=c(GOLDL), width=max(1, S(2.4)))
    d.ellipse(ellipse_box(R_OUT), outline=c(DARK), width=S(W_INK))
    return img


def card_tile(w, h, front):
    pw, ph = S(w), S(h)
    pad = S(16)
    im = Image.new("RGBA", (pw + 2 * pad, ph + 2 * pad), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    box = [pad, pad, pad + pw, pad + ph]
    rad = S(15)
    body = CREAM if front else CREAMD
    d.rounded_rectangle(box, radius=rad, fill=c(body))
    band_h = int(ph * (0.20 if front else 0.14))
    top = CREAML if front else CREAM
    d.rounded_rectangle([box[0], box[1], box[2], box[1] + band_h + rad], radius=rad, fill=c(top))
    d.rectangle([box[0], box[1] + band_h, box[2], box[1] + band_h + rad], fill=c(top))
    sh_h = int(ph * 0.30)
    d.rectangle([box[0], box[3] - sh_h, box[2], box[3]], fill=c(CREAMD if front else CREAMDD))
    dr = int(pw * 0.17)
    mx, my = (box[0] + box[2]) // 2, (box[1] + box[3]) // 2
    d.polygon([(mx, my - dr), (mx + dr, my), (mx, my + dr), (mx - dr, my)], fill=c(CREAMD if front else CREAMDD))
    d.rounded_rectangle(box, radius=rad, outline=c(DARK), width=S(6))
    return im


def place(img, tile, cx, cy, rot):
    r = tile.rotate(rot, resample=Image.BICUBIC, expand=True)
    img.paste(r, (S(cx) - r.width // 2, S(cy) - r.height // 2), r)


def deck_motif(img):
    place(img, card_tile(96, 128, False), CX - 34, CY + 188, -19)
    place(img, card_tile(96, 128, True), CX + 10, CY + 190, 11)


def eye_motif(img):
    hw, hu, hl = 100.0, 56.0, 54.0
    n = 220
    up, lo = [], []
    for i in range(n + 1):
        t = i / n
        x = CX - hw + 2 * hw * t
        k = math.sqrt(max(0.0, 1 - (2 * t - 1) ** 2))
        up.append((S(x), S(CY - hu * k)))
        lo.append((S(x), S(CY + hl * k)))
    pts = up + lo[::-1]
    m = Image.new("L", (CW, CH), 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    shade(img, m, c(SCLERA))
    d = ImageDraw.Draw(img)
    d.line(pts + [pts[0]], fill=c(DARK, 150), width=S(28), joint="curve")
    d.line(pts + [pts[0]], fill=c(CREAML), width=S(20), joint="curve")
    ir = 38.0
    d.ellipse([S(CX - ir), S(CY - ir), S(CX + ir), S(CY + ir)], fill=c(DARK))
    d.ellipse([S(CX - ir), S(CY - ir), S(CX + ir), S(CY + ir)], outline=c(GOLD), width=max(1, S(5)))
    gl = 12.0
    d.ellipse([S(CX - gl - 13), S(CY - gl - 13), S(CX + gl - 13), S(CY + gl - 13)], fill=c(CREAML))


FILES = {"DrawCircle.png": deck_motif, "EyeCircle.png": eye_motif}


def main():
    for d in (OUT, ARCHIVE):
        os.makedirs(d, exist_ok=True)
    for name, motif in FILES.items():
        img = base_disc()
        motif(img)
        out = img.resize((W, H), Image.LANCZOS)
        for d in (OUT, ARCHIVE):
            out.save(os.path.join(d, name))
        print("  %-16s %s" % (name, os.path.join(OUT, name)))
    print("done", len(FILES))


if __name__ == "__main__":
    main()