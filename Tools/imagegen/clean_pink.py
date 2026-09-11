"""Strip pink/magenta chroma-key residue from the rim of an existing cutout.

Unlike defringe.py this does NOT need the original key colour, and it does not
use "how much of this pixel is key colour" as its test.  It walks the rim (the
band of non-transparent pixels within `radius` px of the transparent region)
and repairs any rim pixel whose pink score is above the threshold.

  pink score = min(R, B) - G

That score is ~0 for neutral art, negative for warm skin (red high, blue low),
and only large for the "red high AND blue high" mix that a magenta key leaves
behind.  So it separates contamination from legitimate warm colours, which a
dist-to-key test cannot do once the key colour itself is unknown or the subject
near the edge is pale.

The replacement colour is F, the nearest interior colour (core = alpha==255
eroded by `radius`), propagated outward -- i.e. the flat fill or outline the
rim pixel actually belongs to.  Alpha is never touched, so anti-aliasing
survives; only contaminated RGB changes.

usage: clean_pink.py <src.png> <dst.png> [T] [radius] [min_alpha]
  T          pink-score threshold, default 25
  radius     rim band width in px, default 3
  min_alpha  skip pixels at or below this alpha, default 0
"""
import sys
import numpy as np
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
T = float(sys.argv[3]) if len(sys.argv) > 3 else 25.0
radius = int(sys.argv[4]) if len(sys.argv) > 4 else 3
min_alpha = float(sys.argv[5]) if len(sys.argv) > 5 else 0.0

im = Image.open(src).convert("RGBA")
a = np.asarray(im).astype(np.float32)
alpha = a[..., 3]
rgb = a[..., :3].copy()
h, w = alpha.shape

bg = alpha <= 0.0
near_bg = bg.copy()
for dy in range(-radius, radius + 1):
    for dx in range(-radius, radius + 1):
        sh = np.zeros_like(bg)
        ys0, ys1 = max(dy, 0), h + min(dy, 0)
        xs0, xs1 = max(dx, 0), w + min(dx, 0)
        sh[ys0:ys1, xs0:xs1] = bg[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
        near_bg |= sh

solid = alpha >= 255.0
core = solid & ~near_bg
rim = (alpha > 0.0) & ~core & (alpha > min_alpha)
print(f"solid={int(solid.sum())} core={int(core.sum())} rim={int(rim.sum())} radius={radius}")

cur = rgb.copy()
known = core.copy()
for _ in range(radius * 4 + 4):
    todo = rim & ~known
    if not todo.any():
        break
    acc = np.zeros_like(cur)
    cnt = np.zeros(alpha.shape, dtype=np.float32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy == 0 and dx == 0:
                continue
            sh_k = np.zeros_like(known)
            sh_c = np.zeros_like(cur)
            ys0, ys1 = max(dy, 0), h + min(dy, 0)
            xs0, xs1 = max(dx, 0), w + min(dx, 0)
            sh_k[ys0:ys1, xs0:xs1] = known[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
            sh_c[ys0:ys1, xs0:xs1] = cur[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
            acc += sh_c * sh_k[..., None]
            cnt += sh_k
    got = todo & (cnt > 0)
    cur[got] = acc[got] / cnt[got][:, None]
    known |= got
print(f"rim pixels with no core in reach: {int((rim & ~known).sum())}")

pink = np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1]
hit = rim & (pink > T)
print(f"pink score before: max={float(pink[rim].max()) if rim.any() else 0:.0f}  "
      f"rim px above T={T:.0f}: {int(hit.sum())}")

out_rgb = np.where(hit[..., None], cur, rgb)
out = np.dstack([np.clip(out_rgb, 0, 255), alpha]).astype(np.uint8)
Image.fromarray(out, "RGBA").save(dst)

after = np.minimum(out[..., 0].astype(np.float32), out[..., 2].astype(np.float32)) - out[..., 1]
op = alpha > 128
print(f"saved {dst}   pink>{T:.0f} & alpha>128: {int(((pink > T) & op).sum())} -> {int(((after > T) & op).sum())}")
