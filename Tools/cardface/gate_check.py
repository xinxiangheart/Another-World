import argparse, glob, math, os, sys

try:
    import numpy as np
    from PIL import Image
except Exception as exc:  # pragma: no cover
    sys.exit("need numpy + pillow: %s" % exc)

# 门槛（AGENTS.md「辨识度硬指标」）—— 只约束 5 费，1-3 费仅参考
SPREAD_MIN   = 80.0    # 主体内部明度跨度 p10->p90
P90_MIN      = 140.0   # 主体最亮块
SAT_MAX      = 0.15    # 高饱和像素占比（sat>0.5 且 max>90）
BAND_PX      = 46      # 四角 + 内缩边带必须透明
CORE_ALPHA   = 200     # 「主体内部」的 alpha 口径
MASK_ALPHA   = 32      # 剪影口径
# 第 4 道门（两两撞脸）
D_L_MIN      = 15.0    # 明度中位差 < 15
D_HUE_MIN    = 20.0    # 主色相角差 < 20 度
IOU_MAX      = 0.55    # 剪影 IoU > 0.55
FRINGE_MIN   = 30      # min(R,B)-G 超过它算洋红残边


def luminance(rgb):
    return 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]


def rgb_to_hsv(rgb):
    r, g, b = rgb[..., 0] / 255.0, rgb[..., 1] / 255.0, rgb[..., 2] / 255.0
    mx = np.max(rgb, axis=-1) / 255.0
    mn = np.min(rgb, axis=-1) / 255.0
    diff = mx - mn
    sat = np.where(mx > 0, diff / np.maximum(mx, 1e-6), 0.0)
    hue = np.zeros_like(mx)
    mask = diff > 1e-6
    idx = mask & (mx == r)
    hue[idx] = ((g[idx] - b[idx]) / diff[idx]) % 6.0
    idx = mask & (mx == g)
    hue[idx] = ((b[idx] - r[idx]) / diff[idx]) + 2.0
    idx = mask & (mx == b)
    hue[idx] = ((r[idx] - g[idx]) / diff[idx]) + 4.0
    return hue * 60.0, sat, mx


def key_from_corners(rgb, k):
    """四角平均色 = 键色（AGENTS.md：键色必须每张图重新取样）。"""
    pad = max(4, k // 200)
    corners = np.concatenate([
        rgb[:pad, :pad].reshape(-1, 3), rgb[:pad, -pad:].reshape(-1, 3),
        rgb[-pad:, :pad].reshape(-1, 3), rgb[-pad:, -pad:].reshape(-1, 3)])
    return corners.mean(axis=0)


def analyse(path):
    im = Image.open(path)
    has_alpha = im.mode in ("RGBA", "LA") or "transparency" in im.info
    im = im.convert("RGBA")
    a = np.asarray(im)
    rgb = a[..., :3].astype(np.float64)
    h, w = rgb.shape[:2]
    out = {"path": path, "size": (w, h), "warnings": []}

    if has_alpha and (a[..., 3] < 250).any():
        alpha = a[..., 3]
    else:
        # 没 alpha（生成器直出的洋红底）：按四角键色推定背景，与抠图口径一致
        key = key_from_corners(rgb, min(h, w))
        dist = np.sqrt(((rgb - key) ** 2).sum(axis=-1))
        alpha = np.where(dist <= 60, 0, 255).astype(np.uint8)
        out["keyed_from_corners"] = True
        out["key_rgb"] = tuple(int(v) for v in key)
        out["warnings"].append("无 alpha，按四角键色 %s 推定背景（阈 60）"
                               % (tuple(int(v) for v in key),))

    core = alpha >= CORE_ALPHA
    mask = alpha >= MASK_ALPHA

    if core.sum() < 500:
        out["error"] = "主体太小（core<500px）"
        return out

    lum = luminance(rgb)
    lc = lum[core]
    p10, p90 = np.percentile(lc, 10), np.percentile(lc, 90)
    out["spread"] = float(p90 - p10)
    out["p90"] = float(p90)
    out["p50"] = float(np.percentile(lc, 50))

    hue, sat, mx = rgb_to_hsv(rgb)
    vivid = core & (sat > 0.5) & (mx > 90 / 255.0)
    out["sat_share"] = float(vivid.sum() / core.sum())

    # 主色相：只统计有色的主体像素，按饱和度加权做圆平均
    sel = core & (sat > 0.15)
    if sel.sum() > 200:
        ang = np.deg2rad(hue[sel])
        wgt = sat[sel]
        out["hue"] = float(math.degrees(math.atan2(np.sum(wgt * np.sin(ang)), np.sum(wgt * np.cos(ang)))) % 360)
        out["hue_px"] = int(sel.sum())
    else:
        out["hue"] = None

    ys, xs = np.where(mask)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    bw, bh = (x1 - x0 + 1), (y1 - y0 + 1)
    out["bbox_frac"] = (bw / w, bh / h)
    out["fill"] = float(mask.sum() / (bw * bh))

    # 四角 + 内缩边带
    band_bad = 0
    for name, sl in (("top", (slice(0, BAND_PX), slice(0, w))),
                     ("bottom", (slice(h - BAND_PX, h), slice(0, w))),
                     ("left", (slice(0, h), slice(0, BAND_PX))),
                     ("right", (slice(0, h), slice(w - BAND_PX, w)))):
        n = int((alpha[sl] > 8).sum())
        if n > 0:
            band_bad += n
            out["warnings"].append("%s 边带不透明 %d px" % (name, n))
    out["band_bad"] = band_bad

    # 洋红残边（只在已经抠过图、带 alpha 的成稿上判；未抠图时背景本身就是洋红）
    if out.get("keyed_from_corners"):
        out["fringe"] = -1
    else:
        fr = mask & ((np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1]) > FRINGE_MIN)
        out["fringe"] = int(fr.sum())

    # bbox 归一化剪影（剪影 IoU 用）
    sil = Image.fromarray((mask[y0:y1 + 1, x0:x1 + 1] * 255).astype(np.uint8)).resize((128, 128), Image.BILINEAR)
    out["sil"] = (np.asarray(sil) > 127)
    return out


