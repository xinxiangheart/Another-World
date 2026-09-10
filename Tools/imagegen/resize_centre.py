"""Resize to the project card standard, then centre the subject on both axes."""
import sys
import numpy as np
from PIL import Image

src, dst, tw, th = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
im = Image.open(src).convert("RGBA")
print("in:", im.size, end="  ")
im = im.resize((tw, th), Image.LANCZOS)
a = np.asarray(im)
H, W = a.shape[:2]
ys, xs = np.nonzero(a[..., 3] > 8)
l, r = int(xs.min()), int(W - 1 - xs.max())
t, b = int(ys.min()), int(H - 1 - ys.max())
print(f"pre-centre L={l} R={r} T={t} B={b}")
dx, dy = (l - r) // 2, (t - b) // 2
out = np.zeros_like(a)
if dx >= 0: ox0, ox1, ax0, ax1 = 0, W - dx, dx, W
else:       ox0, ox1, ax0, ax1 = -dx, W, 0, W + dx
if dy >= 0: oy0, oy1, ay0, ay1 = 0, H - dy, dy, H
else:       oy0, oy1, ay0, ay1 = -dy, H, 0, H + dy
out[oy0:oy1, ox0:ox1] = a[ay0:ay1, ax0:ax1]
ys, xs = np.nonzero(out[..., 3] > 8)
print(f"out: {W}x{H}  dx={-dx} dy={-dy}  L={xs.min()} R={W-1-xs.max()} T={ys.min()} B={H-1-ys.max()}")
Image.fromarray(out, "RGBA").save(dst)
