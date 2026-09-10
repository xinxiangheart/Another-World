"""Composite one region of a re-rendered image back onto the original cutout.

usage: patch_region.py <orig.png> <edit.png> <dst.png> "<x0,y0,x1,y1>" <feather>

A masked edit via /v1/images/edits re-renders the WHOLE image, so the result is
never a surgical change.  When only one small part should really move, keep the
original and take just that part from the edit.

The region is a *crossfade*, not an overlay:

  prem_out = prem_e * M + prem_o * (1 - M)     (prem = colour * alpha)
  out_a    = edit_a * M + orig_a * (1 - M)

Overlaying instead (edit over original) looks right at first and is wrong: the
edit is transparent wherever it decided the background is, so whatever the
original had there -- the very artefact we are replacing, e.g. a detached scrap
of the old broken hand -- survives untouched.  A crossfade inside the region
and the untouched original outside is what "change only this part" means.

Both images must already be the same size and aligned; verify by comparing the
alpha bounding boxes before trusting the result.
"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

orig_p, edit_p, dst = sys.argv[1], sys.argv[2], sys.argv[3]
x0, y0, x1, y1 = [int(v) for v in sys.argv[4].split(",")]
feather = int(sys.argv[5]) if len(sys.argv) > 5 else 18

o = Image.open(orig_p).convert("RGBA")
e = Image.open(edit_p).convert("RGBA")
if o.size != e.size:
    raise SystemExit(f"size mismatch: {o.size} vs {e.size}")

W, H = o.size
region = Image.new("L", (W, H), 0)
ImageDraw.Draw(region).rounded_rectangle([x0, y0, x1, y1],
                                         radius=int(min(x1 - x0, y1 - y0) * 0.28), fill=255)
M = np.asarray(region.filter(ImageFilter.GaussianBlur(feather))).astype(np.float32) / 255.0

oc = np.asarray(o).astype(np.float32)
ec = np.asarray(e).astype(np.float32)
oc_a = oc[..., 3] / 255.0
ec_a = ec[..., 3] / 255.0

out_a = ec_a * M + oc_a * (1.0 - M)
prem = ec[..., :3] * ec_a[..., None] * M[..., None] + oc[..., :3] * oc_a[..., None] * (1.0 - M)[..., None]
out_c = prem / np.maximum(out_a, 1e-6)[..., None]

out = np.dstack([np.clip(out_c, 0, 255), np.clip(out_a * 255.0 + 0.5, 0, 255)]).astype(np.uint8)
Image.fromarray(out, "RGBA").save(dst)
print(f"patched region x[{x0},{x1}] y[{y0},{y1}] feather={feather}  masked px={int((M>0.5).sum())}")
print(f"saved {dst}")
