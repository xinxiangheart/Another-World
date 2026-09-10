"""Drop leftover key-coloured pixels from a cutout.

usage: purge_key.py <src.png> <dst.png> <T> [mode] [corner_sample]

  mode  dist (default)  drop pixels within T of the key colour, as measured in
                        RGB.  Simple, and the right tool when the leftovers are
                        strong magenta.
        pink            drop pixels whose red and blue both exceed green by
                        more than T.  Use it for faded artefacts: a washed-out
                        magenta strand measured d=94 from the key (past any
                        sane dist threshold) while still scoring 100 on this
                        test, versus 20 for the purple-tinged grey hair it
                        sits next to.

`key_flood.py` deliberately protects interior magenta as subject glow, so
stray magenta strokes that the generator left *inside* the silhouette survive
the key.  When the artwork has no deliberate near-key colour (no purple glow,
no magenta rim light) that protection is wrong and the leftovers read as pink
scratches.

This zeroes the alpha of those pixels.  Pick T well below the score of any
colour the art really uses: measured this session, an unwanted magenta strand
sat at d=36 while the purple-tinged grey of the hair sat at d=158, so T=80
separated them cleanly on `dist`.
"""
import sys
import numpy as np
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
T = float(sys.argv[3])
mode = sys.argv[4] if len(sys.argv) > 4 else "dist"
cs = int(sys.argv[5]) if len(sys.argv) > 5 else 8

im = Image.open(src).convert("RGBA")
a = np.asarray(im)
alpha = a[..., 3].copy()
rgb = a[..., :3].astype(np.float32)

key = np.concatenate([rgb[:cs, :cs].reshape(-1, 3), rgb[:cs, -cs:].reshape(-1, 3),
                      rgb[-cs:, :cs].reshape(-1, 3), rgb[-cs:, -cs:].reshape(-1, 3)]).mean(axis=0)
if mode == "pink":
    score = np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1]
    label = "min(R,B)-G"
else:
    score = np.sqrt(((rgb - key) ** 2).sum(axis=2))
    label = "dist-to-key"
hit = (alpha > 0) & (score > T) if mode == "pink" else (alpha > 0) & (score < T)
print(f"key={key.round(1)}  mode={mode}  T={T} ({label})  dropping {int(hit.sum())} px")
alpha[hit] = 0

out = np.dstack([a[..., :3], alpha]).astype(np.uint8)
Image.fromarray(out, "RGBA").save(dst)
print(f"saved {dst}")
