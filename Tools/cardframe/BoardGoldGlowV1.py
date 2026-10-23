# -*- coding: utf-8 -*-
"""Board gold glow v1: extract gold lines from Surface/Ornament/Rune, blur, additively overlay."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(r"C:\Users\22589\Documents\GitHub\Another-World")
SRC = ROOT / "Assets/_Game/Art/Sprites/Generated/board-layers-v2"
OUT = ROOT / "Assets/_Game/Art/Sprites/Generated/board-goldglow-v1"
OUT.mkdir(parents=True, exist_ok=True)

def load(name):
    im = Image.open(SRC / name).convert("RGBA")
    return np.asarray(im).astype(np.float64) / 255.0

def rgb_to_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = np.max(rgb, axis=-1); mn = np.min(rgb, axis=-1)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    idx = (mx == r) & m
    h[idx] = ((g[idx] - b[idx]) / d[idx]) % 6
    idx = (mx == g) & m
    h[idx] = (b[idx] - r[idx]) / d[idx] + 2
    idx = (mx == b) & m
    h[idx] = (r[idx] - g[idx]) / d[idx] + 4
    h *= 60.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx

def gold_layer(name, gain=1.0):
    a = load(name)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    h, s, v = rgb_to_hsv(a[..., :3])
    mask = (h > 25) & (h < 70) & (s > 0.12) & (v > 0.25) & (al > 0.03)
    cov = np.where(mask, al * gain, 0.0)
    # lift toward the brightest gold, keep source hue
    rgb = np.stack([r, g, b], axis=-1)
    tint = np.array([0.90, 0.79, 0.52])
    rgb = rgb * 0.65 + tint * 0.35
    print(name, "gold px", int(mask.sum()), "coverage peak", round(float(cov.max()), 3))
    return rgb, cov

def premult_blur(rgb, a, radius):
    pre = Image.fromarray(np.dstack([rgb * a[..., None] * 255, a * 255]).astype(np.uint8), "RGBA")
    pre = pre.filter(ImageFilter.GaussianBlur(radius))
    arr = np.asarray(pre).astype(np.float64) / 255.0
    aa = arr[..., 3]
    out_rgb = np.where(aa[..., None] > 1e-4, arr[..., :3] / np.maximum(aa[..., None], 1e-4), 0.0)
    return out_rgb, aa

sources = ["Board_Surface.png", "Board_Ornament.png", "Board_Rune.png"]
rgbs, covs = zip(*[gold_layer(n) for n in sources])
rgb = np.zeros_like(rgbs[0]); cov = np.zeros_like(covs[0])
for r_, c_ in zip(rgbs, covs):
    rgb += r_ * c_[..., None]; cov += c_
rgb = np.where(cov[..., None] > 1e-4, rgb / np.maximum(cov[..., None], 1e-4), 0.0)

core_rgb, core_a = premult_blur(rgb, cov, 3.0)
halo_rgb, halo_a = premult_blur(rgb, cov, 22.0)
a = np.clip(0.45 * core_a + 0.55 * halo_a, 0, 1) * 0.55
rgbf = np.where(a[..., None] > 1e-4,
                (0.45 * core_rgb * core_a[..., None] + 0.55 * halo_rgb * halo_a[..., None]) /
                np.maximum(0.45 * core_a[..., None] + 0.55 * halo_a[..., None], 1e-4), 0.0)

out = np.zeros((*a.shape, 4), dtype=np.uint8)
out[..., :3] = np.round(np.clip(rgbf, 0, 1) * 255).astype(np.uint8)
out[..., 3] = np.round(np.clip(a, 0, 1) * 255).astype(np.uint8)
Image.fromarray(out, "RGBA").save(OUT / "Board_GoldGlow.png")
print("alpha max", int(out[..., 3].max()), "mean(where>0)", round(float(a[a > 0].mean()), 3))
print("wrote", OUT / "Board_GoldGlow.png")

# ---------- 与原 Board_Glow 合成（Linear 空间） ----------
# 原图固定读 Board_Glow_base.png：已装的 Board_Glow.png 是合成后的成品，重复读会叠加两次。
BASE = OUT / "Board_Glow_base.png"
MUL = 0.30

def to_lin(v):
    v = np.asarray(v, dtype=np.float64)
    return np.where(v <= 0.04045, v / 12.92, ((v + 0.055) / 1.055) ** 2.4)
def to_srgb(v):
    v = np.clip(v, 0, 1)
    return np.where(v <= 0.0031308, v * 12.92, 1.055 * v ** (1 / 2.4) - 0.055)

base = np.asarray(Image.open(BASE).convert("RGBA")).astype(np.float64) / 255.0
gold = np.asarray(Image.open(OUT / "Board_GoldGlow.png").convert("RGBA")).astype(np.float64) / 255.0
a1, a2 = base[..., 3], gold[..., 3] * MUL
r1, r2 = to_lin(base[..., :3]), to_lin(gold[..., :3])
a_out = a1 + a2 * (1 - a1)
rgb_out = (r1 * a1[..., None] + r2 * a2[..., None] * (1 - a1[..., None])) / np.maximum(a_out[..., None], 1e-6)

merged = np.zeros((*a_out.shape, 4), dtype=np.uint8)
merged[..., :3] = np.round(to_srgb(rgb_out) * 255).astype(np.uint8)
merged[..., 3] = np.round(np.clip(a_out, 0, 1) * 255).astype(np.uint8)
Image.fromarray(merged, "RGBA").save(OUT / "Board_Glow_v2_with_gold.png")
print("merged alpha max", int(merged[..., 3].max()))
