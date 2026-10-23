# -*- coding: utf-8 -*-
"""SlotPlate v8: same silhouette, wider rim + deeper inset bevel.
Outputs candidates + 1:1 / 4x previews (linear-space compositing, Unity-like)."""
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(r"C:\Users\22589\Documents\GitHub\Another-World")
SRC = ROOT / "Assets/_Game/Art/Sprites/Board/SlotPlate.png"
OUT = ROOT / "Assets/_Game/Art/Sprites/Generated/slot-v8"
OUT.mkdir(parents=True, exist_ok=True)

a0 = np.array(Image.open(SRC))[:, :, 3]
h, w = a0.shape
R = 61.0

yy, xx = np.mgrid[0:h, 0:w]
px, py = xx + 0.5, yy + 0.5
dx = np.maximum(np.abs(px - w / 2) - (w / 2 - R), 0)
dy = np.maximum(np.abs(py - h / 2) - (h / 2 - R), 0)
dist_out = np.sqrt(dx * dx + dy * dy) - R
mask = np.clip(0.5 - dist_out, 0, 1)          # coverage, AA on the silhouette

m = mask > 0.5
top = np.array([np.argmax(m[:, x]) if m[:, x].any() else -1 for x in range(w)])
bot = np.array([h - 1 - np.argmax(m[::-1, x]) if m[:, x].any() else -1 for x in range(w)])
lef = np.array([np.argmax(m[y, :]) if m[y, :].any() else -1 for y in range(h)])
rig = np.array([w - 1 - np.argmax(m[y, ::-1]) if m[y, :].any() else -1 for y in range(h)])

dT = yy - top[None, :]
dB = bot[None, :] - yy
dL = xx - lef[:, None]
dR = rig[:, None] - xx
dm = np.minimum(np.minimum(dT, dB), np.minimum(dL, dR))

def build(base, rim_band, rim_a, bt, bl, bb, br, top_amt, bot_amt, left_amt, right_amt):
    a = np.full((h, w), base, dtype=np.float64)
    t = np.clip(1.0 - (dT - rim_band) / bt, 0, 1); a -= top_amt * t
    t = np.clip(1.0 - (dL - rim_band) / bl, 0, 1); a -= left_amt * t
    t = np.clip(1.0 - (dB - rim_band) / bb, 0, 1); a += bot_amt * t
    t = np.clip(1.0 - (dR - rim_band) / br, 0, 1); a += right_amt * t
    a = np.where(dm < rim_band, rim_a, a)
    a = np.clip(a, 0, 1) * mask
    return a

variants = {
    "SlotPlate_v8_a.png": build(0.180, 4, 0.92, 110, 60, 55, 50, 0.34, 0.28, 0.12, 0.09),
    "SlotPlate_v8_b.png": build(0.180, 6, 0.95, 110, 70, 60, 55, 0.34, 0.34, 0.14, 0.10),
}
for name, a in variants.items():
    out = np.zeros((h, w, 4), dtype=np.uint8)
    out[..., :3] = 255
    out[..., 3] = np.round(a * 255).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(OUT / name)

# ---------- preview ----------
def srgb_to_lin(v):
    v = np.asarray(v, dtype=np.float64)
    return np.where(v <= 0.04045, v / 12.92, ((v + 0.055) / 1.055) ** 2.4)

def lin_to_srgb(v):
    v = np.clip(v, 0, 1)
    return np.where(v <= 0.0031308, v * 12.92, 1.055 * v ** (1 / 2.4) - 0.055)

bg = srgb_to_lin(np.array([20, 25, 32]) / 255.0)

def compose(alpha, size=(108, 192)):
    im = Image.fromarray(np.round(alpha * 255).astype(np.uint8), "L").resize(size, Image.LANCZOS)
    ai = np.asarray(im, dtype=np.float64) / 255.0
    lin = bg[None, None, :] * (1 - ai[..., None]) + 1.0 * ai[..., None]
    return np.round(lin_to_srgb(lin) * 255).astype(np.uint8)

cur = a0.astype(np.float64) / 255.0
tiles = [("current", compose(cur))] + [(n.split("_")[-1].split(".")[0], compose(v)) for n, v in variants.items()]
scale = 3
gap = 18
tw, th = 108 * scale, 192 * scale
canvas = Image.new("RGB", (len(tiles) * tw + (len(tiles) + 1) * gap, th + 2 * gap + 28), (12, 16, 22))
from PIL import ImageDraw, ImageFont
d = ImageDraw.Draw(canvas)
for i, (label, arr) in enumerate(tiles):
    im = Image.fromarray(arr).resize((tw, th), Image.NEAREST)
    x = gap + i * (tw + gap)
    canvas.paste(im, (x, gap))
    d.text((x + 4, gap + th + 6), label, fill=(210, 200, 170))
canvas.save(OUT / "preview-slot-v8.png")

# 6x zoom of the top-left corner + bottom edge
zoom = 6
zcanvas = Image.new("RGB", (tw // 3 + 40, th + 40), (12, 16, 22))
zcanvas.paste(Image.fromarray(tiles[0][1]).crop((0, 0, 36, 40)).resize((36 * zoom, 40 * zoom), Image.NEAREST), (10, 10))
zcanvas.save(OUT / "preview-slot-v8-zoom-current.png")

print("wrote", OUT)
print("current alpha: fill", int(np.bincount(a0[:, w // 2]).argmax()))
for n, v in variants.items():
    print(n, "rim", int(v[0, w // 2] * 255), "fill", int(v[h // 2, w // 2] * 255),
          "bottom", int(v[h - 12, w // 2] * 255))
