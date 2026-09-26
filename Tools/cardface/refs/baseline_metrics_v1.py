# -*- coding: utf-8 -*-
"""Same metrics over commercial card art and over our own library."""
import os, glob, colorsys, statistics as st
import numpy as np
from PIL import Image, ImageFilter

COMM = r"C:\Users\22589\.codex\visualizations\2026\09\26\01a0db8f-c2df-7563-b487-acff1c7f0cec\cardref"
LIB  = r"Assets\_Game\Resources\Cards\Summon"

def lum_and_hsv(im):
    im = im.convert("RGBA")
    a = np.asarray(im, dtype=np.float32)
    rgb, alpha = a[..., :3], a[..., 3]
    r, g, b = rgb[...,0], rgb[...,1], rgb[...,2]
    mx = rgb.max(axis=2); mn = rgb.min(axis=2)
    L = 0.2126*r + 0.7152*g + 0.0722*b
    S = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0.0)
    # hue
    d = np.maximum(mx - mn, 1e-6)
    h = np.zeros_like(mx)
    m = (mx == r); h[m] = (60 * ((g-b)/d) % 360)[m]
    m = (mx == g); h[m] = (60 * ((b-r)/d) + 120)[m]
    m = (mx == b); h[m] = (60 * ((r-g)/d) + 240)[m]
    return L, S, h, alpha

def metrics(path):
    im = Image.open(path)
    if im.width > 640:
        r = 640 / im.width
        im = im.resize((640, max(1, int(im.height*r))), Image.LANCZOS)
    L, S, h, alpha = lum_and_hsv(im)
    H, W = L.shape
    mask = alpha > 128
    if mask.sum() < 100:
        return None
    Lv = L[mask]; Sv = S[mask]; hv = h[mask]
    p10, p50, p90 = np.percentile(Lv, [10, 50, 90])
    out = {}
    out["spread"] = float(p90 - p10)
    out["p50"] = float(p50); out["p90"] = float(p90)
    out["sat_mean"] = float(Sv.mean())
    out["sat_hi"] = float(((Sv > 0.5) & (Lv > 90)).mean())
    # warm skin-like tone: hue 10-45, moderate sat, bright enough
    skin = (hv >= 10) & (hv <= 45) & (Sv >= 0.15) & (Sv <= 0.80) & (Lv >= 90)
    out["skin"] = float(skin.mean())
    # value structure: 16 bins, how many hold >=5%
    hist, _ = np.histogram(Lv, bins=16, range=(0,256))
    frac = hist / max(1, len(Lv))
    out["value_steps"] = int((frac >= 0.05).sum())
    # hue families: 36 bins of 10 deg over saturated pixels
    satm = Sv > 0.25
    if satm.sum() > 50:
        hh, _ = np.histogram(hv[satm], bins=36, range=(0,360))
        out["hue_clusters"] = int(((hh / satm.sum()) >= 0.05).sum())
    else:
        out["hue_clusters"] = 0
    # focal concentration: high-pass energy density centre vs outer
    g = im.convert("L")
    blur = np.asarray(g.filter(ImageFilter.GaussianBlur(6)), dtype=np.float32)
    hp = np.abs(np.asarray(g, dtype=np.float32) - blur)
    cy0, cy1 = int(H*0.2), int(H*0.8); cx0, cx1 = int(W*0.2), int(W*0.8)
    centre = hp[cy0:cy1, cx0:cx1]; outer = hp.copy()
    outer[cy0:cy1, cx0:cx1] = np.nan
    cm = np.nanmean(centre); om = np.nanmean(outer)
    out["focal"] = float(cm / om) if om > 0 else float("nan")
    out["detail"] = float(hp.mean())
    # thumbnail legibility: contrast surviving at 96x128
    th = g.convert("L").resize((96, 128), Image.LANCZOS)
    out["thumb_std"] = float(np.asarray(th, dtype=np.float32).std())
    return out

groups = {}
for game in sorted(os.listdir(COMM)):
    d = os.path.join(COMM, game)
    if not os.path.isdir(d): continue
    fs = [f for f in sorted(os.listdir(d)) if not f.startswith("FULL_")]
    groups["商业:" + game] = [os.path.join(d, f) for f in fs]

libmap = {"Hero/1": "1费", "Hero/3": "3费", "Hero/5": "5费"}
for sub, label in libmap.items():
    groups["我们:" + label] = sorted(glob.glob(os.path.join(LIB, sub.replace("/", os.sep), "*.png")))
groups["我们:Special"] = sorted(glob.glob(os.path.join(LIB, "Special", "*.png")))
groups["我们:ChosenOne"] = sorted(glob.glob(os.path.join(LIB, "ChosenOne", "*.png")))

keys = ["spread","p50","p90","sat_mean","sat_hi","skin","value_steps","hue_clusters","focal","detail","thumb_std"]
rows = []
for gname, paths in groups.items():
    vals = []
    for p in paths:
        try:
            m = metrics(p)
            if m: vals.append(m)
        except Exception as e:
            pass
    if not vals: continue
    rows.append((gname, len(vals), {k: st.median([v[k] for v in vals]) for k in keys}))

hdr = "%-22s %4s %7s %5s %5s %7s %6s %6s %5s %5s %6s %6s %6s" % (
    "group","n","spread","p50","p90","sat_mean","satHI%","skin%","vstep","hueN","focal","detail","thumbSD")
print(hdr); print("-"*len(hdr))
for gname, n, m in rows:
    print("%-22s %4d %7.1f %5.1f %5.1f %8.3f %6.1f %6.1f %5d %5d %6.2f %6.2f %6.1f" % (
        gname, n, m["spread"], m["p50"], m["p90"], m["sat_mean"], m["sat_hi"]*100,
        m["skin"]*100, m["value_steps"], m["hue_clusters"], m["focal"], m["detail"], m["thumb_std"]))
