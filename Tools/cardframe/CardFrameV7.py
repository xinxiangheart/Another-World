# -*- coding: utf-8 -*-
"""
卡牌预制体 v7 —— 组件分离 / 贴合棋盘底色 / 扁平（不做多层堆叠）

生成：Assets/_Game/Art/Sprites/Generated/cardframe-v7/*.png
预览：Tools/cardframe/preview/cardframe-v7-{components,card,onscreen}.png
用法：仓库根目录下  python Tools/cardframe/CardFrameV7.py

设计口径（对齐 Assets/_Game/Art/Sprites/Generated/board-layers-v2/）
  底色  卡身 #121B27 → #090E16（竖直渐变，与 Board_Plate 同族）
  金线  #C9AF65（同 Board_Ornament 最亮金），只做「一条线」，不加辉光/倒角/投影
  层数  5 层分离组件：卡身 / 金线框 / 名牌 / 画窗 / 数值条 —— 全部平贴，互不叠体
  画布  768x1344（与现役 SummonCard_* 同规格、同 PPU 100，可整层替换）
"""
import os, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageChops, ImageEnhance

def _root():
    d = os.getcwd()
    if os.path.isdir(os.path.join(d, 'Assets', '_Game')):
        return d
    q = os.path.dirname(os.path.abspath(__file__))
    while q and q != os.path.dirname(q):
        if os.path.isdir(os.path.join(q, 'Assets', '_Game')):
            return q
        q = os.path.dirname(q)
    return os.getcwd()


ROOT = _root()
os.chdir(ROOT)

SS = 3
W, H = 768, 1344
OUT = r"Assets/_Game/Art/Sprites/Generated/cardframe-v7"
PREVIEW = r"Tools/cardframe/preview"
FON = r"Assets/_Game/Fonts/NotoSerifCJKsc-Bold.otf"
ART = r"Assets/_Game/Resources/Cards/Summon/Hero/5/SummonCard_{01504}.png"

INK     = (7, 11, 18)
BODY_T  = (18, 27, 39)
BODY_B  = (9, 14, 22)
PLATE_T = (25, 36, 50)
PLATE_B = (15, 22, 32)
GOLD    = (201, 175, 101)
GOLD_L  = (236, 219, 160)
GOLD_D  = (116, 95, 46)
STEEL   = (158, 184, 212)
STEEL_D = (92, 116, 146)
BLOOD   = (193, 63, 66)
BLOOD_D = (133, 35, 43)
IVORY   = (238, 228, 196)

TIERS = [("0", (146, 154, 164)), ("1", (226, 223, 212)), ("2", (86, 176, 104)),
         ("3", (86, 140, 214)), ("4", (152, 108, 208)), ("5", (226, 186, 86))]

INSET, R_OUT = 6, 44
INSET2, R_IN = 26, 30
NP, NP_R     = (40, 40, 728, 186), 18
AW, AW_R     = (88, 268, 681, 1043), 14          # 召唤：对齐 ArtworkArea x89..679 y269.6..1041.2
AW_S, AW_R_S = (88, 268, 681, 856), 14           # 法术：对齐 ArtworkArea 64x64 @ y+12
SP, SP_R     = (40, 1055, 728, 1306), 18         # 召唤：生命/攻击数值条
SP_S, SP_R_S = (40, 872, 728, 1306), 18          # 法术：EffectText 说明条
COST_C, TYPE_C = (82, 113), (666, 113)
HP_C, ATK_C    = (176, 1174), (592, 1174)
NAME_X0, NAME_X1 = 160, 600


def col(rgb, a=255):
    return (int(rgb[0]), int(rgb[1]), int(rgb[2]), int(a))


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def layer(w=W, h=H):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def px4(box):
    return [box[0] * SS, box[1] * SS, box[2] * SS, box[3] * SS]


def rrect(lay, box, rad, fill=None, outline=None, width=0):
    ImageDraw.Draw(lay).rounded_rectangle(px4(box), radius=rad * SS, fill=fill,
                                          outline=outline, width=int(round(width * SS)))


def vgrad(lay, box, rad, top, bottom):
    w, h = lay.size
    ys = np.clip((np.arange(h, dtype=np.float32) / SS - box[1]) / max(1.0, (box[3] - box[1])), 0, 1)[:, None]
    t = np.array(top, dtype=np.float32); b = np.array(bottom, dtype=np.float32)
    arr = np.repeat((t[None, :] * (1 - ys) + b[None, :] * ys)[:, None, :], w, axis=1).astype(np.uint8)
    arr = np.concatenate([arr, np.full((h, w, 1), 255, np.uint8)], axis=2)
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle(px4(box), radius=rad * SS, fill=255)
    lay.paste(Image.fromarray(arr, "RGBA"), (0, 0), m)


def dot(lay, cx, cy, r, color):
    ImageDraw.Draw(lay).ellipse([(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS], fill=color)


def diamond(lay, cx, cy, hx, hy, fill=None, outline=None, width=0):
    P = [(cx * SS, (cy - hy) * SS), ((cx + hx) * SS, cy * SS), (cx * SS, (cy + hy) * SS), ((cx - hx) * SS, cy * SS)]
    d = ImageDraw.Draw(lay)
    if fill is not None: d.polygon(P, fill=fill)
    if outline is not None and width > 0: d.line(P + [P[0]], fill=outline, width=int(round(width * SS)), joint="curve")


def ngon(cx, cy, r, n, rot=-90.0):
    return [(cx + r * math.cos(math.radians(rot + i * 360.0 / n)),
             cy + r * math.sin(math.radians(rot + i * 360.0 / n))) for i in range(n)]


def save(lay, name, w=W, h=H):
    os.makedirs(OUT, exist_ok=True)
    lay.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, name))
    print("   ", name)


