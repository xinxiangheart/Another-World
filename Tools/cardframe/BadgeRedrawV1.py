# -*- coding: utf-8 -*-
"""重出 UI/Health.png、UI/Attack.png：数字落在数值盘上，图案缩到右上角、左下 1/4 被盘压住（方案③）。

预制体里徽章 rect 由 15u 放大到 19u（源画布 511px 不动 → 1u = 26.9px），可见金环直径 12.2u -> 15.5u。
图层：金环+暗盘 -> 图案(缩小/右上/裁进暗盘) -> 数值盘 -> (运行时数字)
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
UI = os.path.join(ROOT, "Assets/_Game/Resources/UI")
OUTDIR = os.path.join(ROOT, "Tools/cardframe/preview")

# ── 版式（卡单位，1u = K 源像素）──
CARD_BADGE = 19.0                 # 徽章 rect 边长（15 -> 19）
K = 511.0 / CARD_BADGE            # 源像素/卡单位
PLATE = (30, 42, 60)
PLATE_RIM = (62, 82, 108)
PLATE_W, PLATE_H = 11.4, 10.4     # 数值盘尺寸
PLATE_C = (-2.5, -2.3)            # 数值盘中心（相对徽章中心）
MARK_SQ = 9.5                     # 图案方形边长
MARK_C = (-2.5 + 11.4 / 2, -2.3 + 10.4 / 2)   # 图案中心 = 数值盘右上角
CLIP_R = 0.90                     # 图案裁切半径（× 源内容半径 R）


def load(name):
    im = Image.open(os.path.join(UI, f"{name}.png")).convert("RGBA")
    a = np.asarray(im).astype(np.int64)
    al = a[:, :, 3]
    ys, xs = np.nonzero(al > 40)
    cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
    R = (xs.max() - xs.min() + 1) / 2
    disc = a[int(cy), int(round(cx - 0.70 * R))][:3].copy()
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    m = (r < 0.80 * R) & (np.abs(a[:, :, :3] - disc).sum(axis=2) > 60) & (al > 50)
    base = a[:, :, :3].copy(); base[m] = disc
    base = np.dstack([base, al]).astype(np.uint8)
    mark = np.zeros_like(a); mark[:, :, :3] = a[:, :, :3]; mark[:, :, 3] = np.where(m, al, 0)
    return (Image.fromarray(base), Image.fromarray(mark.astype(np.uint8)),
            dict(cx=cx, cy=cy, R=R, disc=tuple(int(v) for v in disc)))


def build(name):
    base, mark, g = load(name)
    cx, cy, R = g["cx"], g["cy"], g["R"]
    W = base.size[0]
    out = base.copy()

    # 1) 图案：缩到右上角，裁进暗盘
    n = int(round(MARK_SQ * K))
    mk = mark.resize((n, n), Image.LANCZOS)
    layer = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    mx = int(round(cx + MARK_C[0] * K - n / 2))
    my = int(round(cy - MARK_C[1] * K - n / 2))
    layer.alpha_composite(mk, (mx, my))
    mask = Image.new("L", (W, W), 0)
    rr = CLIP_R * R
    ImageDraw.Draw(mask).ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=255)
    layer.putalpha(Image.fromarray(np.minimum(np.asarray(layer.split()[3]),
                                              np.asarray(mask)).astype(np.uint8)))
    out.alpha_composite(layer)

    # 2) 数值盘（含柔和投影）
    pw, ph = PLATE_W * K, PLATE_H * K
    pcx, pcy = cx + PLATE_C[0] * K, cy - PLATE_C[1] * K
    box = [pcx - pw / 2, pcy - ph / 2, pcx + pw / 2, pcy + ph / 2]
    rad = ph * 0.30
    sh = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle([box[0] + 2, box[1] + 5, box[2] + 2, box[3] + 5],
                                         radius=rad, fill=(0, 0, 0, 110))
    out.alpha_composite(sh.filter(ImageFilter.GaussianBlur(5)))
    pl = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(pl).rounded_rectangle(box, radius=rad, fill=PLATE + (255,),
                                         outline=PLATE_RIM + (255,), width=4)
    out.alpha_composite(pl)
    return out, g


def main():
    for name in ("Health", "Attack"):
        img, g = build(name)
        dst = os.path.join(UI, f"{name}.png")
        img.save(dst)
        # 预览（放大 1:1 徽章 + 卡片尺寸下的样子），拼到对比图
        print("saved", dst, img.size)


main()
