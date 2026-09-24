# -*- coding: utf-8 -*-
"""前缀底图 v2 边缘体系（几何）—— 被 PrefixBgV2.py import

v1 的母题全在中央，实机被召唤物立绘挡掉 → 认不出前缀。v2 把「身份」搬到
**画窗边带 + 四角 + 上下缘**：实测 14 张 1 费卡，原画在左右各留 5~17%、
上下 3~15% 的空边，四角基本全露 —— 这三处是唯一「永远看得见」的地方。

明度口径：
  边带 = 接近满色（210/255），四角 198，花纹 168
  中央母题交给 v1 的母题函数、整体乘 0.40 当纹理

每个函数签名 (d, t, wd, ht)：d=ImageDraw、t=前缀色相、wd/ht=画布宽高。
画布：6 张 = 768x1024；法术通用 = 768x768（法术画窗是方的）。
"""
import math

INS = 14          # 内框色带：距画窗边内缩
BT = 46           # 内框色带厚度
BAND, BAND2, CORNERA, PAT = 214, 118, 198, 168


def make(S, col, mix):
    """S=缩放函数 col=(rgb,a)->tuple mix=(a,b,t)->rgb"""

    def line(d, pts, c, lw):
        d.line([(S(x), S(y)) for (x, y) in pts], fill=c,
               width=max(1, int(round(S(lw)))), joint="curve")

    def rrect(d, x0, y0, x1, y1, r, c, lw):
        d.rounded_rectangle([S(x0), S(y0), S(x1), S(y1)], radius=S(r), outline=c,
                            width=max(1, int(round(S(lw)))))

    def frrect(d, x0, y0, x1, y1, r, fill):
        d.rounded_rectangle([S(x0), S(y0), S(x1), S(y1)], radius=S(r), fill=fill)

    def rect(d, x0, y0, x1, y1, fill):
        d.rectangle([S(x0), S(y0), S(x1), S(y1)], fill=fill)

    def circle(d, cx, cy, r, c, lw):
        d.ellipse([S(cx - r), S(cy - r), S(cx + r), S(cy + r)], outline=c,
                  width=max(1, int(round(S(lw)))))

    def dcircle(d, cx, cy, r, c):
        d.ellipse([S(cx - r), S(cy - r), S(cx + r), S(cy + r)], fill=c)

    def arc(d, cx, cy, r, a0, a1, c, lw):
        d.arc([S(cx - r), S(cy - r), S(cx + r), S(cy + r)], a0, a1, fill=c,
              width=max(1, int(round(S(lw)))))

    def poly(d, pts, c, lw, fill=None):
        P = [(S(x), S(y)) for (x, y) in pts]
        if fill is not None:
            d.polygon(P, fill=fill)
        if lw > 0:
            d.line(P + [P[0]], fill=c, width=max(1, int(round(S(lw)))), joint="curve")

    def ngon(cx, cy, r, n, rot=-90.0):
        return [(cx + r * math.cos(math.radians(rot + i * 360.0 / n)),
                 cy + r * math.sin(math.radians(rot + i * 360.0 / n))) for i in range(n)]

    def corners(wd, ht, k=None):
        k = INS + BT / 2.0 if k is None else k
        return [(k, k), (wd - k, k), (k, ht - k), (wd - k, ht - k)]

    def band(d, t, wd, ht, thick=BT, a=BAND, r=40):
        rrect(d, INS, INS, wd - INS, ht - INS, r, col(t, a), thick)

    def inner(d, t, wd, ht, thick=BT, off=20, a=BAND2, r=26, lw=4):
        o = INS + thick + off
        rrect(d, o, o, wd - o, ht - o, r, col(t, a), lw)

    def note(d, x, y, t, s=1.0):
        dcircle(d, x - 12 * s, y + 20 * s, 23 * s, col(t, CORNERA))
        line(d, [(x + 9 * s, y + 20 * s), (x + 9 * s, y - 44 * s)], col(t, CORNERA), 8)
        line(d, [(x + 9 * s, y - 44 * s), (x + 42 * s, y - 33 * s)], col(t, CORNERA), 8)

    def drop(d, x, y0, r, t, a):
        """尖朝上、圆在下的水滴（挂在带内侧）"""
        poly(d, [(x, y0), (x - r * 0.86, y0 + r * 1.25), (x + r * 0.86, y0 + r * 1.25)],
             col(t, a), 0, fill=col(t, a))
        dcircle(d, x, y0 + r * 1.45, r, col(t, a))

    # ══ 灵能：符文刻度环带 + 六棱晶四角 ══════════════════════════
    def psychic(d, t, wd, ht):
        band(d, t, wd, ht)
        inner(d, t, wd, ht)
        yc, xc = INS + BT + 10, INS + BT + 10
        for x in range(int(wd * 0.15), int(wd * 0.86), 46):
            line(d, [(x, yc), (x, yc + 30)], col(t, PAT), 5)
            line(d, [(x, ht - yc), (x, ht - yc - 30)], col(t, PAT), 5)
        for y in range(int(ht * 0.15), int(ht * 0.86), 46):
            line(d, [(xc, y), (xc + 30, y)], col(t, PAT), 5)
            line(d, [(wd - xc, y), (wd - xc - 30, y)], col(t, PAT), 5)
        for (x, y) in corners(wd, ht, k=62):
            poly(d, ngon(x, y, 50, 6, rot=-90), col(t, CORNERA), 0, fill=col(t, 74))
            poly(d, ngon(x, y, 27, 6, rot=-90), col(t, CORNERA), 5)

    # ══ 渊：带内一圈吸盘弧 + 眼形四角 ═══════════════════════════
    def abyss(d, t, wd, ht):
        band(d, t, wd, ht)
        yc, xc = INS + BT + 14, INS + BT + 14
        for x in range(int(wd * 0.17), int(wd * 0.84), 64):
            arc(d, x, yc, 25, 180, 360, col(t, PAT - 22), 9)
            arc(d, x, ht - yc, 25, 0, 180, col(t, PAT - 22), 9)
        for y in range(int(ht * 0.17), int(ht * 0.84), 64):
            arc(d, xc, y, 25, 90, 270, col(t, PAT - 22), 9)
            arc(d, wd - xc, y, 25, -90, 90, col(t, PAT - 22), 9)
        for (x, y) in corners(wd, ht, k=64):
            arc(d, x, y, 52, 180, 360, col(t, CORNERA), 10)
            arc(d, x, y, 52, 0, 180, col(t, CORNERA), 10)
            dcircle(d, x, y, 19, col(t, CORNERA))
            dcircle(d, x, y, 7, col((8, 10, 18), 255))

    # ══ 机械：四边齿条 + 方角铆钉 ═══════════════════════════════
    def mech(d, t, wd, ht):
        band(d, t, wd, ht)
        t0, t1 = INS + BT, INS + BT + 30
        for x in range(int(wd * 0.13), int(wd * 0.87), 44):
            rect(d, x, t0, x + 24, t1, col(t, PAT))
            rect(d, x, ht - t1, x + 24, ht - t0, col(t, PAT))
        for y in range(int(ht * 0.13), int(ht * 0.87), 44):
            rect(d, t0, y, t1, y + 24, col(t, PAT))
            rect(d, wd - t1, y, wd - t0, y + 24, col(t, PAT))
        for (x, y) in corners(wd, ht, k=64):
            frrect(d, x - 48, y - 48, x + 48, y + 48, 12, col(t, 66))
            rrect(d, x - 48, y - 48, x + 48, y + 48, 12, col(t, CORNERA), 9)
            dcircle(d, x, y, 18, col(t, CORNERA))

    # ══ 血歌：上缘声波 + 下缘滴落 + 音符四角 ═════════════════════
    def blood(d, t, wd, ht):
        band(d, t, wd, ht)
        for i in range(3):
            arc(d, wd / 2.0, INS + BT + 44 + i * 34, 170 + i * 68, 180, 360,
                col(t, PAT - i * 36), 7)
        for x in (wd * 0.21, wd * 0.50, wd * 0.79):
            drop(d, x, ht - INS - BT - 84, 30, t, PAT)
        for (x, y) in corners(wd, ht, k=64):
            note(d, x, y, t)

    # ══ 神灵画卷：上下卷杆 + 卷轴端头四角 ════════════════════════
    def scroll(d, t, wd, ht):
        band(d, t, wd, ht, thick=34)
        for y in (INS + 34 + 34, ht - INS - 34 - 34):
            frrect(d, INS + 24, y - 26, wd - INS - 24, y + 26, 26, col(t, 104))
            rrect(d, INS + 24, y - 26, wd - INS - 24, y + 26, 26, col(t, CORNERA), 7)
        for (x, y) in corners(wd, ht, k=62):
            frrect(d, x - 46, y - 25, x + 46, y + 25, 25, col(t, 100))
            rrect(d, x - 46, y - 25, x + 46, y + 25, 25, col(t, CORNERA), 7)

    # ══ 通用（无前缀）：最朴素 —— 细双框 + 菱形钉 ════════════════
    def common(d, t, wd, ht):
        band(d, t, wd, ht, thick=30, a=int(BAND * 0.74))
        inner(d, t, wd, ht, thick=30, off=18, a=int(BAND2 * 0.82), r=22, lw=3)
        for y in (INS + 30 + 48, INS + 30 + 66):
            line(d, [(wd * 0.16, y), (wd * 0.84, y)], col(t, 94), 3)
            line(d, [(wd * 0.16, ht - y), (wd * 0.84, ht - y)], col(t, 94), 3)
        for (x, y) in corners(wd, ht, k=62):
            poly(d, ngon(x, y, 33, 4, rot=-90), col(t, CORNERA), 0, fill=col(t, 86))

    # ══ 法术通用：细边带 + 小角点（不厚、不装饰）══════════════
    def spell(d, t, wd, ht):
        band(d, t, wd, ht, thick=24, a=198, r=24)
        for (x, y) in corners(wd, ht, k=INS + 12):
            frrect(d, x - 24, y - 24, x + 24, y + 24, 7, col(t, 176))

    return {"Abyss": abyss, "Blood": blood, "Mech": mech, "Psychic": psychic,
            "Scroll": scroll, "Common": common, "Spell": spell}