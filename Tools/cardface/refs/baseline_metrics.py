# -*- coding: utf-8 -*-
import os, glob, statistics as st
import numpy as np
from PIL import Image, ImageFilter

COMM = r"C:\Users\22589\.codex\visualizations\2026\09\26\01a0db8f-c2df-7563-b487-acff1c7f0cec\cardref"
LIB  = r"Assets\_Game\Resources\Cards\Summon"

def prep(path):
    im = Image.open(path).convert("RGBA")
    if im.width > 640:
        r = 640 / im.width
        im = im.resize((640, max(1, int(im.height*r))), Image.LANCZOS)
    a = np.asarray(im, dtype=np.uint8)
    alpha = a[..., 3].astype(np.float32)
    has_alpha = alpha.min() < 200
    if has_alpha:
        ys, xs = np.where(alpha > 128)
        if len(ys) < 100: return None
        im = im.crop((xs.min(), ys.min(), xs.max()+1, ys.max()+1))
        a = np.asarray(im, dtype=np.uint8)
        alpha = a[..., 3].astype(np.float32)
    return im, a.astype(np.float32), alpha, has_alpha

def metrics(path):
    pr = prep(path)
    if pr is None: return None
    im, a, alpha, has_alpha = pr
    rgb = a[..., :3]
    r, g, b = rgb[...,0], rgb[...,1], rgb[...,2]
    mx = rgb.max(axis=2); mn = rgb.min(axis=2)
    L = 0.2126*r + 0.7152*g + 0.0722*b
    S = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0.0)
    d = np.maximum(mx - mn, 1e-6)
    h = np.zeros_like(mx)
    m = (mx == r); h[m] = (60 * ((g-b)/d) % 360)[m]
    m = (mx == g); h[m] = (60 * ((b-r)/d) + 120)[m]
    m = (mx == b); h[m] = (60 * ((r-g)/d) + 240)[m]
    mask = alpha > 128
    Lv, Sv, hv = L[mask], S[mask], h[mask]
    p10, p50, p90 = np.percentile(Lv, [10, 50, 90])
    o = {"spread": float(p90-p10), "p50": float(p50), "p90": float(p90),
         "bright": float((Lv > 200).mean()),
         "dark": float((Lv < 45).mean()),
         "sat_mean": float(Sv.mean()),
         "sat_hi": float(((Sv > 0.5) & (Lv > 90)).mean()),
         "skin": float(((hv>=10)&(hv<=45)&(Sv>=0.15)&(Sv<=0.80)&(Lv>=90)).mean())}
    hist, _ = np.histogram(Lv, bins=16, range=(0,256))
    o["value_steps"] = int(((hist/len(Lv)) >= 0.05).sum())
    sm = Sv > 0.25
    o["hue_clusters"] = int((((np.histogram(hv[sm], bins=36, range=(0,360))[0]/max(1,sm.sum())) >= 0.05).sum())) if sm.sum() > 50 else 0
    # focal: high-pass energy density, centre 60% box vs the rest, INSIDE the mask
    lo = np.asarray(im.convert("L"), dtype=np.float32)
    blur = np.asarray(im.convert("L").filter(ImageFilter.GaussianBlur(6)), dtype=np.float32)
    hp = np.abs(lo - blur)
    H, W = hp.shape
    cy0, cy1, cx0, cx1 = int(H*0.2), int(H*0.8), int(W*0.2), int(W*0.8)
    box = np.zeros_like(mask); box[cy0:cy1, cx0:cx1] = True
    ci, oi = mask & box, mask & ~box
    o["focal"] = float(hp[ci].mean()/hp[oi].mean()) if ci.sum()>50 and oi.sum()>50 and hp[oi].mean()>0 else float("nan")
    o["detail"] = float(hp[mask].mean())
    # thumbnail legibility on mid-grey composite of the CROP
    bgc = Image.new("RGBA", im.size, (32, 32, 32, 255))
    flat = Image.alpha_composite(bgc, im).convert("L").resize((96, 128), Image.LANCZOS)
    o["thumb_std"] = float(np.asarray(flat, dtype=np.float32).std())
    return o

groups = {}
for game in sorted(os.listdir(COMM)):
    d = os.path.join(COMM, game)
    if os.path.isdir(d):
        groups["商业:" + game] = [os.path.join(d, f) for f in sorted(os.listdir(d)) if not f.startswith("FULL_")]
for sub, label in [("Hero/1","1费"),("Hero/3","3费"),("Hero/5","5费"),("Special","Special"),("ChosenOne","ChosenOne")]:
    groups["我们:" + label] = sorted(glob.glob(os.path.join(LIB, sub.replace("/", os.sep), "*.png")))

keys = ["spread","p50","p90","bright","dark","sat_mean","sat_hi","skin","value_steps","hue_clusters","focal","detail","thumb_std"]
rows = []
allcomm = []
for gname, paths in groups.items():
    vals = []
    for p in paths:
        try:
            m = metrics(p)
            if m: vals.append(m)
        except Exception: pass
    if not vals: continue
    if gname.startswith("商业"): allcomm += vals
    rows.append((gname, len(vals), {k: st.median([v[k] for v in vals]) for k in keys}))

# commercial aggregate
agg = {k: st.median([v[k] for v in allcomm]) for k in keys}
hdr = "%-20s %4s %6s %6s %6s %6s %6s %6s %6s %6s %5s %5s %6s %6s %6s" % (
    "group","n","spread","p50","p90",">200%","<45%","satMn","satHI%","skin%","vstep","hueN","focal","detail","thumbSD")
print(hdr); print("-"*len(hdr))
for gname, n, m in rows:
    print("%-20s %4d %6.0f %6.0f %6.0f %6.1f %6.1f %6.2f %6.1f %6.1f %5d %5d %6.2f %6.2f %6.1f" % (
        gname, n, m["spread"], m["p50"], m["p90"], m["bright"]*100, m["dark"]*100, m["sat_mean"],
        m["sat_hi"]*100, m["skin"]*100, m["value_steps"], m["hue_clusters"], m["focal"], m["detail"], m["thumb_std"]))
print("-"*len(hdr))
print("%-20s %4d %6.0f %6.0f %6.0f %6.1f %6.1f %6.2f %6.1f %6.1f %5d %5d %6.2f %6.2f %6.1f" % (
    "商业-四家合计", len(allcomm), agg["spread"], agg["p50"], agg["p90"], agg["bright"]*100, agg["dark"]*100,
    agg["sat_mean"], agg["sat_hi"]*100, agg["skin"]*100, agg["value_steps"], agg["hue_clusters"],
    agg["focal"], agg["detail"], agg["thumb_std"]))
