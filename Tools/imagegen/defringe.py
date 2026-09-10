"""Strip chroma-key colour contamination from the rim of a cutout.

usage: defringe.py <src.png> <dst.png> [radius] [t_min] [t_abs] [mode]

  mode   mix (default) apply the tests above; all  repair every rim pixel
         unconditionally.  Use `all` for line art, where the rim is only ever
         the outline or the adjacent flat fill, so replacing it with F cannot
         invent anything.  It is the only way to clear a rim whose F is pale.

  t_abs  optional absolute guard: a rim pixel is also repaired when its colour
         is within t_abs of the key colour, whatever the mix fraction says.
         Needed because `t` is measured against F, the nearest interior colour,
         and when F is itself pale (a light grey cuff, a white highlight, an off
         white key) a washed-out rim pixel scores a low `t` and survives as a
         bead of key colour.  0 (the default) disables the guard.

Only pixels within `radius` px of the transparent region are considered, so a
colour the artwork legitimately uses near the key colour is left alone.  Stray
magenta *enclosed* by the subject never reaches that rim and is not fixed
here -- remove it first with `purge_key.py`, which is what it is for.

A magenta chroma key leaves a rim of pixels whose RGB is a blend of the key
colour and the subject, e.g. 50% gold + 50% magenta = pink.  Those pixels can
sit at alpha ~= 255, so an alpha-only fix does nothing and the cutout shows a
pink halo on light and dark grounds alike.

This keeps every alpha value and only repairs RGB, and only where the pixel is
genuinely a mix of key and subject:

  core      = alpha==255 eroded by `radius`      (certain interior colour)
  rim       = alpha>0 within `radius` px of alpha==0
  F         = nearest core colour, propagated outward into the rim
  t         = mix fraction solving C = (1-t)*F + t*K   (K = corner key colour)
  t <= t_min -> leave the pixel alone (it is real subject colour, e.g. the
                black outline, which merely happens to sit near the edge)
  t >  t_min -> replace it with F, the interior colour the rim pixel belongs
                to.  Unmixing, RGB = (C - t*K)/(1-t), is the textbook inverse
                but it amplifies the error in F enormously once t is high
                (a 75% magenta rim pixel unmixed against the wrong F came out
                bright green), and flat cel line art does not need it: the rim
                is either the black outline or the adjacent flat fill, and F
                already is one of those two.

Alpha is untouched, so anti-aliasing survives; only the contamination goes.
"""
import sys
import numpy as np
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
radius = int(sys.argv[3]) if len(sys.argv) > 3 else 3
t_min = float(sys.argv[4]) if len(sys.argv) > 4 else 0.05
t_abs = float(sys.argv[5]) if len(sys.argv) > 5 else 0.0
mode = sys.argv[6] if len(sys.argv) > 6 else "mix"

im = Image.open(src).convert("RGBA")
a = np.asarray(im).astype(np.float32)
alpha = a[..., 3]
rgb = a[..., :3].copy()

h, w = alpha.shape
cs = 8
corner_rgb = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
key = np.concatenate([corner_rgb[:cs, :cs].reshape(-1, 3),
                      corner_rgb[:cs, -cs:].reshape(-1, 3),
                      corner_rgb[-cs:, :cs].reshape(-1, 3),
                      corner_rgb[-cs:, -cs:].reshape(-1, 3)]).mean(axis=0)
print(f"key={key.round(1)}  radius={radius}  t_min={t_min}")

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
rim = (alpha > 0.0) & ~core
print(f"solid={int(solid.sum())}  core={int(core.sum())}  rim={int(rim.sum())}")

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
print(f"rim pixels without a core neighbour: {int((rim & ~known).sum())}")

F = cur
KF = key[None, None, :] - F
denom = (KF ** 2).sum(axis=2)
denom[denom < 1e-6] = 1e-6
t = (((rgb - F) * KF).sum(axis=2) / denom)
t = np.clip(t, 0.0, 0.98)
d_key = np.sqrt(((rgb - key[None, None, :]) ** 2).sum(axis=2))
hit = rim if mode == "all" else rim & ((t > t_min) | (d_key < t_abs))
print(f"decontaminated pixels: {int(hit.sum())}  (max t={float(t[rim].max()) if rim.any() else 0:.2f})")

out_rgb = np.where(hit[..., None], F, rgb)

out = np.dstack([np.clip(out_rgb, 0, 255), alpha]).astype(np.uint8)
Image.fromarray(out, "RGBA").save(dst)
print(f"saved {dst}")
