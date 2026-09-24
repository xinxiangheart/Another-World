# -*- coding: utf-8 -*-
"""择牌「选定框」素材：Resources/UI/PickFrame_{0..5}.png（9 宫格，border 32）。

**不是一套金框**——颜色跟被选中的卡的费用走，取各费用边带 Frame_Edge_{cost} 的实测主体色，
所以选定框读起来就是那张卡自己的框色往外扩一圈。

材质语言与 Cards/Frame/Frame_Edge_* 同剖面（实测）：
  外柔边 → 外亮线 2 → 主体 4 → 中线 1 → 暗线 1 → 凹槽 1（共 10px = 768 宽边带 25px 的同比）

框体 = 边带 + 圈内一道细双线（1px 暗 + 1.6px 亮，内缩 15px），
四角再加两样细活：连边带与细线的锥形角撑、角撑内侧一颗小角珠。
角部装饰只活在 32px 角区内（以「裁下角块 → 画 → 转回四角」保证与边带像素级对齐），
边条沿长度方向无细节，可安全九宫格拉伸。
"""
import os
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
OUTDIR = os.path.join(ROOT, "Assets/_Game/Resources/UI")
S = 4                      # 超采样
W, H = 96, 144             # 源尺寸
BORDER = 32
TILE = 32                  # 角区（源图像素）
RADIUS = 14                # 外圆角（源图像素）

# 各费用主体色 = Frame_Edge_{cost} 实测中位色
BASE = [
    (146, 154, 164),   # 0 费 灰
    (226, 223, 212),   # 1 费 象牙白
    (86, 176, 104),    # 2 费 绿
    (86, 140, 214),    # 3 费 蓝
    (152, 108, 208),   # 4 费 紫
    (226, 186, 86),    # 5 费 金
]
GROOVE = (6, 8, 14)


def clip(c):
    return tuple(max(0, min(255, int(round(v)))) for v in c)


def mul(c, k, a=255):
    return clip((c[0] * k, c[1] * k, c[2] * k)) + (a,)


def rr(d, i0, i1, rad, fill, size):
    """一圈矩形环：inset i0 填满，再来一圈 inset i1 清空。"""
    w, h = size
    d.rounded_rectangle([i0, i0, w - 1 - i0, h - 1 - i0], radius=rad, fill=fill)
    if i1 is not None:
        d.rounded_rectangle([i1, i1, w - 1 - i1, h - 1 - i1], radius=max(0.0, rad - (i1 - i0)), fill=(0, 0, 0, 0))


def paint(B):
    """整幅：边带 + 圈内细双线。两样都是「沿边长度方向无细节」的，可安全拉伸。"""
    w, h = W * S, H * S
    k = lambda v: v * S
    R = RADIUS * S
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    # 边带：外柔边 0.5 / 外亮线 2 / 主体 4 / 中线 1 / 暗线 1 / 凹槽 1
    rr(d, 0, k(0.4), R, mul(B, 1.0, 105), (w, h))
    rr(d, k(0.4), k(2.5), R - k(0.4), mul(B, 1.02), (w, h))
    rr(d, k(2.5), k(6.5), R - k(2.5), mul(B, 1.0), (w, h))
    rr(d, k(6.5), k(7.5), R - k(6.5), mul(B, 1.07), (w, h))
    rr(d, k(7.5), k(8.5), R - k(7.5), mul(B, 0.72), (w, h))
    rr(d, k(8.5), k(9.5), R - k(8.5), GROOVE + (205,), (w, h))

    # 圈内细双线：1px 暗 + 1.6px 亮（内缩 15）
    rr(d, k(15.0), k(16.0), max(0.0, R - k(15.0)), GROOVE + (185,), (w, h))
    rr(d, k(16.0), k(17.7), max(0.0, R - k(16.0)), mul(B, 1.02), (w, h))
    return im


def corner(tile, B):
    """在（已含边带与细线的）左上角块上画角撑与角珠。"""
    d = ImageDraw.Draw(tile)
    k = lambda v: v * S
    RIM = mul(B, 1.02)
    HI = mul(B, 1.07)
    GR = GROOVE + (195,)
    u = 0.70710678

    # 锥形角撑：从带面内侧斜伸到细线（45°）
    p1, p2, wa, wb = 10.2, 17.0, 1.55, 1.05
    poly = [(p1 - wa * u, p1 + wa * u), (p2 - wb * u, p2 + wb * u),
            (p2 + wb * u, p2 - wb * u), (p1 + wa * u, p1 - wa * u)]
    d.polygon([(k(x), k(y)) for x, y in poly], fill=RIM, outline=GR, width=max(1, int(0.8 * S)))

    # 角珠：细线内侧的菱形小珠
    c, r = 20.6, 2.6
    dia = [(c, c - r), (c + r, c), (c, c + r), (c - r, c)]
    d.polygon([(k(x), k(y)) for x, y in dia], fill=GROOVE + (205,))
    r2 = 1.7
    dia2 = [(c, c - r2), (c + r2, c), (c, c + r2), (c - r2, c)]
    d.polygon([(k(x), k(y)) for x, y in dia2], fill=HI)
    return tile


def build(B):
    im = paint(B)
    full = im
    t = TILE * S
    base_tile = full.crop((0, 0, t, t))
    tile = corner(base_tile, B)
    for ang in (0, 90, 180, 270):
        piece = tile if ang == 0 else tile.rotate(-ang, resample=Image.NEAREST)
        if ang == 0:
            full.alpha_composite(piece, (0, 0))
        elif ang == 90:
            full.alpha_composite(piece, (W * S - t, 0))
        elif ang == 180:
            full.alpha_composite(piece, (W * S - t, H * S - t))
        else:
            full.alpha_composite(piece, (0, H * S - t))

    out = im.resize((W, H), Image.LANCZOS)

    # 外圈一点同色辉光，把框从压暗背景上托起来（很轻，不抢卡面）
    a = np.asarray(out).astype(np.float32)
    g = np.zeros_like(a)
    g[:, :, 0], g[:, :, 1], g[:, :, 2] = B
    g[:, :, 3] = np.clip(a[:, :, 3] * 0.36, 0, 255)
    glow = Image.fromarray(g.astype(np.uint8)).filter(ImageFilter.GaussianBlur(2.0))

    res = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    res.alpha_composite(glow)
    res.alpha_composite(out)
    return res


META_TMPL = open(os.path.join(ROOT, "Tools/cardframe/_meta_tmpl.txt"), "r", encoding="utf-8").read()


def main():
    for i, B in enumerate(BASE):
        dst = os.path.join(OUTDIR, "PickFrame_%d.png" % i)
        build(B).save(dst)
        meta = dst + ".meta"
        if not os.path.exists(meta):
            guid = "".join(random.choice("0123456789abcdef") for _ in range(32))
            with open(meta, "w", encoding="utf-8", newline="\n") as f:
                f.write(META_TMPL.replace("__GUID__", guid))
        print("saved", os.path.basename(dst))


main()
