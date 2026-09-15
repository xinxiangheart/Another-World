"""Repaint one eye as flat cel art and delete its eyebrow: nothing else changes.

usage: flat_eye.py <src.png> <dst.png> "<brow poly>" "<eye box>" [opt=value ...]

  brow poly   "x,y x,y x,y ..." polygon covering the eyebrow to delete
  eye box     "x0,y0,x1,y1" rectangle covering the eye to erase and redraw

Options (defaults are the 01521 values; lengths in px):

  inner=x,y        inner corner of the eye                           673,346.5
  outer=x,y        outer corner of the eye                           701.5,339.5
  apex_up=y        apex height of the upper lid arc                  338.5
  apex_lo=y        apex height of the lower rim                      348.0
  lash=mid,tail    upper lid thickness, at the middle / at the tips  4.2,0.9
  band=h           darker tone pressed under the lash                2.2
  gold=R,G,B       flat iris block                                   186,155,94
  dark=R,G,B       darker tone along the iris' upper edge            117,91,44
  line=R,G,B       eyelid stroke                                     15,13,15
  hilite=R,G,B     the single highlight                              240,233,221
  hilite_at=x,f   highlight x, its height as a fraction of the iris 679,0.44
  hilite_r=r       highlight radius                                  1.7
  iris_dx=x        iris origin, shifted from the inner corner        2.5
  iris_dy=y                                                         -0.3
  iris_end=t       where the iris stops along the eye, 0..1;         1.0
                   <1 pushes the iris toward the inner corner so the
                   gaze reads as looking that way (iris_end=0.70 uses
                   about 70% of the aperture, leaving the outer 30%
                   to the socket tone -- a shadow, never a white)
  socket=R,G,B     the aperture tone beyond iris_end                16,15,18
  grow=n           mask dilation, ImageFilter.MaxFilter(n)           5
  span=x0,x1       columns used by the flat fill                     640,712
  ss=n             supersampling factor while drawing                4

Pipeline:

  1. Erase the brow polygon and the eye box, then fill those pixels flat along
     each column, interpolating between the first untouched pixel above and
     below.  Diffusion inpainting is deliberately NOT used: it leaves a grey
     smudge that reads as exactly the soft gradient the flat cel rule forbids.
  2. Draw the eye in the house language -- one thick dark upper lid stroke
     tapering to sharp tips, one flat iris block filling the aperture, a
     darker tone pressed along its upper edge (hard boundary), one hard-edged
     highlight.  No eyebrow, no lashes, no lower lid line, no eye white, no
     pupil.
  3. Composite through the mask, so every pixel outside it stays bit-identical
     to the source -- check that with the printed changed-pixel bbox.

Why this exists: an edit through /v1/images/edits (google/gemini-3-pro-image)
re-renders the whole picture and, on 01521, refused three times to drop the
eyebrow while pushing the face toward realism and moving the head.  When the
ask is "make this face obey the three-stroke rule and change nothing else",
drawing the eye is both faster and exact.

Prints mask size, changed pixels, changed bbox, and warns when the eye
geometry reaches outside the mask (which would clip it).

Requires numpy and Pillow.
"""
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

DEFAULTS = {
    "inner": "673,346.5",
    "outer": "701.5,339.5",
    "apex_up": "338.5",
    "apex_lo": "348.0",
    "lash": "4.2,0.9",
    "band": "2.2",
    "gold": "186,155,94",
    "dark": "117,91,44",
    "line": "15,13,15",
    "hilite": "240,233,221",
    "hilite_at": "679,0.44",
    "hilite_r": "1.7",
    "grow": "5",
    "span": "640,712",
    "ss": "4",
    "iris_dx": "2.5",
    "iris_dy": "-0.3",
    "iris_end": "1.0",
    "socket": "16,15,18",
}

PAIR = ("inner", "outer", "lash", "hilite_at", "span")
TRIPLE = ("gold", "dark", "line", "hilite", "socket")
SCALAR = ("apex_up", "apex_lo", "band", "hilite_r", "iris_dx", "iris_dy", "iris_end")


def numbers(s):
    return [float(v) for v in s.split(",")]


def parse_opt(key, raw):
    if key in PAIR:
        a, b = numbers(raw)
        return (a, b)
    if key in TRIPLE:
        r, g, b = numbers(raw)
        return (int(r), int(g), int(b))
    if key in SCALAR:
        return float(raw)
    if key in ("grow", "ss"):
        return int(raw)
    raise SystemExit(f"unknown option: {key}")


def bez(p0, c, p2, n=120):
    """Sample a quadratic bezier from p0 to p2 with control c."""
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t * t * p2[1])
            for t in (i / n for i in range(n + 1))]


def ctrl_y(apex_y, y0, y1):
    """Control height that puts a quadratic's apex (at t=0.5) on apex_y."""
    return 2 * apex_y - (y0 + y1) / 2


def y_at(pts, xq):
    """Height of the sampled curve pts at the x nearest xq."""
    return min(pts, key=lambda p: abs(p[0] - xq))[1]


def bbox(pts):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return min(xs), min(ys), max(xs), max(ys)


BLUR = 0.7                     # feather on the flat column fill


