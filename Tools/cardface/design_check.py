# -*- coding: utf-8 -*-
"""设计轴体检：服装/材质 · 配色 · 细节密度 · 特效（高饱和+最亮块）。
   门槛取自商业 96 张的实测带（AGENTS.md「商业化卡面基准（召唤物）」→「四条设计轴」）。
   与 refs/baseline_metrics.py 同一套口径，可直接对照。
   用法：python Tools/cardface/design_check.py [--all] 卡片PNG..."""
import argparse, glob, os, sys
try:
    import numpy as np
    from PIL import Image, ImageFilter
except Exception as exc:
    sys.exit("need numpy + pillow: %s" % exc)

# —— 门槛（商业 96 张实测带）——
# 门槛取自两边实测的分布（商业 96 的 p25 与 我方的 p75 之间；2026-09-26 定）
VSTEP_MIN  = 6       # 明度阶梯数（服装/材质分级）  商业 p25 6  p50 8  |  我方 p50 4  p75 5
HUEN_MIN   = 4       # 色相簇数（配色成关系）      商业 p25 4  p50 5  |  我方 p50 3  p75 5
DETAIL_MIN = 10.0    # 细节密度（high-pass 能量）  商业 p25 10 p50 13 |  我方 p50 7  p75 10.5
SAT_MIN    = 0.04    # 高饱和像素占比（特效/点缀）商业 p25 5.2% p50 11.9% | 我方 p50 0.2% p75 3.0%
SAT_MAX    = 0.30    #   上限（旧门 0.15 会误杀 YGO 20.6% 与 HS 19.8%）
BRIGHT_MIN = 0.03    # 近白最亮块（L>200）         商业 p25 3.2% p50 8.8% | 我方 p50 0.1% p75 2.4%
DARK_MAX   = 0.45    # 近黑占比（L<45）的上限      商业 p75 35.1%       | 我方 p75 59.8%
MAX_BAD    = 1       # 6 项里最多允许 1 项不达标

def metrics(path):
    im = Image.open(path).convert("RGBA")
    if im.width > 900:
        r = 900.0/im.width
        im = im.resize((900, max(1,int(im.height*r))), Image.LANCZOS)
    a = np.asarray(im, dtype=np.float32)
    rgb, alpha = a[..., :3], a[..., 3]
    r_, g_, b_ = rgb[...,0], rgb[...,1], rgb[...,2]
    mx, mn = rgb.max(2), rgb.min(2)
    L = 0.2126*r_ + 0.7152*g_ + 0.0722*b_
    S = np.where(mx > 0, (mx-mn)/np.maximum(mx,1e-6), 0.0)
    d = np.maximum(mx-mn, 1e-6)
    h = np.zeros_like(mx)
    m = (mx == r_); h[m] = (60*((g_-b_)/d) % 360)[m]
    m = (mx == g_); h[m] = (60*((b_-r_)/d) + 120)[m]
    m = (mx == b_); h[m] = (60*((r_-g_)/d) + 240)[m]
    mask = alpha > 128
    Lv, Sv, hv = L[mask], S[mask], h[mask]
    if Lv.size < 500:
        return None
    o = {}
    hist, _ = np.histogram(Lv, bins=16, range=(0,256))
    o["vstep"]  = int(((hist/len(Lv)) >= 0.05).sum())
    sm = Sv > 0.25
    o["hueN"]   = int((((np.histogram(hv[sm], bins=36, range=(0,360))[0]/max(1,sm.sum())) >= 0.05).sum())) if sm.sum() > 50 else 0
    lo = np.asarray(im.convert("L"), dtype=np.float32)
    blur = np.asarray(im.convert("L").filter(ImageFilter.GaussianBlur(6)), dtype=np.float32)
    o["detail"] = float(np.abs(lo-blur)[mask].mean())
    o["satHI"]  = float(((Sv > 0.5) & (Lv > 90)).mean())
    o["bright"] = float((Lv > 200).mean())
    o["dark"]   = float((Lv < 45).mean())
    return o

def verdict(o):
    bad = []
    if o["vstep"]  < VSTEP_MIN:  bad.append("明度阶梯 %d<%d" % (o["vstep"], VSTEP_MIN))
    if o["hueN"]   < HUEN_MIN:   bad.append("色相簇 %d<%d" % (o["hueN"], HUEN_MIN))
    if o["detail"] < DETAIL_MIN: bad.append("细节 %.1f<%.1f" % (o["detail"], DETAIL_MIN))
    if o["satHI"]  < SAT_MIN:    bad.append("高饱和 %.1f%%<%.1f%%" % (o["satHI"]*100, SAT_MIN*100))
    if o["satHI"]  > SAT_MAX:    bad.append("高饱和 %.1f%%>%.1f%%" % (o["satHI"]*100, SAT_MAX*100))
    if o["bright"] < BRIGHT_MIN: bad.append("最亮块 %.1f%%<%.1f%%" % (o["bright"]*100, BRIGHT_MIN*100))
    if o["dark"]   > DARK_MAX:   bad.append("近黑 %.1f%%>%.1f%%" % (o["dark"]*100, DARK_MAX*100))
    return bad

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="*")
    ap.add_argument("--all", action="store_true", help="扫全库 Assets/_Game/Resources/Cards/Summon")
    ap.add_argument("--corpus", choices=["comm","sgs","refs"], help="扫参考件语料（visualizations/2026/09/26/01a0db8f-.../）")
    a = ap.parse_args()
    files = list(a.files)
    if a.all:
        for sub in ["Hero/1","Hero/3","Hero/5","Special","ChosenOne"]:
            files += sorted(glob.glob(os.path.join("Assets/_Game/Resources/Cards/Summon", sub, "*.png")))
    if a.corpus:
        base = os.path.join(os.path.expanduser("~"), ".codex", "visualizations", "2026", "09", "26",
                            "01a0db8f-c2df-7563-b487-acff1c7f0cec")
        sub = {"comm": "cardref", "sgs": "sgs", "refs": "body_ref"}[a.corpus]
        for ext in ("*.png", "*.jpg"):
            files += [p for p in sorted(glob.glob(os.path.join(base, sub, "**", ext), recursive=True))
                      if not os.path.basename(p).startswith("sheet")]
    if not files:
        ap.error("no input")
    print("%-34s %6s %5s %7s %7s %7s %7s   %s" % ("file","vstep","hueN","detail","satHI","bright","dark","判"))
    print("-"*104)
    fails = 0
    for p in files:
        try: o = metrics(p)
        except Exception as e: print("%-34s  ERROR %s" % (os.path.basename(p)[:34], e)); continue
        if o is None: print("%-34s  (alpha 太小/空图)" % os.path.basename(p)[:34]); continue
        bad = verdict(o)
        hard = len(bad) > MAX_BAD
        fails += 1 if hard else 0
        print("%-34s %6d %5d %7.1f %6.1f%% %6.1f%% %6.1f%%   %s" % (
            os.path.basename(p)[:34], o["vstep"], o["hueN"], o["detail"],
            o["satHI"]*100, o["bright"]*100, o["dark"]*100,
            "OK" if not bad else ("警: " if not hard else "退: ") + "; ".join(bad)))
    print("-"*104)
    print("%d 张里 %d 张判退" % (len(files), fails))

if __name__ == "__main__":
    main()