# 边带（费用色带）—— 现役整框靠 34px 实心色带区分费用，这里保留「一条实色带」但压到 22px 且不做倒角
BAND = 22.0


def _edge_layer(tone, name):
    """tone=None → 纯白（运行时乘色）；否则直接上色"""
    t = (255, 255, 255) if tone is None else tone
    L = layer()
    mid = INSET + BAND / 2.0
    rrect(L, (mid, mid, W - mid, H - mid), R_OUT - BAND / 2.0, outline=col(t, 255), width=BAND)
    rrect(L, (INSET + BAND + 0.75, INSET + BAND + 0.75, W - INSET - BAND - 0.75, H - INSET - BAND - 0.75),
          R_OUT - BAND - 1, outline=col(INK, 190), width=1.5)
    rrect(L, (INSET2 + 14, INSET2 + 14, W - INSET2 - 14, H - INSET2 - 14), R_IN - 8,
          outline=col(t, 75), width=1.6)
    for cx, cy in ((mid + 2, mid + 2), (W - mid - 2, mid + 2), (mid + 2, H - mid - 2), (W - mid - 2, H - mid - 2)):
        dot(L, cx, cy, 5, col(mix(t, (255, 255, 255), 0.55) if tone else (255, 255, 255), 235))
    save(L, name)


# ══════════════════════════ 卡框 5 层 + 卡背 ══════════════════════════
def _hole(lay, box, rad):
    """在卡身上挖出画窗透明洞 —— 预制体是「卡框节点盖在原画之上」，
    画窗必须透明原画才透得出来（现役 SummonCard 的洞实测 y 271..1039）。"""
    ImageDraw.Draw(lay).rounded_rectangle(px4((box[0] - 3, box[1] - 3, box[2] + 3, box[3] + 3)),
                                          radius=int(rad * SS), fill=(0, 0, 0, 0))


def build_frame():
    for box, rad, nm in ((AW, AW_R, "Card_Body.png"), (AW_S, AW_R_S, "Card_Body_Spell.png")):
        L = layer(); vgrad(L, (INSET, INSET, W - INSET, H - INSET), R_OUT, BODY_T, BODY_B)
        _hole(L, box, rad)
        save(L, nm)

    _edge_layer(GOLD, "Card_Edge.png")

    L = layer(); vgrad(L, NP, NP_R, PLATE_T, PLATE_B)
    rrect(L, NP, NP_R, outline=col(GOLD, 200), width=2.2)
    diamond(L, 22, 113, 9, 13, fill=col(GOLD, 220)); diamond(L, W - 22, 113, 9, 13, fill=col(GOLD, 220))
    save(L, "Card_NamePlate.png")

    for box, rad, nm in ((AW, AW_R, "Card_ArtWindow.png"), (AW_S, AW_R_S, "Card_ArtWindow_Spell.png")):
        L = layer()
        rrect(L, box, rad, outline=col(GOLD, 175), width=2.2)
        rrect(L, (box[0] + 3, box[1] + 3, box[2] - 3, box[3] - 3), rad - 3, outline=col(INK, 165), width=1.2)
        save(L, nm)

    for box, rad, nm, mid in ((SP, SP_R, "Card_StatPlate.png", True), (SP_S, SP_R_S, "Card_StatPlate_Spell.png", False)):
        L = layer(); vgrad(L, box, rad, PLATE_T, PLATE_B)
        rrect(L, box, rad, outline=col(GOLD, 200), width=2.2)
        cy = (box[1] + box[3]) / 2.0
        diamond(L, 22, cy, 9, 13, fill=col(GOLD, 220)); diamond(L, W - 22, cy, 9, 13, fill=col(GOLD, 220))
        if mid:
            rrect(L, (383.25, box[1] + 24, 384.75, box[3] - 24), 0.6, fill=col(GOLD, 70))
        save(L, nm)

    L = layer(); vgrad(L, (INSET, INSET, W - INSET, H - INSET), R_OUT, BODY_T, BODY_B)
    rrect(L, (INSET, INSET, W - INSET, H - INSET), R_OUT, outline=col(GOLD, 235), width=3.0)
    rrect(L, (INSET2, INSET2, W - INSET2, H - INSET2), R_IN, outline=col(GOLD, 80), width=1.6)
    for cx, cy in ((30, 30), (W - 30, 30), (30, H - 30), (W - 30, H - 30)):
        dot(L, cx, cy, 4.5, col(GOLD, 215))
    CX, CY = W / 2.0, H / 2.0
    for rot in (0.0, 180.0):
        diamond(L, CX, CY + (170 * (1 if rot else -1)) * 0 + 0, 0, 0)
    # 六芒星 = 两个三角
    for rot in (0.0, 180.0):
        tri = [(CX + 196 * math.cos(math.radians(rot - 90 + i * 120)),
                CY + 196 * math.sin(math.radians(rot - 90 + i * 120))) for i in range(3)]
        ImageDraw.Draw(L).line([(x * SS, y * SS) for x, y in tri] + [(tri[0][0] * SS, tri[0][1] * SS)],
                               fill=col(GOLD, 150), width=int(2.4 * SS), joint="curve")
    diamond(L, CX, CY, 92, 92, outline=col(GOLD, 120), width=2.0)
    dot(L, CX, CY, 8, col(GOLD, 200))
    for i in range(16):
        a = math.radians(i * 22.5 - 90)
        dot(L, CX + 272 * math.cos(a), CY + 272 * math.sin(a), 3.4, col(GOLD, 95))
    save(L, "Card_Back.png")

    # 按费用分色的金线框（组件分离后可直接换色，替代旧的 SummonCard_0..5 整体换框）
    for tag, t in TIERS:
        _edge_layer(t, "Card_Edge_%s.png" % tag)
    _edge_layer(None, "Card_Edge_Tint.png")      # 纯白版，给运行时 Image.color 乘色用


