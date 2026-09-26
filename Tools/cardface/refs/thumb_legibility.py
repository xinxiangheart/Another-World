# -*- coding: utf-8 -*-
"""Card-size legibility (thumbSD): whole-frame vs subject-only, commercial corpus vs our library."""
import os, glob, statistics as st
import numpy as np
from PIL import Image

BASE = r"C:\Users\22589\.codex\visualizations\2026\09\26\01a0db8f-c2df-7563-b487-acff1c7f0cec"
COMM = os.path.join(BASE, "cardref")
LIB  = r"Assets\_Game\Resources\Cards\Summon"

def measure(path, has_alpha):
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im.resize((96,128), Image.LANCZOS), dtype=np.float32)
    g = 0.2126*a[...,0] + 0.7152*a[...,1] + 0.0722*a[...,2]
    comp = (g*(a[...,3]/255.0) + 32.0*(1-a[...,3]/255.0))
    whole = float(comp.std())
    if has_alpha:
        m = a[...,3] > 32
        sub = float(comp[m].std()) if m.sum() > 200 else float("nan")
    else:
        sub = whole
    return whole, sub

groups = {}
for game in sorted(os.listdir(COMM)):
    d = os.path.join(COMM, game)
    if not os.path.isdir(d): continue
    groups["商业:"+game] = [(p, False) for p in sorted(glob.glob(os.path.join(d, "*"))) if "sheet" not in os.path.basename(p)]
for sub in ["Hero/1","Hero/3","Hero/5","Hero","Special","ChosenOne"]:
    g = sorted(glob.glob(os.path.join(LIB, sub.replace("/", os.sep), "*")))
    if g: groups["我们:"+sub] = [(p, True) for p in g]

print("%-24s %4s  %8s %8s" % ("group","n","whole","subject"))
print("-"*52)
for k,items in groups.items():
    w=[];s=[]
    for p,ha in items:
        try:
            x,y = measure(p,ha)
            w.append(x); s.append(y if y==y else None)
        except Exception as e:
            pass
    ss=[x for x in s if x is not None]
    if not w:
        continue
    print("%-24s %4d  %8.1f %8.1f" % (k, len(w), st.median(w), st.median(ss) if ss else float("nan")))
allc=[v for k,items in groups.items() if k.startswith("商业") for v in [measure(p,ha) for p,ha in items]]
print("-"*52)
print("商业合计 whole %.1f  subject %.1f" % (st.median([x[0] for x in allc]), st.median([x[1] for x in allc])))
