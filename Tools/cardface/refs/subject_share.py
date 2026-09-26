# -*- coding: utf-8 -*-
import os, glob, statistics as st
from collections import deque
import numpy as np
from PIL import Image

COMM = r"C:\Users\22589\.codex\visualizations\2026\09\26\01a0db8f-c2df-7563-b487-acff1c7f0cec\cardref"
LIB  = r"Assets\_Game\Resources\Cards\Summon"

def subject_share(im, alpha=None):
    """Largest salient blob as a share of the frame (rough 'subject occupancy')."""
    g = im.convert("RGB").resize((96, 128), Image.LANCZOS)
    a = np.asarray(g, dtype=np.float32)
    if alpha is not None:
        m = np.asarray(alpha.resize((96,128), Image.LANCZOS), dtype=np.float32) > 128
    else:
        m = np.ones(a.shape[:2], dtype=bool)
    if m.sum() < 50: return float("nan")
    border = np.concatenate([a[0], a[-1], a[:,0], a[:,-1]])
    bgc = border.mean(axis=0)
    d = np.linalg.norm(a - bgc, axis=2)
    d[~m] = 0
    thr = np.percentile(d[m], 70)
    sal = (d > thr) & m
    seen = np.zeros_like(sal, dtype=bool)
    best = 0
    H, W = sal.shape
    for y in range(H):
        for x in range(W):
            if sal[y,x] and not seen[y,x]:
                q = deque([(y,x)]); seen[y,x] = True; n = 0
                while q:
                    cy, cx = q.popleft(); n += 1
                    for dy, dx in ((1,0),(-1,0),(0,1),(0,-1)):
                        ny, nx = cy+dy, cx+dx
                        if 0 <= ny < H and 0 <= nx < W and sal[ny,nx] and not seen[ny,nx]:
                            seen[ny,nx] = True; q.append((ny,nx))
                best = max(best, n)
    return best / float(H*W)

def load(path):
    im = Image.open(path).convert("RGBA")
    if im.width > 640:
        r = 640/im.width
        im = im.resize((640, max(1,int(im.height*r))), Image.LANCZOS)
    a = np.asarray(im)[...,3]
    return im, (Image.fromarray(a) if a.min() < 200 else None)

groups = {}
for game in sorted(os.listdir(COMM)):
    d = os.path.join(COMM, game)
    if os.path.isdir(d):
        groups["商业:" + game] = [os.path.join(d,f) for f in sorted(os.listdir(d)) if not f.startswith("FULL_")]
for sub, lab in [("Hero/1","1费"),("Hero/3","3费"),("Hero/5","5费"),("Special","Special"),("ChosenOne","ChosenOne")]:
    groups["我们:" + lab] = sorted(glob.glob(os.path.join(LIB, sub.replace("/", os.sep), "*.png")))

print("%-20s %4s %10s %10s" % ("group","n","subject占幅中位","四分位"))
for g, ps in groups.items():
    vals = []
    for p in ps:
        try:
            im, al = load(p)
            s = subject_share(im, al)
            if s == s: vals.append(s)
        except Exception: pass
    if not vals: continue
    vals.sort()
    q1 = vals[len(vals)//4]; q3 = vals[3*len(vals)//4]
    print("%-20s %4d %10.3f   %.2f–%.2f" % (g, len(vals), st.median(vals), q1, q3))
