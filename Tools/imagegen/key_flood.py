"""Chroma-key a flat magenta background, flood-fill based.

Only magenta CONNECTED TO THE IMAGE BORDER counts as background, so interior
magenta-ish pixels (glow rays, purple rim light, energy wisps) are preserved
instead of being knocked out by a global colour-distance test.

usage: key_flood.py <src> <dst> <T_fill> <T0> <T1>
  T_fill  pixels closer than this to the key seed the flood (blocking barrier)
  T0/T1   smoothstep band: d<=T0 -> transparent, d>=T1 -> opaque
"""
import sys, time
import numpy as np
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
T_fill, T0, T1 = float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5])
t0 = time.time()

im = Image.open(src).convert("RGB")
a = np.asarray(im).astype(np.float32)
H, W, _ = a.shape
cs = 8
corners = np.concatenate([a[:cs, :cs].reshape(-1, 3), a[:cs, -cs:].reshape(-1, 3),
                          a[-cs:, :cs].reshape(-1, 3), a[-cs:, -cs:].reshape(-1, 3)])
key = corners.mean(axis=0)
d = np.sqrt(((a - key) ** 2).sum(axis=2))
print(f"key={key.round(1)}  size={W}x{H}")

bg_like = d < T_fill
print(f"bg_like={100.0*bg_like.mean():.1f}%   t={time.time()-t0:.1f}s")


def flood_from_border(bl):
    """Scanline flood fill over bl, seeded from every border pixel that is True."""
    H, W = bl.shape
    reach = np.zeros((H, W), dtype=bool)
    stack = []
    for x in np.nonzero(bl[0])[0]:
        stack.append((0, int(x)))
    for x in np.nonzero(bl[H - 1])[0]:
        stack.append((H - 1, int(x)))
    for y in np.nonzero(bl[:, 0])[0]:
        stack.append((int(y), 0))
    for y in np.nonzero(bl[:, W - 1])[0]:
        stack.append((int(y), W - 1))
    while stack:
        y, x = stack.pop()
        if reach[y, x] or not bl[y, x]:
            continue
        x0 = x
        while x0 > 0 and bl[y, x0 - 1] and not reach[y, x0 - 1]:
            x0 -= 1
        x1 = x
        while x1 < W - 1 and bl[y, x1 + 1] and not reach[y, x1 + 1]:
            x1 += 1
        reach[y, x0:x1 + 1] = True
        for ny in (y - 1, y + 1):
            if 0 <= ny < H:
                seg = bl[ny, x0:x1 + 1] & ~reach[ny, x0:x1 + 1]
                for i in np.nonzero(seg)[0]:
                    stack.append((ny, x0 + int(i)))
    return reach


reach = flood_from_border(bg_like)
print(f"reachable bg={100.0*reach.mean():.1f}%   t={time.time()-t0:.1f}s")


def shift_and(mask, r):
    """True where mask is True in the whole (2r+1) square neighbourhood."""
    out = mask.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dy == 0 and dx == 0:
                continue
            sh = np.zeros_like(mask)
            ys0, ys1 = max(dy, 0), mask.shape[0] + min(dy, 0)
            xs0, xs1 = max(dx, 0), mask.shape[1] + min(dx, 0)
            sh[ys0:ys1, xs0:xs1] = mask[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
            out &= sh
    return out


alpha = np.clip((d - T0) / (T1 - T0), 0.0, 1.0)
deep_bg = shift_and(reach, 2)
alpha[deep_bg] = 0.0

# interior pixels that are magenta-ish but NOT reachable from the border are
# subject (glow), never background -- force them fully opaque
protected = (~reach) & (d > T0) & shift_and(~reach, 1)
n_prot = int((protected & (alpha < 1.0)).sum())
alpha[protected] = 1.0
print(f"protected interior glow pixels: {n_prot}")

rgba = np.dstack([np.asarray(im), (alpha * 255.0 + 0.5).astype(np.uint8)])
Image.fromarray(rgba, "RGBA").save(dst)
print(f"saved {dst}   total t={time.time()-t0:.1f}s")
