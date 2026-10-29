# -*- coding: utf-8 -*-
"""01331 囚牢「雾牢」v1 —— 组件图 + 静态叠图预览。

输出与本脚本同目录（先审图，不直接进 Resources）：
  SlotPrison_FogBed_01331.png   540x960
  SlotPrison_MistA_01331.png    640x640
  SlotPrison_MistB_01331.png    640x640
  SlotPrison_Bar_01331.png      128x720
  SlotPrison_Sigil_01331.png    384x384
  SlotPrison_Mote_01331.png     64x64
  SlotPrison_Vortex_01331.png   192x192
  preview_01331_slots_v1.png
  preview_01331_timeline_v1.png

雾全部是程序化 fBm 噪声，便于控制 alpha 边缘与走向；符文/雾栅是矢量式绘制，
不依赖 imagegen 的透明边。
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(r"C:\Users\22589\Documents\GitHub\Another-World")
OUT = Path(__file__).resolve().parent
SLOT_PLATE = ROOT / "Assets/_Game/Art/Sprites/Board/SlotPlate.png"
SLOT_EDGE = ROOT / "Assets/_Game/Art/Sprites/Board/SlotEdge.png"
BOARD = ROOT / "Assets/_Game/Art/Sprites/Board/Board.png"
FONT = r"C:\Windows\Fonts\msyh.ttc"
FONT_BD = r"C:\Windows\Fonts\msyhbd.ttc"

DEEP = np.array([36, 21, 54], np.float32)      # #241536
MID = np.array([80, 51, 116], np.float32)      # #503374
LIGHT = np.array([154, 121, 208], np.float32)  # #9A79D0
CORE = np.array([233, 222, 255], np.float32)   # #E9DEFF


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def value_noise(h, w, sx, sy, rng):
    gh = max(2, int(round(h / max(1.0, sy))))
    gw = max(2, int(round(w / max(1.0, sx))))
    grid = rng.random((gh, gw)).astype(np.float32)
    img = Image.fromarray((grid * 255).astype(np.uint8), "L").resize((w, h), Image.BICUBIC)
    return np.asarray(img, np.float32) / 255.0


def fbm(h, w, sx, sy, octaves, seed, persistence=0.56):
    rng = np.random.default_rng(seed)
    total = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    for i in range(octaves):
        total += amp * value_noise(h, w, max(2.0, sx / (2.0 ** i)), max(2.0, sy / (2.0 ** i)), rng)
        norm += amp
        amp *= persistence
    n = total / norm
    lo, hi = np.percentile(n, 2), np.percentile(n, 98)
    return np.clip((n - lo) / max(1e-5, hi - lo), 0.0, 1.0)


def save_rgba(arr, name):
    arr = np.clip(arr, 0, 255).astype(np.uint8)
    Image.fromarray(arr, "RGBA").save(OUT / name)
    return OUT / name


def make_fog_bed(w=540, h=960, seed=7):
    yy, xx = np.mgrid[0:h, 0:w]
    cx, cy = (w - 1) / 2, (h - 1) / 2
    nx = (xx - cx) / (w / 2)
    ny = (yy - cy) / (h / 2)
    r = np.sqrt(nx * nx + ny * ny)

    dm = np.minimum.reduce([xx, w - 1 - xx, yy, h - 1 - yy]).astype(np.float32)
    edge = np.exp(-dm / 98.0)
    border = smoothstep(0, 13, dm)
    n1 = fbm(h, w, 190, 190, 5, seed)
    n2 = fbm(h, w, 58, 58, 4, seed + 1)
    n = 0.62 * n1 + 0.38 * n2

    alpha = (0.40 * edge * (0.60 + 0.55 * n) + 0.13 * smoothstep(1.15, 0.15, r) * n2)
    alpha += 0.075 * smoothstep(-0.2, 1.0, ny) * n2
    alpha *= border * smoothstep(1.02, 0.55, r)
    alpha = np.clip(alpha, 0, 0.50)

    dens = smoothstep(0.30, 0.82, n)
    rgb = lerp(DEEP[None, None, :], MID[None, None, :], (dens * 0.85)[..., None])
    rgb = lerp(rgb, LIGHT[None, None, :], (smoothstep(0.70, 0.95, n) * 0.22)[..., None])
    out = np.zeros((h, w, 4), np.float32)
    out[..., :3] = rgb
    out[..., 3] = alpha * 255
    return out


def make_mist(size=640, seed=11, streaky=True, alpha_max=0.78):
    yy, xx = np.mgrid[0:size, 0:size]
    cx, cy = (size - 1) / 2, (size - 1) / 2
    nx = (xx - cx) / (size / 2)
    ny = (yy - cy) / (size / 2)
    r = np.sqrt(nx * nx + ny * ny)

    if streaky:
        n1 = fbm(size, size, 330, 78, 5, seed)
        n2 = fbm(size, size, 120, 44, 4, seed + 3)
        warp = fbm(size, size, 240, 240, 3, seed + 7)
        n = 0.55 * n1 + 0.45 * n2
        n = n * (0.78 + 0.22 * warp)
    else:
        n1 = fbm(size, size, 155, 190, 5, seed)
        n2 = fbm(size, size, 62, 78, 4, seed + 3)
        n = 0.65 * n1 + 0.35 * n2

    falloff = np.clip((1.04 - r) / 0.52, 0, 1) ** 1.55
    a = smoothstep(0.33, 0.70, n) * falloff
    a = np.asarray(Image.fromarray((a * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2.2)), np.float32) / 255.0
    a = np.clip(a * alpha_max, 0, 1)

    dens = smoothstep(0.40, 0.86, n)
    rgb = lerp(DEEP[None, None, :], MID[None, None, :], (0.35 + 0.65 * dens)[..., None])
    rgb = lerp(rgb, LIGHT[None, None, :], (smoothstep(0.66, 0.93, n) * 0.55)[..., None])
    out = np.zeros((size, size, 4), np.float32)
    out[..., :3] = rgb
    out[..., 3] = a * 255
    return out


def make_bar(w=128, h=720, seed=21):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    v = 1.0 - yy / (h - 1)
    centers = w * 0.5 + np.sin(v * np.pi * 3.4 + seed) * w * 0.035
    dx = xx - centers
    core = np.exp(-(dx / (w * 0.075)) ** 2)
    glow = np.exp(-(dx / (w * 0.36)) ** 2)

    vertical = smoothstep(0.0, 0.055, v) * smoothstep(1.02, 0.62, v)
    n = fbm(h, w, 26, 78, 4, seed)
    breakup = 0.55 + 0.55 * n
    a = (0.52 * core + 0.18 * glow) * vertical * breakup
    a *= smoothstep(0.0, 0.018, v)
    a = np.clip(a * 0.78, 0, 1)

    rgb = lerp(MID[None, None, :], LIGHT[None, None, :], (core * 0.92)[..., None])
    out = np.zeros((h, w, 4), np.float32)
    out[..., :3] = rgb
    out[..., 3] = a * 255
    return out


def make_mote(size=64):
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    c = (size - 1) / 2
    r = np.sqrt((xx - c) ** 2 + (yy - c) ** 2) / (size * 0.5)
    a = np.clip(1.0 - r, 0, 1) ** 2.6
    a = np.clip(a * 1.2, 0, 1)
    out = np.zeros((size, size, 4), np.float32)
    out[..., :3] = CORE
    out[..., 3] = a * 255
    return out


def draw_sigil(size=384):
    ss = 4
    W = size * ss
    c = W / 2
    line = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(line)
    R = 148 * ss

    for i in range(8):
        a0 = i * 45 + 5.5
        a1 = a0 + 34
        d.arc([c - R, c - R, c + R, c + R], a0, a1, fill=255, width=3 * ss)

    r2 = 112 * ss
    d.arc([c - r2, c - r2, c + r2, c + r2], 0, 360, fill=235, width=2 * ss)

    for ang in (0, 90, 180, 270):
        rad = np.deg2rad(ang)
        x0, y0 = c + np.cos(rad) * 120 * ss, c + np.sin(rad) * 120 * ss
        x1, y1 = c + np.cos(rad) * 140 * ss, c + np.sin(rad) * 140 * ss
        d.line([(x0, y0), (x1, y1)], fill=255, width=int(round(2.4 * ss)))

    dia = [(c, c - 58 * ss), (c + 58 * ss, c), (c, c + 58 * ss), (c - 58 * ss, c)]
    d.line(dia + [dia[0]], fill=255, width=int(round(2.6 * ss)), joint="curve")
    dia2 = [(c, c - 34 * ss), (c + 34 * ss, c), (c, c + 34 * ss), (c - 34 * ss, c)]
    d.line(dia2 + [dia2[0]], fill=205, width=int(round(1.5 * ss)), joint="curve")

    for ang in (45, 135, 225, 315):
        rad = np.deg2rad(ang)
        x, y = c + np.cos(rad) * 126 * ss, c + np.sin(rad) * 126 * ss
        d.ellipse([x - 3.2 * ss, y - 3.2 * ss, x + 3.2 * ss, y + 3.2 * ss], fill=255)

    core = line.resize((size, size), Image.LANCZOS)
    glow = line.filter(ImageFilter.GaussianBlur(5.2 * ss)).resize((size, size), Image.LANCZOS)

    ca = np.asarray(core, np.float32) / 255.0
    ga = np.asarray(glow, np.float32) / 255.0
    out = np.zeros((size, size, 4), np.float32)
    rgb = CORE[None, None, :] * (ca[..., None] ** 0.65) + np.array([120, 82, 180], np.float32)[None, None, :] * (1 - ca[..., None]) * 0.55
    out[..., :3] = rgb
    out[..., 3] = np.clip(ca * 0.97 * 255 + ga * 0.35 * 255, 0, 255)
    return out


def draw_vortex(size=192):
    ss = 4
    W = size * ss
    img = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(img)
    c = W / 2
    for arm in range(3):
        pts = []
        for k in range(160):
            t = k / 159.0
            theta = arm * (2 * np.pi / 3) + t * 2.35 * np.pi
            r = (82 - 70 * t) * ss
            pts.append((c + np.cos(theta) * r, c + np.sin(theta) * r))
        d.line(pts, fill=255, width=max(1, int(2.2 * ss)), joint="curve")
    for rr, a0, a1 in ((58, 10, 170), (34, 190, 330), (18, 20, 300)):
        d.arc([c - rr * ss, c - rr * ss, c + rr * ss, c + rr * ss], a0, a1, fill=210, width=int(round(1.6 * ss)))
    d.ellipse([c - 4 * ss, c - 4 * ss, c + 4 * ss, c + 4 * ss], fill=255)

    yy, xx = np.mgrid[0:W, 0:W].astype(np.float32)
    r = np.sqrt((xx - c) ** 2 + (yy - c) ** 2) / (W * 0.5)
    fade = np.clip((1.0 - r) / 0.55, 0, 1) ** 1.4
    line = np.asarray(img, np.float32) / 255.0 * fade
    line = np.asarray(Image.fromarray((line * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(0.6 * ss)), np.float32) / 255.0
    core = np.asarray(img.resize((size, size), Image.LANCZOS), np.float32) / 255.0
    glow = np.asarray(Image.fromarray((line * 255).astype(np.uint8), "L").resize((size, size), Image.LANCZOS), np.float32) / 255.0
    out = np.zeros((size, size, 4), np.float32)
    out[..., :3] = np.array([201, 167, 255], np.float32)[None, None, :]
    out[..., 3] = np.clip(core * 0.85 * 255 + glow * 0.45 * 255, 0, 255)
    return out


def load_rgba(path):
    return Image.open(path).convert("RGBA")


def tint_mask(mask_img, color, alpha_mul=1.0):
    a = np.asarray(mask_img.getchannel("A"), np.float32) / 255.0 * alpha_mul
    h, w = a.shape
    out = np.zeros((h, w, 4), np.uint8)
    out[..., 0], out[..., 1], out[..., 2] = color
    out[..., 3] = np.clip(a * 255, 0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def add_alpha(img, mul):
    a = np.asarray(img, np.float32).copy()
    a[..., 3] *= mul
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def paste(base, img, center=None, xy=None, scale=1.0, rot=0.0, alpha=1.0):
    if scale != 1.0:
        img = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))), Image.LANCZOS)
    if rot:
        img = img.rotate(rot, resample=Image.BICUBIC, expand=True)
    if alpha != 1.0:
        img = add_alpha(img, alpha)
    if center is not None:
        x = int(center[0] - img.width / 2)
        y = int(center[1] - img.height / 2)
    else:
        x, y = int(xy[0]), int(xy[1])
    base.alpha_composite(img, (x, y))


def slot_soft_mask(size, blur=8):
    w, h = size
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    inset = max(1, int(round(w * 0.012)))
    radius = max(2, int(round(w * 0.113)))
    d.rounded_rectangle([inset, inset, w - 1 - inset, h - 1 - inset], radius=radius, fill=255)
    m = m.filter(ImageFilter.GaussianBlur(blur))
    return np.asarray(m, np.float32) / 255.0


def build_slot(assets, size=(540, 960), ally=True, progress=1.0, show_vortex=True):
    w, h = size
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    plate = assets["plate"].resize(size, Image.LANCZOS)
    edge = assets["edge"].resize(size, Image.LANCZOS)
    canvas.alpha_composite(tint_mask(plate, (11, 16, 25), 0.96))
    canvas.alpha_composite(tint_mask(plate, (58, 34, 86), 0.34))
    canvas.alpha_composite(tint_mask(edge, (107, 88, 54), 0.62))

    p = float(np.clip(progress, 0, 1))
    fog_u = smoothstep(0.0, 0.22, p)
    mist_u = smoothstep(0.08, 0.56, p)
    bar_u = smoothstep(0.28, 0.78, p)
    sig_u = smoothstep(0.58, 0.94, p)

    if fog_u > 0:
        paste(canvas, assets["fogbed"].resize(size, Image.LANCZOS), xy=(0, 0), alpha=fog_u * 0.92)
    if mist_u > 0:
        mist = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        paste(mist, assets["mista"], center=(w * 0.50, h * 0.44), scale=w * 1.42 / assets["mista"].width,
              rot=-8, alpha=0.80 * mist_u)
        paste(mist, assets["mistb"], center=(w * 0.55, h * 0.62), scale=w * 1.22 / assets["mistb"].width,
              rot=14, alpha=0.66 * mist_u)
        ma = np.asarray(mist, np.float32).copy()
        ma[..., 3] *= slot_soft_mask(size, blur=max(3, int(round(w * 0.018))))
        canvas.alpha_composite(Image.fromarray(np.clip(ma, 0, 255).astype(np.uint8), "RGBA"))

    if bar_u > 0:
        xs = [0.17, 0.38, 0.62, 0.83]
        hs = [0.74, 0.84, 0.80, 0.70]
        rots = [-2.2, 1.0, -1.2, 2.0]
        for fx, fh, rr in zip(xs, hs, rots):
            bw = w * 0.115
            bh = h * fh
            img = assets["bar"].resize((int(bw), int(bh)), Image.LANCZOS)
            paste(canvas, img, center=(w * fx, h * 0.94 - bh / 2), rot=rr, alpha=bar_u * 0.68)

    if sig_u > 0:
        sw = w * 0.36
        paste(canvas, assets["sigil"], center=(w * 0.50, h * 0.50), scale=sw / assets["sigil"].width,
              alpha=sig_u * 0.96)
        if 0.58 < p < 0.99:
            flash = 1.0 - abs(p - 0.76) / 0.18
            if flash > 0:
                glow = Image.new("RGBA", (w, h), (170, 120, 255, 0))
                gd = ImageDraw.Draw(glow)
                rad = w * 0.30
                gd.ellipse([w * 0.5 - rad, h * 0.5 - rad, w * 0.5 + rad, h * 0.5 + rad],
                           fill=(170, 120, 255, int(26 * flash)))
                glow = glow.filter(ImageFilter.GaussianBlur(18))
                canvas.alpha_composite(glow)

    if ally and show_vortex:
        vw = w * 0.24
        paste(canvas, assets["vortex"], center=(w * 0.50, h * 0.50), scale=vw / assets["vortex"].width,
              alpha=0.58 * smoothstep(0.60, 0.92, p))

    rng = np.random.default_rng(31)
    for _ in range(9):
        px = rng.uniform(0.16, 0.84) * w
        py = rng.uniform(0.24, 0.88) * h
        s = rng.uniform(0.020, 0.040) * w
        a = rng.uniform(0.25, 0.55) * smoothstep(0.35, 0.85, p)
        paste(canvas, assets["mote"], center=(px, py), scale=s / assets["mote"].width, alpha=a)

    return canvas


def font(size, bold=False):
    return ImageFont.truetype(FONT_BD if bold else FONT, size)


def label(draw, xy, text, size=28, fill=(224, 214, 238), bold=False, anchor="la"):
    draw.text(xy, text, font=font(size, bold), fill=fill, anchor=anchor)


def make_preview(assets):
    W, H = 1920, 1200
    bg = load_rgba(BOARD).resize((W, 1080), Image.LANCZOS)
    bg = Image.eval(bg, lambda v: int(v * 0.52))
    tint = Image.new("RGBA", (W, 1080), (18, 9, 32, 56))
    bg.alpha_composite(tint)
    canvas = Image.new("RGBA", (W, H), (7, 9, 14, 255))
    canvas.alpha_composite(bg, (0, 0))

    draw = ImageDraw.Draw(canvas)
    label(draw, (64, 46), "01331 囚牢 · 雾牢 v1", 46, (238, 228, 255), True)
    label(draw, (66, 108), "紫色雾为主体 / 雾栅为骨架 / 中央封印为锁点", 24, (170, 154, 196))
    label(draw, (66, 144), "静态叠图：左＝可放渊的己方格，右＝封死的敌方格", 22, (132, 120, 158))

    slot_w, slot_h = 432, 768
    y = 220
    left = build_slot(assets, (slot_w, slot_h), ally=True)
    right = build_slot(assets, (slot_w, slot_h), ally=False)
    canvas.alpha_composite(left, (300, y))
    canvas.alpha_composite(right, (1188, y))

    label(draw, (300 + slot_w / 2, y + slot_h + 22), "己方囚牢 · 中央涡眼可入渊", 25, (206, 188, 236), True, "ma")
    label(draw, (1188 + slot_w / 2, y + slot_h + 22), "敌方囚牢 · 封死", 25, (186, 172, 206), True, "ma")

    items = [
        ("FogBed", assets["fogbed"], "540×960 · 底雾"),
        ("MistA", assets["mista"], "640×640 · 慢雾"),
        ("MistB", assets["mistb"], "640×640 · 快雾"),
        ("Bar", assets["bar"], "128×720 · 雾栅"),
        ("Sigil", assets["sigil"], "384×384 · 封印"),
        ("Vortex", assets["vortex"], "192×192 · 涡眼"),
    ]
    x0, y0 = 64, 1090
    cw, ch = 292, 96
    for i, (name, img, sub) in enumerate(items):
        x = x0 + i * (cw + 14)
        card = Image.new("RGBA", (cw, ch), (27, 33, 47, 235))
        d2 = ImageDraw.Draw(card)
        d2.rounded_rectangle([0, 0, cw - 1, ch - 1], radius=9, outline=(58, 72, 94, 255), width=1)
        d2.rounded_rectangle([10, 12, 86, 84], radius=5, fill=(44, 52, 68, 255))
        thumb = img.copy()
        thumb.thumbnail((64, 64), Image.LANCZOS)
        card.alpha_composite(thumb, (16 + (64 - thumb.width) // 2, (ch - thumb.height) // 2))
        d2.text((94, 22), name, font=font(21, True), fill=(224, 214, 238))
        d2.text((94, 50), sub, font=font(17), fill=(126, 138, 158))
        canvas.alpha_composite(card, (x, y0))

    p = OUT / "preview_01331_slots_v1.png"
    canvas.convert("RGB").save(p, quality=95)
    return p


def make_timeline(assets):
    W, H = 1920, 760
    canvas = Image.new("RGBA", (W, H), (7, 9, 14, 255))
    bg = load_rgba(BOARD).resize((W, 430), Image.LANCZOS)
    bg = Image.eval(bg, lambda v: int(v * 0.34))
    canvas.alpha_composite(bg, (0, 0))
    draw = ImageDraw.Draw(canvas)
    label(draw, (64, 42), "进场四帧 · 0.62s", 40, (238, 228, 255), True)
    label(draw, (66, 98), "铺雾 → 聚雾 → 立栅 → 落印", 23, (170, 154, 196))

    frames = [(0.18, "0.12s  铺雾"), (0.46, "0.28s  聚雾"), (0.80, "0.46s  立栅"), (1.00, "0.62s  落印")]
    sw, sh = 300, 533
    gap = 58
    total = len(frames) * sw + (len(frames) - 1) * gap
    x = (W - total) // 2
    for p, cap in frames:
        slot = build_slot(assets, (sw, sh), ally=True, progress=p)
        canvas.alpha_composite(slot, (x, 170))
        draw.text((x + sw / 2, 170 + sh + 18), cap, font=font(22, True), fill=(210, 196, 232), anchor="ma")
        x += sw + gap

    p = OUT / "preview_01331_timeline_v1.png"
    canvas.convert("RGB").save(p, quality=95)
    return p


def main():
    assets = {}
    assets["fogbed"] = Image.fromarray(np.clip(make_fog_bed(), 0, 255).astype(np.uint8), "RGBA")
    assets["mista"] = Image.fromarray(np.clip(make_mist(640, 11, True, 0.78), 0, 255).astype(np.uint8), "RGBA")
    assets["mistb"] = Image.fromarray(np.clip(make_mist(640, 29, False, 0.68), 0, 255).astype(np.uint8), "RGBA")
    assets["bar"] = Image.fromarray(np.clip(make_bar(), 0, 255).astype(np.uint8), "RGBA")
    assets["sigil"] = Image.fromarray(np.clip(draw_sigil(), 0, 255).astype(np.uint8), "RGBA")
    assets["mote"] = Image.fromarray(np.clip(make_mote(), 0, 255).astype(np.uint8), "RGBA")
    assets["vortex"] = Image.fromarray(np.clip(draw_vortex(), 0, 255).astype(np.uint8), "RGBA")
    assets["plate"] = load_rgba(SLOT_PLATE)
    assets["edge"] = load_rgba(SLOT_EDGE)

    save_rgba(np.asarray(assets["fogbed"]), "SlotPrison_FogBed_01331.png")
    save_rgba(np.asarray(assets["mista"]), "SlotPrison_MistA_01331.png")
    save_rgba(np.asarray(assets["mistb"]), "SlotPrison_MistB_01331.png")
    save_rgba(np.asarray(assets["bar"]), "SlotPrison_Bar_01331.png")
    save_rgba(np.asarray(assets["sigil"]), "SlotPrison_Sigil_01331.png")
    save_rgba(np.asarray(assets["mote"]), "SlotPrison_Mote_01331.png")
    save_rgba(np.asarray(assets["vortex"]), "SlotPrison_Vortex_01331.png")

    p1 = make_preview(assets)
    p2 = make_timeline(assets)
    print(p1)
    print(p2)


if __name__ == "__main__":
    main()
