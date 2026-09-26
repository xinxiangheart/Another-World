# -*- coding: utf-8 -*-
"""四条设计轴对比（同一脸尺度）：每张图按「脸高 = 110px」重采样，并裁成固定 64:84 画窗，
   逐格打印 细节密度 / 高饱和占比 / 明度跨度 —— 服装 · 特效 · 细节 · 头发 的差距
   只有在这个同一比例下才可比。
   行：OURS（现役库） / NEWBASE（2026-09-26 新底盘校准图） / COMMERCIAL / SANGUOSHA
   用法：python Tools/cardface/refs/design_compare.py [输出png]
"""
import os, sys, glob
from collections import deque
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

BASE = os.path.join(os.path.expanduser("~"), ".codex", "visualizations", "2026", "09", "26",
                    "01a0db8f-c2df-7563-b487-acff1c7f0cec")
COMM, SGS, LIB = os.path.join(BASE, "cardref"), os.path.join(BASE, "sgs"), r"Assets\_Game\Resources\Cards\Summon"
NEW  = os.path.join(BASE, "draft_new")
OUT  = sys.argv[1] if len(sys.argv) > 1 else os.path.join(BASE, "design_compare.png")
FACE_H = 110          # 统一后的脸高（px）
WIN_H  = 3.40         # 画窗高 = 脸高 × 3.4  → 约 3–4 头身，与新取景规则同量级
ASPECT = 64/84.0      # 卡面画窗宽高比
CELL_W, CELL_H = 320, 440

def load(path):
    im = Image.open(path).convert("RGBA")
    if im.width > 900:
        r = 900.0/im.width
        im = im.resize((900, max(1, int(im.height*r))), Image.LANCZOS)
    return im

