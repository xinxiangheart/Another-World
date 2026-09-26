# -*- coding: utf-8 -*-
import os, zipfile, shutil, glob
import numpy as np
from PIL import Image
from collections import deque

BASE = r"C:\Users\22589\.codex\visualizations\2026\09\26\01a0db8f-c2df-7563-b487-acff1c7f0cec"
BODY = os.path.join(BASE, "body_ref")
if os.path.isdir(BODY): shutil.rmtree(BODY)
os.makedirs(BODY)
z = zipfile.ZipFile(r"C:\Users\22589\OneDrive\Desktop\图片参考.docx")
media = sorted([n for n in z.namelist() if n.startswith("word/media/")],
               key=lambda n: int("".join(c for c in os.path.basename(n) if c.isdigit()) or 0))
for n in media:
    open(os.path.join(BODY, os.path.basename(n)), "wb").write(z.read(n))
print("body_ref", len(media))

def detect(path, out_size=480):
    im = Image.open(path).convert("RGBA")
    if im.width > out_size:
        im = im.resize((out_size, max(1,int(im.height*out_size/im.width))), Image.LANCZOS)
    bgc = Image.new("RGBA", im.size, (40,40,44,255))
    a = np.asarray(Image.alpha_composite(bgc, im).convert("RGB"), dtype=np.float32)
    H, W = a.shape[:2]
    r, g, b = a[...,0], a[...,1], a[...,2]
    mx = a.max(axis=2); mn = a.min(axis=2)
    L = 0.2126*r + 0.7152*g + 0.0722*b
    S = np.where(mx > 0, (mx-mn)/np.maximum(mx,1e-6), 0)
    d = np.maximum(mx-mn, 1e-6); h = np.zeros_like(mx)
    m = (mx == r); h[m] = (60*((g-b)/d) % 360)[m]
    m = (mx == g); h[m] = (60*((b-r)/d)+120)[m]
    m = (mx == b); h[m] = (60*((r-g)/d)+240)[m]
    skin = (h >= 5) & (h <= 40) & (S >= 0.18) & (S <= 0.75) & (L >= 110)
    box = None
    if skin.sum() >= 40:
        seen = np.zeros_like(skin, bool); bestn = 0
        for y in range(H):
            for x in range(W):
                if skin[y,x] and not seen[y,x]:
                    q = deque([(y,x)]); seen[y,x]=True
                    ys=[y]; xs=[x]; n=1
                    while q:
                        cy,cx = q.popleft()
                        for dy,dx in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)):
                            ny,nx = cy+dy,cx+dx
                            if 0<=ny<H and 0<=nx<W and skin[ny,nx] and not seen[ny,nx]:
                                seen[ny,nx]=True; q.append((ny,nx)); n+=1; ys.append(ny); xs.append(nx)
                    if n>bestn: bestn=n; box=(min(ys),max(ys),min(xs),max(xs))
        if bestn < 60: box = None
    return im.at if False else (im, box)

import statistics as st
res = []
for f in sorted(glob.glob(os.path.join(BODY, "*.png"))):
    im, box = detect(f)
    if box:
        y0,y1,x0,x1 = box
        res.append(((y1-y0+1)/im.height, (x1-x0+1)/im.width, ((y0+y1)/2)/im.height))
print("图片参考(全身立绘) n=%d/%d  脸高/画幅中位 %.1f%%  脸宽 %.1f%%  纵向 %.2f" % (
    len(res), len(glob.glob(os.path.join(BODY,'*.png'))),
    st.median([r[0] for r in res])*100, st.median([r[1] for r in res])*100, st.median([r[2] for r in res])))

# verification sheet: draw the detected box
COMM = os.path.join(BASE, "cardref")
def sheet(paths, label):
    tiles = []
    for p in paths:
        im, box = detect(p)
        im = im.convert("RGB").copy()
        if box:
            from PIL import ImageDraw
            d = ImageDraw.Draw(im); y0,y1,x0,x1 = box
            d.rectangle([x0,y0,x1,y1], outline=(0,255,80), width=3)
        tiles.append(im.resize((260, int(260*im.height/im.width)), Image.LANCZOS))
    W = sum(t.width for t in tiles); H = max(t.height for t in tiles)
    s = Image.new("RGB", (W, H), (20,20,24)); x = 0
    for t in tiles: s.paste(t, (x, 0)); x += t.width
    s.save(os.path.join(BASE, label)); print(label, s.size)

sheet([os.path.join(COMM,"mtg",f) for f in sorted(os.listdir(os.path.join(COMM,"mtg")))[:4]]
      + [os.path.join(COMM,"hearthstone",f) for f in sorted(os.listdir(os.path.join(COMM,"hearthstone")))[:2]], "verify_comm.png")
sheet(sorted(glob.glob(os.path.join(BODY,"*.png")))[:6], "verify_body.png")