def draw_eye(img, o, ss):
    """Draw the eye on img at ss-times supersampling.  Return the big image,
    the geometry and the two lid curves (all in source pixels) so the caller
    can check the eye against the mask."""
    W, H = img.size
    big = img.resize((W * ss, H * ss), Image.LANCZOS)
    dr = ImageDraw.Draw(big)

    in_x, in_y = o["inner"]
    oc_x, oc_y = o["outer"]
    mid_x = (in_x + oc_x) / 2
    up_cy = ctrl_y(o["apex_up"], in_y, oc_y)
    lo_cy = ctrl_y(o["apex_lo"], in_y, oc_y)
    up = bez((in_x, in_y), (mid_x, up_cy), (oc_x, oc_y))
    lo = bez((in_x, in_y), (mid_x, lo_cy), (oc_x, oc_y))

    iris_up = bez((in_x + o["iris_dx"], in_y + o["iris_dy"]), (mid_x, up_cy), (oc_x, oc_y))
    iris_lo = bez((in_x + o["iris_dx"], in_y + o["iris_dy"]), (mid_x, lo_cy), (oc_x, oc_y))

    n = len(up)
    i_end = max(0, min(n - 1, int(round(o["iris_end"] * (n - 1)))))

    def cut(pts):
        return pts[:i_end + 1]

    def S(pts):
        return [(x * ss, y * ss) for x, y in pts]

    if i_end < n - 1:
        dr.polygon(S(up[i_end:] + lo[i_end:][::-1]), fill=o["socket"])
    dr.polygon(S(cut(iris_up) + cut(iris_lo)[::-1]), fill=o["gold"])
    dr.polygon(S(cut(iris_up) + [(x, y + o["band"]) for x, y in cut(iris_up)[::-1]]), fill=o["dark"])

    lash_mid, lash_tail = o["lash"]

    def thickness(x):
        t = (x - in_x) / (oc_x - in_x)
        return lash_tail + lash_mid * (t * (1 - t) * 4) ** 0.55

    lash = [(x, y - thickness(x)) for x, y in up]
    dr.polygon(S(up) + S(lash[::-1]), fill=o["line"])

    hx, hf = o["hilite_at"]
    hy = y_at(up, hx) + hf * (y_at(lo, hx) - y_at(up, hx))
    r = o["hilite_r"]
    dr.ellipse([(hx - r) * ss, (hy - r) * ss, (hx + r) * ss, (hy + r) * ss], fill=o["hilite"])

    geom = up + lo + iris_up + lash + [(hx - r, hy - r), (hx + r, hy + r)]
    return big, geom, up, lo


def main(argv):
    if len(argv) < 5:
        raise SystemExit(__doc__.strip())
    src, dst, brow, box = argv[1:5]

    o = dict(DEFAULTS)
    for kv in argv[5:]:
        k, sep, v = kv.partition("=")
        if k not in DEFAULTS or not sep or not v:
            raise SystemExit(f"expected <known option>=<value>, got: {kv}")
        o[k] = v
    o = {k: parse_opt(k, v) for k, v in o.items()}

    flat = numbers(",".join(brow.split()))
    if len(flat) % 2:
        raise SystemExit("brow poly needs coordinates in pairs")
    brow_pts = [(int(flat[i]), int(flat[i + 1])) for i in range(0, len(flat), 2)]
    box_pts = [int(v) for v in numbers(box)]
    if len(box_pts) != 4:
        raise SystemExit("eye box needs x0,y0,x1,y1")

    im = Image.open(src).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    H, W, _ = a.shape

    reg = Image.new("L", im.size, 0)
    d = ImageDraw.Draw(reg)
    d.polygon(brow_pts, fill=255)
    d.rectangle(box_pts, fill=255)
    m = np.asarray(reg.filter(ImageFilter.MaxFilter(o["grow"]))) > 0

    base = a.copy()
    x0, x1 = o["span"]
    for x in range(max(0, int(x0)), min(W, int(x1))):
        col = np.where(m[:, x])[0]
        if len(col) == 0:
            continue
        top, bot = col.min(), col.max()
        c_top = a[top - 1, x] if top - 1 >= 0 else a[bot + 1, x]
        c_bot = a[bot + 1, x] if bot + 1 < H else a[top - 1, x]
        n = bot - top + 1
        t = (np.arange(1, n + 1) / (n + 1)).reshape(-1, 1)
        base[top:bot + 1, x] = c_top * (1 - t) + c_bot * t

    sm = np.asarray(Image.fromarray(np.clip(base, 0, 255).astype(np.uint8))
                    .filter(ImageFilter.GaussianBlur(BLUR))).astype(np.float32)
    base = np.where(m[..., None], sm, a)

    big, geom, up, lo = draw_eye(Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)), o, o["ss"])
    drawn = np.asarray(big.resize((W, H), Image.LANCZOS)).astype(np.float32)

    out = np.clip(np.where(m[..., None], drawn, a), 0, 255).astype(np.uint8)
    Image.fromarray(out).save(dst)

    ys, xs = np.where(m)
    print(f"mask    {int(m.sum())} px, bbox x {xs.min()}-{xs.max()} / y {ys.min()}-{ys.max()}")
    mid_x = (o["inner"][0] + o["outer"][0]) / 2
    print(f"drawn   apex up {y_at(up, mid_x):.1f} / lo {y_at(lo, mid_x):.1f}")
    ex0, ey0, ex1, ey1 = bbox(geom)
    print(f"eye     bbox x {ex0:.1f}-{ex1:.1f} / y {ey0:.1f}-{ey1:.1f}")
    if ex0 < xs.min() or ey0 < ys.min() or ex1 > xs.max() or ey1 > ys.max():
        print("warning eye geometry reaches outside the mask and would be clipped")

    changed = np.any(out.astype(np.int16) != np.asarray(im).astype(np.int16), axis=2)
    cy, cx = np.where(changed)
    if len(cx) == 0:
        print("changed 0 px")
    else:
        print(f"changed {int(changed.sum())} px ({100 * changed.mean():.3f}%), "
              f"bbox x {cx.min()}-{cx.max()} / y {cy.min()}-{cy.max()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