# ══════════════════════════ 角标 ══════════════════════════
N, S = 256, 256 * SS
BODY, BED = (21, 31, 44), (9, 14, 21)


def _s(v):
    return int(round(v * SS))


def _lp():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def _solid(c):
    return Image.new("RGBA", (S, S), c)


def _sp(pts):
    return [(_s(x), _s(y)) for x, y in pts]


def _scale(pts, k, c):
    return [(c[0] + (x - c[0]) * k, c[1] + (y - c[1]) * k) for x, y in pts]


def _bx(x0, y0, x1, y1):
    return [_s(x0), _s(y0), _s(x1), _s(y1)]


def _mc(cx, cy, r):
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).ellipse([_s(cx - r), _s(cy - r), _s(cx + r), _s(cy + r)], fill=255); return m


def _paste(lay, img, m):
    lay.alpha_composite(Image.composite(img, Image.new("RGBA", img.size, (0, 0, 0, 0)), m))


def _bsave(lay, name, size=511):
    """现役图标一律 511x511（Cost 250x250）—— CardIcons3D.SetFixedSize 按 sprite.bounds.x
    归一，像素尺寸不等就会在屏上变大变小，所以必须跟原图一致。"""
    os.makedirs(OUT, exist_ok=True)
    lay.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, name)); print("   ", name)


def _socket_circle(lay, R, line, fill):
    _paste(lay, _solid(col(line)), _mc(128, 128, R))
    _paste(lay, _solid(col(fill)), _mc(128, 128, R - 6.5))
    _paste(lay, _solid(col(BED, 255)), _mc(128, 128, R - 20))


def _socket_hex(lay, R, line, fill, lw=7.0):
    pts = ngon(128, 128, R, 6, -90)
    d = ImageDraw.Draw(lay)
    d.polygon(_sp(pts), fill=col(fill))
    d.polygon(_sp(_scale(pts, 0.70, (128, 128))), fill=col(BED, 255))
    d.line(_sp(pts) + [_sp(pts)[0]], fill=col(line), width=_s(lw), joint="curve")


def _socket_diamond(lay, R, line, fill, lw=7.0):
    pts = [(128, 128 - R), (128 + R, 128), (128, 128 + R), (128 - R, 128)]
    d = ImageDraw.Draw(lay)
    d.polygon(_sp(pts), fill=col(fill))
    d.polygon(_sp(_scale(pts, 0.72, (128, 128))), fill=col(BED, 255))
    d.line(_sp(pts) + [_sp(pts)[0]], fill=col(line), width=_s(lw), joint="curve")


