# -*- coding: utf-8 -*-
"""01331 囚牢「雾牢」动态预览 v1。

- 雾：A/B 两层左右弥漫，反向、不同周期；整体限制在槽位软边内。
- 栅：底端固定，顶端随风摆；四根相位错开，带慢速阵风包络。
- 8 秒无缝循环，12fps，输出 GIF + 动画 WebP。
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import SlotPrisonFXv1 as fx  # noqa: E402

OUT = HERE
W, H = 640, 940
SLOT_W, SLOT_H = 384, 683
SLOT_X, SLOT_Y = (W - SLOT_W) // 2, 138
FPS = 10
SECONDS = 8
FRAMES = FPS * SECONDS


def load(name):
    return Image.open(OUT / name).convert("RGBA")


def bend_rows(img, offset_at_top, gust, wobble=0.0, phase=0.0):
    """按行水平位移：底端 0，越往上越大；返回新的 RGBA。"""
    arr = np.asarray(img, np.float32)
    h, w = arr.shape[:2]
    out = np.zeros_like(arr)
    for y in range(h):
        t = 1.0 - y / (h - 1)
        off = offset_at_top * (t ** 1.65) * (0.75 + 0.25 * gust)
        off += wobble * (t ** 2) * np.sin(phase + (1.0 - t) * 3.4)
        off = int(round(off))
        if off > 0:
            out[y, off:] = arr[y, : w - off]
        elif off < 0:
            out[y, : w + off] = arr[y, -off:]
        else:
            out[y] = arr[y]
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")


def build_static(assets):
    bg = fx.load_rgba(fx.BOARD).resize((W, int(W * 1080 / 1920)), Image.LANCZOS)
    bg = Image.eval(bg, lambda v: int(v * 0.50))
    canvas = Image.new("RGBA", (W, H), (7, 9, 14, 255))
    canvas.alpha_composite(bg, (0, 0))
    tint = Image.new("RGBA", (W, H), (18, 9, 32, 48))
    canvas.alpha_composite(tint)

    # 槽位底板
    slot = Image.new("RGBA", (SLOT_W, SLOT_H), (0, 0, 0, 0))
    plate = assets["plate"].resize((SLOT_W, SLOT_H), Image.LANCZOS)
    edge = assets["edge"].resize((SLOT_W, SLOT_H), Image.LANCZOS)
    slot.alpha_composite(fx.tint_mask(plate, (11, 16, 25), 0.96))
    slot.alpha_composite(fx.tint_mask(plate, (58, 34, 86), 0.34))
    slot.alpha_composite(fx.tint_mask(edge, (107, 88, 54), 0.62))
    canvas.alpha_composite(slot, (SLOT_X, SLOT_Y))

    d = ImageDraw.Draw(canvas)
    d.text((32, 34), "01331 囚牢 · 雾牢 v1 · 动态", font=fx.font(30, True), fill=(238, 228, 255))
    d.text((34, 82), "雾：左右弥漫 / 反向双周期", font=fx.font(19), fill=(176, 158, 202))
    d.text((34, 110), "栅：底端固定 / 顶端随风摆 / 四根相位错开", font=fx.font(19), fill=(176, 158, 202))
    d.text((SLOT_X + SLOT_W / 2, SLOT_Y + SLOT_H + 18), "己方囚牢 · 中央涡眼可入渊", font=fx.font(20, True),
           fill=(206, 188, 236), anchor="ma")
    d.text((34, 872), "8s 无缝循环 · 10fps · 预览非最终速度", font=fx.font(16), fill=(118, 110, 138))
    return canvas


def main():
    assets = {
        "plate": load("SlotPrison_FogBed_01331.png") if False else fx.load_rgba(fx.SLOT_PLATE),
        "edge": fx.load_rgba(fx.SLOT_EDGE),
        "fogbed": load("SlotPrison_FogBed_01331.png").resize((SLOT_W, SLOT_H), Image.LANCZOS),
        "mista": load("SlotPrison_MistA_01331.png"),
        "mistb": load("SlotPrison_MistB_01331.png"),
        "bar": load("SlotPrison_Bar_01331.png"),
        "sigil": load("SlotPrison_Sigil_01331.png"),
        "mote": load("SlotPrison_Mote_01331.png"),
        "vortex": load("SlotPrison_Vortex_01331.png"),
    }
    static = build_static(assets)
    mask = fx.slot_soft_mask((SLOT_W, SLOT_H), blur=7)

    # 雾层：比槽位大，允许漂移；最后统一乘软边遮罩
    mist_size = 620
    mist_a = assets["mista"].resize((mist_size, mist_size), Image.LANCZOS)
    mist_b = assets["mistb"].resize((mist_size, mist_size), Image.LANCZOS)

    # 栅：先加宽画布，顶端摆动时不会被自身边界裁掉
    bar_specs = [(0.17, 0.74, 0.0), (0.38, 0.84, 0.9), (0.62, 0.80, 1.8), (0.83, 0.70, 2.7)]
    bars = []
    for fxpos, fh, phase in bar_specs:
        bh = int(SLOT_H * fh)
        bw = int(SLOT_W * 0.115)
        pad = Image.new("RGBA", (bw * 3, bh), (0, 0, 0, 0))
        pad.alpha_composite(assets["bar"].resize((bw, bh), Image.LANCZOS), (bw, 0))
        bars.append((pad, int(SLOT_W * fxpos), int(SLOT_H * 0.94) - bh, phase))

    sigil = assets["sigil"].resize((int(SLOT_W * 0.36), int(SLOT_W * 0.36)), Image.LANCZOS)
    vortex = assets["vortex"].resize((int(SLOT_W * 0.24), int(SLOT_W * 0.24)), Image.LANCZOS)
    mote = assets["mote"]

    rng = np.random.default_rng(5)
    motes = []
    for _ in range(10):
        motes.append((rng.uniform(0.16, 0.84), rng.uniform(0.0, 1.0), rng.uniform(0.6, 1.2),
                      rng.uniform(0.018, 0.032), rng.uniform(0.0, 6.28)))

    frames = []
    for f in range(FRAMES):
        t = f / FRAMES
        a = 2 * np.pi * t
        frame = static.copy()

        # 雾：A 8s，B 4s（反向）；轻微旋转 + 呼吸
        mist_layer = Image.new("RGBA", (SLOT_W, SLOT_H), (0, 0, 0, 0))
        ax = np.sin(a) * SLOT_W * 0.100
        ay = np.sin(a + 0.9) * SLOT_H * 0.012
        m1 = mist_a.rotate(-7 + np.sin(a + 0.4) * 2.2, resample=Image.BICUBIC, expand=False)
        fx.paste(mist_layer, m1, center=(SLOT_W * 0.50 + ax, SLOT_H * 0.44 + ay),
                 alpha=0.80 * (0.92 + 0.08 * np.sin(a * 2 + 0.3)))
        bx = np.sin(a * 2 + 1.3) * SLOT_W * 0.078
        by = np.cos(a * 2 + 0.4) * SLOT_H * 0.015
        m2 = mist_b.rotate(12 + np.sin(a * 2 + 1.1) * 2.4, resample=Image.BICUBIC, expand=False)
        fx.paste(mist_layer, m2, center=(SLOT_W * 0.54 + bx, SLOT_H * 0.62 + by),
                 alpha=0.66 * (0.90 + 0.10 * np.sin(a * 2 + 1.7)))
        ma = np.asarray(mist_layer, np.float32).copy()
        ma[..., 3] *= mask
        frame.alpha_composite(Image.fromarray(np.clip(ma, 0, 255).astype(np.uint8), "RGBA"), (SLOT_X, SLOT_Y))

        # 底雾呼吸
        fog = assets["fogbed"].copy()
        fog = fx.add_alpha(fog, 0.90 + 0.06 * np.sin(a * 2))
        frame.alpha_composite(fog, (SLOT_X, SLOT_Y))

        # 栅：底端固定、顶端摆；共享阵风包络
        gust = 0.80 + 0.20 * np.sin(a + 0.6)
        for pad, x, y, phase in bars:
            top = np.sin(a * 2 + phase) * SLOT_W * 0.055
            top += np.sin(a + phase * 0.6) * SLOT_W * 0.018
            wobble = SLOT_W * 0.008 * np.sin(a * 3 + phase * 1.4)
            bimg = bend_rows(pad, top, gust, wobble=wobble, phase=phase + a * 0.8)
            frame.alpha_composite(bimg, (SLOT_X + x - pad.width // 2, SLOT_Y + y))

        # 封印：呼吸 + 极慢摆
        sw = sigil.width * (1.0 + 0.008 * np.sin(a * 2 + 0.5))
        s = sigil.resize((int(sw), int(sw)), Image.LANCZOS)
        s = s.rotate(np.sin(a + 0.2) * 1.5, resample=Image.BICUBIC, expand=True)
        sa = fx.add_alpha(s, 0.92 + 0.05 * np.sin(a * 2 + 0.4))
        frame.alpha_composite(sa, (SLOT_X + int(SLOT_W * 0.5 - s.width / 2), SLOT_Y + int(SLOT_H * 0.5 - s.height / 2)))

        # 涡眼：连续旋转
        v = vortex.rotate(-t * 360.0, resample=Image.BICUBIC, expand=True)
        va = fx.add_alpha(v, 0.55)
        frame.alpha_composite(va, (SLOT_X + int(SLOT_W * 0.5 - v.width / 2), SLOT_Y + int(SLOT_H * 0.5 - v.height / 2)))

        # 微光尘：每个粒子 3s 循环，向上飘
        for i, (fxp, off, speed, size, ph) in enumerate(motes):
            prog = (t * (8.0 / 3.0) + off) % 1.0
            px = SLOT_X + (fxp + 0.02 * np.sin(a + ph)) * SLOT_W
            py = SLOT_Y + (0.90 - 0.58 * prog) * SLOT_H
            al = np.sin(np.pi * prog) ** 1.2 * 0.55
            s = mote.resize((max(2, int(size * SLOT_W)),) * 2, Image.LANCZOS)
            frame.alpha_composite(fx.add_alpha(s, al), (int(px), int(py)))

        frames.append(frame.convert("RGB"))

    gif_path = OUT / "preview_01331_motion_v1.gif"
    palette = frames[0].quantize(colors=256, method=Image.MEDIANCUT)
    pf = [im.quantize(palette=palette, dither=Image.FLOYDSTEINBERG) for im in frames]
    pf[0].save(gif_path, save_all=True, append_images=pf[1:], duration=int(1000 / FPS), loop=0, optimize=True)

    small_path = OUT / "preview_01331_motion_v1_small.gif"
    sw, sh = 400, 588
    small_rgb = [im.resize((sw, sh), Image.LANCZOS) for im in frames]
    small_pal = small_rgb[0].quantize(colors=96, method=Image.MEDIANCUT)
    small_pf = [im.quantize(palette=small_pal, dither=Image.FLOYDSTEINBERG) for im in small_rgb]
    small_pf[0].save(small_path, save_all=True, append_images=small_pf[1:], duration=int(1000 / FPS), loop=0, optimize=True)

    webp_path = OUT / "preview_01331_motion_v1.webp"
    frames[0].save(webp_path, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0,
                   quality=88, method=6)
    print(gif_path)
    print(small_path)
    print(webp_path)


if __name__ == "__main__":
    main()