def skin_blob(im):
    a = np.asarray(im.convert("RGB"), dtype=np.float32)
    r, g, b = a[...,0], a[...,1], a[...,2]
    mx, mn = a.max(2), a.min(2); d = np.maximum(mx-mn, 1e-6)
    L = 0.2126*r + 0.7152*g + 0.0722*b
    S = np.where(mx > 0, (mx-mn)/np.maximum(mx, 1e-6), 0)
    h = np.zeros_like(mx); m = (mx == r); h[m] = (60*((g-b)/d) % 360)[m]
    m = (mx == g); h[m] = (60*((b-r)/d) + 120)[m]; m = (mx == b); h[m] = (60*((r-g)/d) + 240)[m]
    skin = (h >= 5) & (h <= 45) & (S >= 0.12) & (S <= 0.80) & (L >= 100)
    if im.mode == "RGBA":
        skin &= (np.asarray(im)[...,3] > 128)
    H, W = skin.shape
    seen = np.zeros_like(skin, bool); best = None; bestn = 0
    for y0, x0 in zip(*np.nonzero(skin)):
        if seen[y0, x0] or not skin[y0, x0]: continue
        q = deque([(y0, x0)]); seen[y0, x0] = True; n = 0; yy = [y0]; xx = [x0]
        while q:
            cy, cx = q.popleft(); n += 1
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = cy+dy, cx+dx
                    if 0 <= ny < H and 0 <= nx < W and skin[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True; q.append((ny, nx)); yy.append(ny); xx.append(nx)
        if n > bestn: bestn = n; best = (min(yy), max(yy), min(xx), max(xx))
    return best if bestn >= 120 else None

def face_window(im):
    """把图重采样成：脸高 = FACE_H，画窗高 = FACE_H×WIN_H，宽高比 = 卡面画窗。"""
    box = skin_blob(im)
    if box is None: return None, None
    y0, y1, x0, x1 = box
    fh = max(8, y1-y0+1)
    sc = FACE_H/float(fh)
    big = im.resize((max(1,int(im.width*sc)), max(1,int(im.height*sc))), Image.LANCZOS)
    cy, cx = int((y0+y1)/2*sc), int((x0+x1)/2*sc)
    H = int(FACE_H*WIN_H); W = int(H*ASPECT)
    t = cy - int(0.62*FACE_H); l = cx - W//2
    win = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    src = big.crop((l, t, l+W, t+H))
    win.paste(src, (0, 0))
    return win, fh

def metrics(img):
    a = np.asarray(img.convert("RGB"), dtype=np.float32)
    al = np.asarray(img)[..., 3] > 128 if img.mode == "RGBA" else np.ones(a.shape[:2], bool)
    L = 0.2126*a[...,0] + 0.7152*a[...,1] + 0.0722*a[...,2]
    hp = np.asarray(Image.fromarray(L.astype(np.uint8)).filter(ImageFilter.FIND_EDGES), dtype=np.float32)
    mx, mn = a.max(2), a.min(2)
    S = np.where(mx > 0, (mx-mn)/np.maximum(mx, 1e-6), 0)
    Lv = L[al]
    q = np.quantile(Lv, [0.10, 0.90])
    return (float(hp[al].mean()), float(((S[al] > 0.5) & (mx[al] > 90)).mean()*100), float(q[1]-q[0]))

OURS = ["01101", "01124", "01323", "01325", "01504", "01525"]
OURS = [(n, os.path.join(LIB, r"Hero\%s\SummonCard_{%s}.png" % (
    {"01101":1, "01124":1, "01323":3, "01325":3, "01504":5, "01525":5}[n], n))) for n in OURS]
NEWB = [("newbase v1", os.path.join(NEW, "v1_knightpriest.png")),
        ("newbase v2", os.path.join(NEW, "v2_slatecoat.png"))]
COMMS = []
for g in ["hearthstone", "mtg", "runeterra", "yugioh"]:
    fs = sorted([p for p in glob.glob(os.path.join(COMM, g, "*")) if "sheet" not in os.path.basename(p)])
    if fs: COMMS.append((g[:4], fs[0]))
SGS = [("sgs/" + os.path.basename(p)[:-4], p) for p in sorted(glob.glob(os.path.join(SGS, "*.png")))[:4]]

rows = []
for tag, items in (("OURS - current library", OURS),
                   ("NEW PROMPT BASE - 2026-09-26 calibration", NEWB),
                   ("COMMERCIAL", COMMS), ("SANGUOSHA", SGS)):
    for name, path in items:
        win, fh = face_window(load(path))
        if win is None:
            print("no face:", name); continue
        d, s, v = metrics(win)
        rows.append((tag, name, win, d, s, v, fh))
        print("%-42s %-20s faceH@orig=%-4d detail=%-6.1f satHI%%=%-5.2f span=%5.1f" % (tag, name, fh, d, s, v))

tags = list(dict.fromkeys(r[0] for r in rows))
cols = max(sum(1 for r in rows if r[0] == t) for t in tags)
sheet = Image.new("RGB", (cols*CELL_W, len(tags)*(CELL_H+34)+16), (13, 20, 31))
dr = ImageDraw.Draw(sheet)
y = 8
for t in tags:
    dr.text((12, y), t, fill=(232, 203, 132)); y += 30
    x = 0
    for _, name, win, d, s, v, fh in [r for r in rows if r[0] == t]:
        cell = Image.new("RGB", (CELL_W, CELL_H), (13, 20, 31))
        im = win
        if im.width > CELL_W or im.height > CELL_H-46:
            k = min(CELL_W/float(im.width), (CELL_H-46)/float(im.height))
            im = im.resize((max(1,int(im.width*k)), max(1,int(im.height*k))), Image.LANCZOS)
        cell.paste(im, ((CELL_W-im.width)//2, 28+(CELL_H-46-im.height)//2), im)
        sheet.paste(cell, (x, y))
        dr = ImageDraw.Draw(sheet)
        dr.text((x+6, y+5), name, fill=(195, 205, 220))
        dr.text((x+6, y+CELL_H-32), "detail %.1f" % d, fill=(150, 165, 185))
        dr.text((x+6, y+CELL_H-20), "satHI %.1f%%   span %.0f" % (s, v), fill=(150, 165, 185))
        x += CELL_W
    y += CELL_H + 34
sheet.save(OUT)
print("saved", OUT)