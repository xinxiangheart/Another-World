# -*- coding: utf-8 -*-
"""How much of the frame height is a FACE, in commercial card art vs the full-body refs."""
import os, glob, statistics as st
from collections import deque
import numpy as np
from PIL import Image

COMM = r"C:\Users\22589\.codex\visualizations\2026\09\26\01a0db8f-c2df-7563-b487-acff1c7f0cec\cardref"
REFS = r"C:\Users\22589\.codex\visualizations\2026\09\26\01a0db8f-c2df-7563-b487-acff1c7f0cec\face_ref"
LIB  = r"Assets\_Game\Resources\Cards\Summon"

def skin_blob(path):
    im = Image.open(path).convert("RGBA")
    if im.width > 480:
        im = im.resize((480, max(1,int(im.height*480/im.width))), Image.LANCZOS)
    bgc = Image.new("RGBA", im.size, (40,40,44,255))
    a = np.asarray(Image.alpha_composite(bgc, im).convert("RGB"), dtype=np.float32)
    H, W = a.shape[:2]
    r, g, b = a[...,0], a[...,1], a[...,2]
    mx = a.max(axis=2); mn = a.min(axis=2)
    L = 0.2126*r + 0.7152*g + 0.0722*b
    S = np.where(mx > 0, (mx-mn)/np.maximum(mx,1e-6), 0)
    d = np.maximum(mx-mn, 1e-6)
    h = np.zeros_like(mx)
    m = (mx == r); h[m] = (60*((g-b)/d) % 360)[m]
    m = (mx == g); h[m] = (60*((b-r)/d)+120)[m]
    m = (mx == b); h[m] = (60*((r-g)/d)+240)[m]
    skin = (h >= 5) & (h <= 40) & (S >= 0.18) & (S <= 0.75) & (L >= 110)
    if skin.sum() < 40: return None
    seen = np.zeros_like(skin, bool); best = None; bestn = 0
    for y in range(H):
        for x in range(W):
            if skin[y,x] and not seen[y,x]:
                q = deque([(y,x)]); seen[y,x] = True
                ys = [y]; xs = [x]; n = 1
                while q:
                    cy, cx = q.popleft()
                    for dy, dx in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)):
                        ny, nx = cy+dy, cx+dx
                        if 0 <= ny < H and 0 <= nx < W and skin[ny,nx] and not seen[ny,nx]:
                            seen[ny,nx] = True; q.append((ny,nx)); n += 1
                            ys.append(ny); xs.append(nx)
                if n > bestn: bestn = n; best = (min(ys),max(ys),min(xs),max(xs))
    if best is None or bestn < 60: return None
    y0, y1, x0, x1 = best
    return {"faceH": (y1-y0+1)/H, "faceW": (x1-x0+1)/W,
            "cy": ((y0+y1)/2)/H, "area": bestn/(H*W)}

groups = {}
for game in sorted(os.listdir(COMM)):
    d = os.path.join(COMM, game)
    if os.path.isdir(d):
        groups["商业:"+game] = [os.path.join(d,f) for f in sorted(os.listdir(d)) if not f.startswith("FULL_")]
groups["参考:明日方舟32"] = sorted(glob.glob(os.path.join(REFS, "*.png")))
for sub, lab in [("Hero/1","1费"),("Hero/3","3费"),("Hero/5","5费")]:
    groups["我们:"+lab] = sorted(glob.glob(os.path.join(LIB, sub.replace("/", os.sep), "*.png")))

print("%-20s %4s %6s %10s %10s %10s" % ("group","n(有脸)","/总","脸高/画幅","脸宽/画幅","脸的纵向位置"))
print("-"*70)
for g, ps in groups.items():
    res = []
    for p in ps:
        try:
            s = skin_blob(p)
            if s: res.append(s)
        except Exception: pass
    if not res:
        print("%-20s %4d %6d   (测不到脸)" % (g, 0, len(ps))); continue
    print("%-20s %4d %6d %9.1f%% %9.1f%% %11.2f" % (
        g, len(res), len(ps),
        st.median([x["faceH"] for x in res])*100,
        st.median([x["faceW"] for x in res])*100,
        st.median([x["cy"] for x in res])))
