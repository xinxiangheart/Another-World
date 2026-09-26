# -*- coding: utf-8 -*-
"""同一「脸尺度」下对比：把每张图裁到脸部窗口，再把脸统一到 110px 高。
   -> 服装 / 特效 / 细节 / 头发 的差距在同一比例下才可比。"""
import os, glob, statistics as st
from collections import deque
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

BASE = r"C:\Users\22589\.codex\visualizations\2026\09\26\01a0db8f-c2df-7563-b487-acff1c7f0cec"
COMM = os.path.join(BASE, "cardref")
SGS  = os.path.join(BASE, "sgs")
REFS = os.path.join(BASE, "body_ref")
LIB  = r"Assets\_Game\Resources\Cards\Summon"
OUT  = BASE

def load(path):
    im = Image.open(path).convert("RGBA")
    if im.width > 900:
        r = 900.0/im.width
        im = im.resize((900, max(1,int(im.height*r))), Image.LANCZOS)
    return im

def skin_blob(im):
    a = np.asarray(im.convert("RGB"), dtype=np.float32)
    r,g,b = a[...,0],a[...,1],a[...,2]
    mx = a.max(2); mn = a.min(2); d = np.maximum(mx-mn,1e-6)
    L = 0.2126*r+0.7152*g+0.0722*b
    S = np.where(mx>0,(mx-mn)/np.maximum(mx,1e-6),0)
    h = np.zeros_like(mx); m=(mx==r); h[m]=(60*((g-b)/d)%360)[m]
    m=(mx==g); h[m]=(60*((b-r)/d)+120)[m]; m=(mx==b); h[m]=(60*((r-g)/d)+240)[m]
    skin = (h>=5)&(h<=45)&(S>=0.12)&(S<=0.80)&(L>=100)
    if im.mode=="RGBA":
        skin &= (np.asarray(im)[...,3] > 128)
    H,W = skin.shape
    seen = np.zeros_like(skin,bool); best=None; bestn=0
    ys,xs = np.nonzero(skin)
    for y0,x0 in zip(ys,xs):
        if seen[y0,x0] or not skin[y0,x0]: continue
        q=deque([(y0,x0)]); seen[y0,x0]=True; n=0; yy=[y0]; xx=[x0]
        while q:
            cy,cx=q.popleft(); n+=1
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    ny,nx=cy+dy,cx+dx
                    if 0<=ny<H and 0<=nx<W and skin[ny,nx] and not seen[ny,nx]:
                        seen[ny,nx]=True; q.append((ny,nx)); yy.append(ny); xx.append(nx)
        if n>bestn: bestn=n; best=(min(yy),max(yy),min(xx),max(xx))
    return best if bestn>=120 else None

def face_window(im):
    box = skin_blob(im)
    W,H = im.size
    if box is None:
        return None, None
    y0,y1,x0,x1 = box
    fh = max(8, y1-y0+1); fc = ((y0+y1)//2, (x0+x1)//2)
    half = int(fh*1.9)
    l = max(0, fc[1]-half); r = min(W, fc[1]+half)
    t = max(0, fc[0]-int(fh*1.15)); b = min(H, fc[0]+int(fh*2.6))
    if r-l < 24 or b-t < 24: return None, None
    win = im.crop((l,t,r,b))
    scale = 110.0/fh
    win = win.resize((max(1,int(win.width*scale)), max(1,int(win.height*scale))), Image.LANCZOS)
    return win, fh

def metrics(img):
    a = np.asarray(img.convert("RGB"), dtype=np.float32)
    L = 0.2126*a[...,0]+0.7152*a[...,1]+0.0722*a[...,2]
    hp = np.asarray(Image.fromarray(L.astype(np.uint8)).filter(ImageFilter.FIND_EDGES), dtype=np.float32)
    detail = float(hp.mean()); 
    mx=a.max(2); mn=a.min(2); S=np.where(mx>0,(mx-mn)/np.maximum(mx,1e-6),0)
    satHI = float(((S>0.5)&(mx>90)).mean()*100)
    q = np.quantile(L,[0.05,0.25,0.5,0.75,0.95])
    return detail, satHI, float(q[4]-q[0])

ours = [("01504", r"Hero\5\SummonCard_{01504}.png"), ("01531", r"Hero\5\SummonCard_{01531}.png"),
        ("01124", r"Hero\1\SummonCard_{01124}.png"), ("01103", r"Hero\1\SummonCard_{01103}.png"),
        ("01112", r"Hero\1\SummonCard_{01112}.png")]
comm = []
for g in ["hearthstone","mtg","runeterra","yugioh"]:
    fs = sorted([p for p in glob.glob(os.path.join(COMM,g,"*")) if "sheet" not in os.path.basename(p)])
    comm += [(g[:4]+"/"+os.path.basename(p)[:14], p) for p in fs[:3]]
refs = [("ark/"+os.path.basename(p)[:8], p) for p in sorted(glob.glob(os.path.join(REFS,"*.png")))[:3]]
sgs  = [("sgs/"+os.path.basename(p)[:-4], p) for p in sorted(glob.glob(os.path.join(SGS,"*.png")))[:3]]

rows = []
for tag, items in (("OURS", [(n, os.path.join(LIB,p)) for n,p in ours]), ("COMMERCIAL", comm), ("ARKNIGHTS-REF", refs), ("SANGUOSHA", sgs)):
    for name, path in items:
        im = load(path)
        win, fh = face_window(im)
        if win is None:
            print("no face:", name); continue
        d,s,v = metrics(win)
        rows.append((tag, name, win, d, s, v, fh))
        print("%-14s %-18s faceH@orig=%-4d  detail=%-6.1f satHI%%=%-5.2f spread=%5.1f" % (tag,name,fh,d,s,v))

# tile
cell_w = 360; cell_h = 400
cols = max(len([r for r in rows if r[0]==t]) for t in set(r[0] for r in rows))
order = ["OURS","COMMERCIAL","ARKNIGHTS-REF","SANGUOSHA"]
W = cols*(cell_w+10)+10; H = len(order)*(cell_h+34)+10
sheet = Image.new("RGB",(W,H),(12,12,14)); dr = ImageDraw.Draw(sheet)
for ri,t in enumerate(order):
    y = 10+ri*(cell_h+34)
    dr.text((14, y+8), t, fill=(255,214,120))
    i = 0
    for tag,name,win,d,s,v,fh in rows:
        if tag != t: continue
        win = win.resize((cell_w, cell_h), Image.LANCZOS)
        x = 10+i*(cell_w+10)
        sheet.paste(win,(x,y+30))
        dr.text((x+4, y+cell_h+32-2), "%s d%.0f s%.1f%%" % (name[:16], d, s), fill=(200,200,200))
        i += 1
sheet.save(os.path.join(OUT,"face_scale_compare.png"))
print("saved face_scale_compare.png", sheet.size)