def build_badges():
    def cost(tint, name):
        L = _lp()
        _socket_hex(L, 104, tint if tint else GOLD, mix(BED, tint, 0.18) if tint else BODY)
        _bsave(L, name, 250)
    cost(None, "Badge_Cost.png")
    for tag, t in TIERS:
        cost(t, "Badge_Cost_%s.png" % tag)

    K = 260; O = K // 2
    sw = Image.new("RGBA", (K * SS, K * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(sw)
    P = lambda p: [((O + x) * SS, (O + y) * SS) for x, y in p]
    d.polygon(P([(0, -92), (8, -70), (8, 16), (0, 26), (-8, 16), (-8, -70)]), fill=col(STEEL))
    d.polygon(P([(0, -92), (8, -70), (8, 16), (0, 26)]), fill=col(STEEL_D))
    d.polygon(P([(-25, 16), (25, 16), (25, 25), (-25, 25)]), fill=col(GOLD))
    d.polygon(P([(0, 16), (25, 16), (25, 25), (0, 25)]), fill=col(GOLD_D))
    d.polygon(P([(-5, 25), (5, 25), (5, 54), (-5, 54)]), fill=col(STEEL_D))
    d.ellipse(P([(-8, 54), (8, 70)]), fill=col(GOLD))
    L = _lp(); _socket_circle(L, 104, GOLD, BODY)
    for ang in (-43, 43):
        L.alpha_composite(sw.rotate(ang, resample=Image.BICUBIC, center=(O * SS, O * SS)), (_s(128 - O), _s(128 - O)))
    _bsave(L, "Badge_Attack.png")

    L = _lp(); _socket_circle(L, 104, GOLD, BODY)
    C, R, Pt = (128, 152), 46, (128, 46)
    dy = C[1] - Pt[1]; Ln = math.sqrt(dy * dy - R * R); sn = R / dy
    T = (Pt[0] + Ln * sn, Pt[1] + Ln * math.sqrt(1 - sn * sn))
    m = Image.new("L", (S, S), 0); dd = ImageDraw.Draw(m)
    dd.ellipse([_s(C[0] - R), _s(C[1] - R), _s(C[0] + R), _s(C[1] + R)], fill=255)
    dd.polygon([(_s(Pt[0]), _s(Pt[1])), (_s(T[0]), _s(T[1])), (_s(2 * Pt[0] - T[0]), _s(T[1]))], fill=255)
    _paste(L, _solid(col(BLOOD)), m)
    m2 = m.transform(m.size, Image.AFFINE, (1, 0, _s(-8), 0, 1, _s(-5)), resample=Image.BILINEAR)
    _paste(L, _solid(col(BLOOD_D)), ImageChops.subtract(m, m2))
    hl = Image.new("L", (S, S), 0)
    ImageDraw.Draw(hl).ellipse([_s(C[0] - 28), _s(C[1] - 34), _s(C[0] - 15), _s(C[1] - 19)], fill=150)
    _paste(L, _solid(col((248, 232, 228))), ImageChops.multiply(hl, m))
    _bsave(L, "Badge_Health.png")

    # 种类
    L = _lp(); _socket_diamond(L, 110, GOLD, mix(BED, GOLD, 0.18))
    d = ImageDraw.Draw(L); g = col(GOLD_L)
    d.polygon(_sp([(104, 146), (152, 146), (146, 124), (110, 124)]), fill=g)
    d.polygon(_sp([(104, 146), (152, 146), (152, 152), (104, 152)]), fill=col(GOLD_D))
    d.polygon(_sp([(110, 124), (146, 124), (140, 112), (116, 112)]), fill=col(GOLD_D))
    d.polygon(_sp([(100, 112), (112, 112), (108, 138), (96, 132)]), fill=g)
    d.polygon(_sp([(156, 112), (144, 112), (148, 138), (160, 132)]), fill=g)
    d.rectangle(_bx(124, 128, 132, 148), fill=col(BED))
    _bsave(L, "Badge_Type_Hero.png")

    L = _lp(); _socket_diamond(L, 110, IVORY, mix(BED, IVORY, 0.16))
    d = ImageDraw.Draw(L)
    d.rectangle(_bx(120, 108, 136, 158), fill=col(IVORY))
    d.rectangle(_bx(104, 124, 152, 140), fill=col(IVORY))
    d.rectangle(_bx(128, 108, 136, 158), fill=col((198, 186, 150)))
    _bsave(L, "Badge_Type_Chosen.png")

    L = _lp(); _socket_diamond(L, 110, STEEL, mix(BED, STEEL, 0.16))
    d = ImageDraw.Draw(L); cx, cy, a, b = 128, 130, 46, 13
    d.polygon(_sp([(cx, cy - a), (cx + b, cy - b), (cx + a, cy), (cx + b, cy + b),
                   (cx, cy + a), (cx - b, cy + b), (cx - a, cy), (cx - b, cy - b)]), fill=col(STEEL))
    d.polygon(_sp([(cx, cy - a), (cx + b, cy - b), (cx + a, cy), (cx, cy)]), fill=col((208, 226, 246)))
    d.ellipse(_bx(cx - 10, cy - 10, cx + 10, cy + 10), fill=col(BED, 255))
    _bsave(L, "Badge_Type_Special.png")


# ══════════════════════════ 预览 ══════════════════════════
def A(name):
    return Image.open(os.path.join(OUT, name)).convert("RGBA")


def assemble(name="能量收割者", cost="3", hp="6", atk="2", typeb="Badge_Type_Hero.png",
             edge="Card_Edge.png", art=ART):
    L = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for f in ("Card_Body.png", edge, "Card_NamePlate.png", "Card_ArtWindow.png"):
        L.alpha_composite(A(f))
    if art:
        L.alpha_composite(Image.open(art).convert("RGBA").resize((AW[2] - AW[0], AW[3] - AW[1]), Image.LANCZOS), (AW[0], AW[1]))
    L.alpha_composite(A("Card_StatPlate.png"))
    for img, cxy, sz in (("Badge_Cost.png", COST_C, 128), (typeb, TYPE_C, 100),
                         ("Badge_Health.png", HP_C, 118), ("Badge_Attack.png", ATK_C, 118)):
        im = A(img).resize((sz, sz), Image.LANCZOS)
        L.alpha_composite(im, (int(cxy[0] - sz / 2), int(cxy[1] - sz / 2)))
    d = ImageDraw.Draw(L)

    def ctext(t, cx, cy, f, fill, stroke, sc):
        bb = d.textbbox((0, 0), t, font=f, stroke_width=stroke)
        d.text((cx - (bb[2] - bb[0]) / 2 - bb[0], cy - (bb[3] - bb[1]) / 2 - bb[1]), t,
               font=f, fill=fill, stroke_width=stroke, stroke_fill=sc)

    size = 92
    while size > 30:
        f = ImageFont.truetype(FON, size)
        if d.textlength(name, font=f) <= (NAME_X1 - NAME_X0): break
        size -= 2
    f_name = ImageFont.truetype(FON, size)
    bb = d.textbbox((0, 0), name, font=f_name, stroke_width=3)
    d.text((NAME_X0 - bb[0], 113 - (bb[3] - bb[1]) / 2 - bb[1]), name, font=f_name,
           fill=(238, 230, 208), stroke_width=3, stroke_fill=(6, 10, 16))
    ctext(cost, COST_C[0], COST_C[1] + 2, ImageFont.truetype(FON, 78), (240, 244, 250), 4, (4, 8, 16))
    ctext(hp, HP_C[0], HP_C[1] + 4, ImageFont.truetype(FON, 60), (246, 240, 236), 4, (4, 8, 16))
    ctext(atk, ATK_C[0], ATK_C[1] + 4, ImageFont.truetype(FON, 60), (246, 240, 236), 4, (4, 8, 16))
    return L


def build_previews():
    os.makedirs(PREVIEW, exist_ok=True)
    card = assemble()
    bg = Image.new("RGB", (W, H), (24, 28, 34)); bg.paste(card, (0, 0), card)
    nb = Image.new("RGB", (W, H), (24, 28, 34)); c2 = assemble(art=None); nb.paste(c2, (0, 0), c2)
    bk = Image.new("RGB", (W, H), (24, 28, 34)); bb = A("Card_Back.png"); bk.paste(bb, (0, 0), bb)
    out = Image.new("RGB", (W * 3 + 80, H + 40), (18, 21, 26))
    for i, im in enumerate([nb, bg, bk]):
        out.paste(im, (20 + i * 788, 20))
    out.save(os.path.join(PREVIEW, "cardframe-v7-card.png"))

    comps = [("Card_Body", "卡身"), ("Card_Edge", "金线框"), ("Card_NamePlate", "名牌"),
             ("Card_ArtWindow", "画窗"), ("Card_StatPlate", "数值条"), ("Card_Back", "卡背"),
             ("Badge_Cost", "费用徽"), ("Badge_Attack", "攻击徽"), ("Badge_Health", "生命徽"),
             ("Badge_Type_Hero", "种类·英雄"), ("Badge_Type_Chosen", "种类·天选"),
             ("Badge_Type_Special", "种类·特殊")]
    cols, cw, ch, lab = 6, 300, 525, 26
    rows = (len(comps) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cw, rows * (ch + lab)), (24, 28, 34))
    d = ImageDraw.Draw(sheet); f = ImageFont.truetype(FON, 17)
    for i, (n, cn) in enumerate(comps):
        r, c = divmod(i, cols)
        im = A(n + ".png"); im.thumbnail((cw - 24, ch - 24), Image.LANCZOS)
        sheet.paste(im, (c * cw + (cw - im.width) // 2, r * (ch + lab) + lab + (ch - im.height) // 2), im)
        d.text((c * cw + 10, r * (ch + lab) + 5), cn + "  " + n + ".png", font=f, fill=(215, 215, 215))
    sheet.save(os.path.join(PREVIEW, "cardframe-v7-components.png"))

    old = Image.open(r"Assets/_Game/Art/Sprites/Generated/CardFrameCUR_scaled.png").convert("RGBA").resize((W, H))
    CW, CH = 104, 182
    pl = lambda im: im.resize((CW, CH), Image.LANCZOS)
    board = Image.open(os.path.join(PREVIEW, "board-full-after.png")).convert("RGB").resize((1476, 830), Image.LANCZOS)
    o = Image.new("RGB", (1476, 1210), (18, 21, 26)); o.paste(board, (0, 0))
    d = ImageDraw.Draw(o); f = ImageFont.truetype(FON, 20); f2 = ImageFont.truetype(FON, 16)
    imgs = [pl(card) for _ in range(3)] + [pl(old)]
    labels = ["新卡 常态", "新卡 可打出", "新卡 不可用", "旧卡（现役）"]
    for i, (im, lb) in enumerate(zip(imgs, labels)):
        x, y = 120 + i * 280, 300
        if i == 1:
            gl = Image.new("RGBA", (CW + 16, CH + 16), (0, 0, 0, 0))
            ImageDraw.Draw(gl).rounded_rectangle([2, 2, CW + 13, CH + 13], radius=10, outline=(120, 235, 130, 235), width=3)
            o.paste(im, (x, y), im); o.paste(gl, (x - 8, y - 8), gl)
        elif i == 2:
            g = Image.blend(im, im.convert("L").convert("RGBA"), 0.75)
            g = ImageEnhance.Brightness(g).enhance(0.62); g.putalpha(Image.new("L", (CW, CH), 205))
            o.paste(g, (x, y), g)
        else:
            o.paste(im, (x, y), im)
        d.text((x, y + CH + 8), lb, font=f, fill=(232, 232, 232))
    for i, (im, lb) in enumerate(zip(imgs, labels)):
        x = 120 + i * 330; z = pl(im).resize((260, 455), Image.LANCZOS)
        o.paste(z, (x, 560), z); d.text((x, 1023), lb + "  ×2.5", font=f2, fill=(210, 210, 210))
    d.text((24, 24), "cardframe-v7 —— 组件分离卡框 / 贴合棋盘夜色钢蓝+金线", font=f, fill=(230, 226, 214))
    o.save(os.path.join(PREVIEW, "cardframe-v7-onscreen.png"))


# ══════════════════════════ 前缀 / 特性 / 状态 图标 ══════════════════════════
PREFIX_TINT = {"Abyss": (160, 90, 232), "Blood": (216, 54, 78), "Mech": (217, 135, 60),
               "Psychic": (69, 198, 232), "Scroll": (92, 192, 126)}
TRAIT_TINT = {"First": (232, 194, 74), "Enter": (95, 180, 232), "Leave": (147, 164, 184),
              "Exit": (232, 144, 60), "Revenge": (216, 72, 60), "Discard": (216, 184, 120),
              "Attach": (111, 191, 138)}
STATUS_TINT = {"Shield": (154, 180, 216), "Buff": (86, 200, 138), "DeBuff": (216, 72, 84)}
TRAIT_CN = {"First": "先手", "Enter": "进场", "Leave": "退场", "Exit": "主动退场",
            "Revenge": "反击", "Discard": "抛置", "Attach": "附着"}
PREFIX_CN = {"Abyss": "渊", "Blood": "血歌", "Mech": "机械", "Psychic": "灵能", "Scroll": "神灵画卷"}
STATUS_CN = {"Shield": "护盾", "Buff": "增益", "DeBuff": "减益"}


def _socket_square(lay, half, line, fill, rad=46, lw=7.0):
    box = (128 - half, 128 - half, 128 + half, 128 + half)
    d = ImageDraw.Draw(lay)
    d.rounded_rectangle(_bx(*box), radius=_s(rad), fill=col(fill))
    d.rounded_rectangle(_bx(128 - half + 14, 128 - half + 14, 128 + half - 14, 128 + half - 14),
                        radius=_s(rad * 0.7), fill=col(BED, 255))
    d.rounded_rectangle(_bx(*box), radius=_s(rad), outline=col(line), width=_s(lw))


def _gear(lay, cx, cy, r, teeth, color, dark):
    d = ImageDraw.Draw(lay)
    for i in range(teeth):
        a = math.radians(i * 360.0 / teeth)
        d.polygon(_sp([(cx + (r - 6) * math.cos(a) - 11 * math.sin(a), cy + (r - 6) * math.sin(a) + 11 * math.cos(a)),
                       (cx + (r + 13) * math.cos(a) - 9 * math.sin(a), cy + (r + 13) * math.sin(a) + 9 * math.cos(a)),
                       (cx + (r + 13) * math.cos(a) + 9 * math.sin(a), cy + (r + 13) * math.sin(a) - 9 * math.cos(a)),
                       (cx + (r - 6) * math.cos(a) + 11 * math.sin(a), cy + (r - 6) * math.sin(a) - 11 * math.cos(a))]), fill=col(color))
    d.ellipse(_bx(cx - r, cy - r, cx + r, cy + r), fill=col(color))
    d.polygon(_sp([(128, cy - r), (128 + r, cy), (128, cy + r), (128 - r, cy)]), fill=col(dark))
    d.ellipse(_bx(cx - 22, cy - 22, cx + 22, cy + 22), fill=col(BED, 255))


def _arrow(lay, cx, cy, ang, color):
    a = math.radians(ang)
    def R(x, y):
        return (cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a))
    ImageDraw.Draw(lay).polygon(
        _sp([R(-40, -26), R(6, -26), R(6, -46), R(52, 0), R(6, 46), R(6, 26), R(-40, 26)]), fill=col(color))


def _return_glyph(lay, tint):
    d = ImageDraw.Draw(lay)
    c = mix(tint, (0, 0, 0), 0.25)
    d.arc(_bx(68, 68, 188, 188), 300, 200, fill=col(c), width=_s(18))
    d.polygon(_sp([(178, 60), (208, 96), (160, 100)]), fill=col(c))
    d.arc(_bx(96, 96, 160, 160), 200, 340, fill=col(mix(tint, (255, 255, 255), 0.30)), width=_s(11))


def _shield_glyph(lay, color, dark):
    d = ImageDraw.Draw(lay)
    d.polygon(_sp([(128, 62), (194, 86), (194, 140), (128, 198), (62, 140), (62, 86)]), fill=col(color))
    d.polygon(_sp([(128, 62), (194, 86), (194, 140), (128, 198)]), fill=col(dark))


def _rings_glyph(lay, color, dark):
    d = ImageDraw.Draw(lay)
    d.ellipse(_bx(64, 92, 160, 188), outline=col(color), width=_s(16))
    d.ellipse(_bx(96, 68, 192, 164), outline=col(dark), width=_s(16))


def _bolt_glyph(lay, tint):
    ImageDraw.Draw(lay).polygon(_sp([(150, 48), (84, 140), (120, 140), (106, 208), (174, 110), (136, 110)]), fill=col(tint))


def _eye_glyph(lay, tint):
    d = ImageDraw.Draw(lay)
    d.ellipse(_bx(52, 82, 204, 174), fill=col((228, 230, 238)))
    d.polygon(_sp([(52, 128), (128, 182), (204, 128), (128, 174)]), fill=col((196, 200, 214)))
    d.ellipse(_bx(98, 98, 158, 158), fill=col(tint))
    d.ellipse(_bx(115, 115, 141, 141), fill=col(BED, 255))
    d.polygon(_sp([(128, 74), (146, 100), (110, 100)]), fill=col((228, 230, 238)))


def _crystal_glyph(lay, tint):
    _socket_diamond(lay, 98, tint, mix(BED, tint, 0.30), lw=6.0)
    d = ImageDraw.Draw(lay)
    pts = [(128, 62), (170, 128), (128, 194), (86, 128)]
    d.polygon(_sp(pts), fill=col((226, 240, 250)))
    d.polygon(_sp([(128, 62), (170, 128), (128, 194)]), fill=col(mix(tint, (255, 255, 255), 0.35)))


def _scroll_glyph(lay, tint):
    d = ImageDraw.Draw(lay)
    d.rectangle(_bx(100, 84, 156, 176), fill=col((235, 228, 210)))
    for i in range(4):
        d.rectangle(_bx(112, 104 + i * 18, 144, 111 + i * 18), fill=col((146, 142, 128)))
    for cx in (94, 162):
        d.ellipse(_bx(cx - 14, 70, cx + 14, 190), fill=col(mix(tint, (0, 0, 0), 0.30)))
        d.ellipse(_bx(cx - 8, 78, cx + 8, 104), fill=col(mix(tint, (255, 255, 255), 0.30)))
        d.ellipse(_bx(cx - 8, 156, cx + 8, 182), fill=col(mix(tint, (0, 0, 0), 0.55)))


def _droplet_glyph(lay, tint):
    C, R, Pt = (128, 152), 40, (128, 62)
    dy = C[1] - Pt[1]; Ln = math.sqrt(dy * dy - R * R); sn = R / dy
    T = (Pt[0] + Ln * sn, Pt[1] + Ln * math.sqrt(1 - sn * sn))
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    d.ellipse(_bx(C[0] - R, C[1] - R, C[0] + R, C[1] + R), fill=255)
    d.polygon(_sp([Pt, T, (2 * Pt[0] - T[0], T[1])]), fill=255)
    _paste(lay, _solid(col(tint)), m)
    m2 = m.transform(m.size, Image.AFFINE, (1, 0, _s(-7), 0, 1, _s(-5)), resample=Image.BILINEAR)
    _paste(lay, _solid(col(mix(tint, (0, 0, 0), 0.38))), ImageChops.subtract(m, m2))
    hl = Image.new("L", (S, S), 0)
    ImageDraw.Draw(hl).ellipse(_bx(C[0] - 24, C[1] - 30, C[0] - 12, C[1] - 16), fill=140)
    _paste(lay, _solid(col((252, 242, 238))), ImageChops.multiply(hl, m))


def _grave_glyph(lay, tint):
    d = ImageDraw.Draw(lay)
    d.pieslice(_bx(84, 62, 172, 150), 180, 360, fill=col(tint))
    d.rectangle(_bx(84, 106, 172, 190), fill=col(tint))
    d.rectangle(_bx(120, 92, 136, 162), fill=col(BED, 255))
    d.rectangle(_bx(104, 110, 152, 126), fill=col(BED, 255))


def _helm_glyph(lay, tint):
    d = ImageDraw.Draw(lay)
    d.polygon(_sp([(96, 168), (160, 168), (152, 118), (104, 118)]), fill=col(tint))
    d.polygon(_sp([(90, 120), (104, 120), (100, 158), (84, 150)]), fill=col(tint))
    d.polygon(_sp([(166, 120), (152, 120), (156, 158), (172, 150)]), fill=col(tint))
    d.rectangle(_bx(120, 128, 136, 172), fill=col(BED, 255))


def build_icons():
    for k, t in PREFIX_TINT.items():
        L = _lp()
        _socket_hex(L, 104, t, mix(BED, t, 0.16))
        ({"Abyss": lambda l, c: _eye_glyph(l, c),
          "Blood": lambda l, c: _droplet_glyph(l, c),
          "Mech": lambda l, c: _gear(l, 128, 128, 52, 8, c, mix(c, (0, 0, 0), 0.45)),
          "Psychic": lambda l, c: _crystal_glyph(l, c),
          "Scroll": lambda l, c: _scroll_glyph(l, c)}[k])(L, t)
        _bsave(L, "Icon_Prefix_%s.png" % k)

    for k, t in TRAIT_TINT.items():
        L = _lp()
        _socket_square(L, 100, t, mix(BED, t, 0.14))
        g = {"First": lambda l, c: _bolt_glyph(l, c),
             "Enter": lambda l, c: _arrow(l, 128, 128, 0, c),
             "Leave": lambda l, c: _grave_glyph(l, c),
             "Exit": lambda l, c: _arrow(l, 128, 128, 180, c),
             "Revenge": lambda l, c: _return_glyph(l, c),
             "Discard": lambda l, c: _scroll_glyph(l, c),
             "Attach": lambda l, c: _rings_glyph(l, c, mix(c, (0, 0, 0), 0.35))}[k]
        g(L, t)
        _bsave(L, "Icon_Trait_%s.png" % k)

    for k, t in STATUS_TINT.items():
        L = _lp()
        _socket_circle(L, 104, t, mix(BED, t, 0.14))
        if k == "Shield":
            _shield_glyph(L, t, mix(t, (0, 0, 0), 0.4))
        elif k == "Buff":
            _arrow(L, 128, 132, -90, mix(t, (255, 255, 255), 0.25))
        else:
            _arrow(L, 128, 132, 90, mix(t, (255, 255, 255), 0.25))
        _bsave(L, "Icon_Status_%s.png" % k)


def build_icon_preview():
    rows = [
        ("前缀  Prefix", [("Icon_Prefix_Abyss", "渊"), ("Icon_Prefix_Blood", "血歌"),
                          ("Icon_Prefix_Mech", "机械"), ("Icon_Prefix_Psychic", "灵能"),
                          ("Icon_Prefix_Scroll", "神灵画卷")]),
        ("特性  Trait", [("Icon_Trait_First", "先手"), ("Icon_Trait_Enter", "进场"),
                         ("Icon_Trait_Leave", "退场"), ("Icon_Trait_Exit", "主动退场"),
                         ("Icon_Trait_Revenge", "反击"), ("Icon_Trait_Discard", "抛置"),
                         ("Icon_Trait_Attach", "附着")]),
        ("状态  Status", [("Icon_Status_Shield", "护盾"), ("Icon_Status_Buff", "增益"),
                          ("Icon_Status_DeBuff", "减益")]),
    ]
    cw, lab = 230, 30
    out = Image.new("RGB", (7 * cw + 40, len(rows) * (cw + lab) + 40), (24, 28, 34))
    d = ImageDraw.Draw(out); f = ImageFont.truetype(FON, 18)
    y = 20
    for title, items in rows:
        d.text((20, y), title, font=f, fill=(232, 226, 208)); y += lab
        for i, (n, cn) in enumerate(items):
            im = A(n + ".png").resize((cw - 30, cw - 30), Image.LANCZOS)
            x = 20 + i * cw
            out.paste(im, (x + 15, y + 10), im)
            d.text((x + 15, y + cw - 10), cn, font=f, fill=(210, 210, 210))
        y += cw
    out.save(os.path.join(PREVIEW, "cardframe-v7-icons.png"))


def build_cost_preview():
    CUR = r"Assets/_Game/Resources/Cards/Back And Front/Summon/SummonCard_%d.png"
    CWv, CHv = 384, 672
    CY = ["#8C8C8C", "#F5F0E4", "#3C8C3C", "#325AA0", "#643C96", "#E8C850"]   # 现役实测
    MY = ["#929AA4", "#E2DFD4", "#56B068", "#568CD6", "#986CD0", "#E2BA56"]   # 新方案提亮
    out = Image.new("RGB", (2560, 1880), (22, 25, 31)); d = ImageDraw.Draw(out)
    ft = ImageFont.truetype(FON, 36); fs = ImageFont.truetype(FON, 23); fm = ImageFont.truetype(FON, 19)
    d.text((40, 28), "费用档怎么区分？—— 只换「边带」这一层", font=ft, fill=(236, 228, 208))
    d.text((40, 80), "现役：SummonCard_0..5 六张整框，靠 34px 实心色带区分（每张 768×1344 ≈ 385KB）", font=fs, fill=(176, 182, 196))
    y = 130
    d.text((40, y), "现役整框", font=fs, fill=(224, 220, 208)); y += 40
    for i in range(6):
        im = Image.open(CUR % i).convert("RGBA").resize((CWv, CHv), Image.LANCZOS)
        out.paste(im, (40 + i * (CWv + 20), y), im)
        d.text((40 + i * (CWv + 20), y + CHv + 10), "%d 费" % i, font=fs, fill=(228, 228, 228))
    y += CHv + 60
    d.text((40, y), "新方案 Card_Edge_0..5：卡身 / 名牌 / 画窗 / 数值条 / 徽标 全部共用，只换边带层（22px 实色，无倒角）",
           font=fs, fill=(224, 220, 208)); y += 40
    for i in range(6):
        im = assemble(cost=str(i), edge="Card_Edge_%d.png" % i, art=None).resize((CWv, CHv), Image.LANCZOS)
        out.paste(im, (40 + i * (CWv + 20), y), im)
        d.text((40 + i * (CWv + 20), y + CHv + 10), "%d 费" % i, font=fs, fill=(228, 228, 228))
    y += CHv + 60
    d.text((40, y), "色相（左：现役实测原值 / 右：新方案提亮，暗蓝暗紫在暗底上要够亮）", font=fs, fill=(224, 220, 208)); y += 40
    for i in range(6):
        x = 40 + i * 415
        for k, hexs in enumerate((CY[i], MY[i])):
            c = tuple(int(hexs[j:j+2], 16) for j in (1, 3, 5))
            d.rectangle([x + k * 190, y, x + k * 190 + 170, y + 66], fill=c)
            d.text((x + k * 190, y + 74), hexs, font=fm, fill=(200, 200, 200))
        d.text((x, y + 104), "%d 费" % i, font=fs, fill=(228, 228, 228))
    d.text((40, y + 160), "另给 Card_Edge_Tint.png（纯白版）：一张图用 Image.color 乘色，运行时随费用实时变色（减费 3→2 能当场从蓝跳绿）",
           font=fm, fill=(168, 176, 190))
    out.save(os.path.join(PREVIEW, "cardframe-v7-cost-tiers.png"))


if __name__ == "__main__":
    print("frame:"); build_frame()
    print("badges:"); build_badges()
    print("icons:"); build_icons()
    build_icon_preview()
    print("previews:"); build_previews()
    build_cost_preview()
    print("done ->", OUT)