def verdict(r, tier):
    fails = []
    if r.get("error"):
        return ["ERROR"], fails
    if tier == 5:
        if r["spread"] < SPREAD_MIN: fails.append("明度跨度 %.0f < %.0f" % (r["spread"], SPREAD_MIN))
        if r["p90"] < P90_MIN: fails.append("最亮块 p90 %.0f < %.0f" % (r["p90"], P90_MIN))
        if r["sat_share"] > SAT_MAX: fails.append("高饱和占比 %.3f > %.2f" % (r["sat_share"], SAT_MAX))
    if r["band_bad"] > 0: fails.append("46px 边带不透明")
    if r["fringe"] > 0: fails.append("洋红残边 %d px" % r["fringe"])
    if r.get("keyed_from_corners"): fails.append("还没抠图（洋红底）")
    return (["PASS"] if not fails else ["FAIL"]), fails


def main():
    ap = argparse.ArgumentParser(description="卡面三道门 + 第 4 道门体检")
    ap.add_argument("paths", nargs="*", help="PNG 路径或通配符")
    ap.add_argument("--tier", type=int, default=5, choices=[0, 1, 3, 5],
                    help="费用档；三道门只对 5 费判 PASS/FAIL，1-3 费仅报数")
    ap.add_argument("--pairwise", action="store_true", help="额外跑第 4 道门（两两撞脸）")
    args = ap.parse_args()

    files = []
    for p in args.paths:
        files.extend(sorted(glob.glob(p)) if any(c in p for c in "*?[") else [p])
    files = [f for f in files if f.lower().endswith(".png") and os.path.isfile(f)]
    if not files:
        sys.exit("no PNG input")

    rows = [analyse(f) for f in files]
    head = "%-34s %6s %6s %7s %8s %13s %6s %6s %8s" % (
        "file", "spread", "p90", "p50", "sat%", "hue", "WxH", "fill", "fringe")
    print(head); print("-" * len(head))
    for r in rows:
        if r.get("error"):
            print("%-34s  %s" % (os.path.basename(r["path"])[:34], r["error"])); continue
        v, fails = verdict(r, args.tier)
        mark = ("%d费" % args.tier) + (v[0] if args.tier == 5 else "")
        if r.get("keyed_from_corners"):
            mark += "*"
        print("%-34s %6.0f %6.0f %7.0f %7.1f%% %13s %6s %6.2f %8s  %s%s" % (
            os.path.basename(r["path"])[:34], r["spread"], r["p90"], r["p50"],
            r["sat_share"] * 100,
            ("%.0f" % r["hue"]) if r["hue"] is not None else "-",
            "%dx%d" % r["size"], r["fill"],
            ("n/a" if r["fringe"] < 0 else "%d" % r["fringe"]), mark,
            ("  <- " + "; ".join(fails)) if fails else ""))

    if args.pairwise and len(rows) > 1:
        print("\n第 4 道门（两两，三条同时成立 = 撞脸）")
        for i in range(len(rows)):
            for j in range(i + 1, len(rows)):
                a, b = rows[i], rows[j]
                if a.get("error") or b.get("error"):
                    continue
                dl = abs(a["p50"] - b["p50"])
                dh = None
                if a["hue"] is not None and b["hue"] is not None:
                    dh = abs((a["hue"] - b["hue"] + 180) % 360 - 180)
                inter = np.logical_and(a["sil"], b["sil"]).sum()
                union = np.logical_or(a["sil"], b["sil"]).sum()
                iou = inter / union if union else 0.0
                hits = []
                if dl < D_L_MIN: hits.append("明度差 %.0f<15" % dl)
                if dh is not None and dh < D_HUE_MIN: hits.append("色相差 %.0f<20" % dh)
                if iou > IOU_MAX: hits.append("IoU %.2f>0.55" % iou)
                tag = "撞脸" if len(hits) == 3 else ("警告" if len(hits) == 2 else "ok")
                print("%-30s x %-30s  %s  (%s)" % (
                    os.path.basename(a["path"])[:30], os.path.basename(b["path"])[:30], tag,
                    "; ".join(hits) if hits else "明度差 %.0f / IoU %.2f" % (dl, iou)))


if __name__ == "__main__":
    main()
