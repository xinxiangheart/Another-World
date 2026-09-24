# -*- coding: utf-8 -*-
"""前缀底图 v1 母题（几何 + 明度重写版）—— 被 PrefixBgV1.py import"""
import math
from PIL import ImageDraw

W, H = 768, 1024

def make(S, col, mix):
    """S=缩放函数 col=(rgb,a)->tuple"""

    def line(d, pts, c, w):
        d.line([(S(x), S(y)) for (x, y) in pts], fill=c, width=max(1, int(round(S(w)))), joint="curve")

    def circle(d, cx, cy, r, c, w):
        d.ellipse([S(cx - r), S(cy - r), S(cx + r), S(cy + r)], outline=c, width=max(1, int(round(S(w)))))

    def dcircle(d, cx, cy, r, c):
        d.ellipse([S(cx - r), S(cy - r), S(cx + r), S(cy + r)], fill=c)

    def ticks(d, cx, cy, r, n, ln, c, w, a0=0.0):
        for i in range(n):
            a = math.radians(a0 + i * 360.0 / n)
            d.line([(S(cx + r * math.cos(a)), S(cy + r * math.sin(a))),
                    (S(cx + (r + ln) * math.cos(a)), S(cy + (r + ln) * math.sin(a)))],
                   fill=c, width=max(1, int(round(S(w)))))

    def arc(d, cx, cy, r, a0, a1, c, w):
        d.arc([S(cx - r), S(cy - r), S(cx + r), S(cy + r)], a0, a1, fill=c, width=max(1, int(round(S(w)))))

    def poly(d, pts, c, w, fill=None):
        P = [(S(x), S(y)) for (x, y) in pts]
        if fill is not None:
            d.polygon(P, fill=fill)
        if w > 0:
            d.line(P + [P[0]], fill=c, width=max(1, int(round(S(w)))), joint="curve")

    def ngon(cx, cy, r, n, rot=-90.0):
        return [(cx + r * math.cos(math.radians(rot + i * 360.0 / n)),
                 cy + r * math.sin(math.radians(rot + i * 360.0 / n))) for i in range(n)]

    def gear(d, cx, cy, ro, ri, teeth, c, w):
        pts = []
        for i in range(teeth * 2):
            a = math.pi * i / teeth + math.pi / (teeth * 2)
            rr = ro if i % 2 == 0 else ri
            pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        poly(d, pts, c, w)
        circle(d, cx, cy, ri * 0.66, c, w)
        circle(d, cx, cy, ri * 0.20, c, w)

    def teardrop(d, cx, ay, cy, R, c, w, fill_a=0):
        """以 (cx,ay) 为尖、(cx,cy) 为圆心 R 的水滴：圆 + 两条切线"""
        dd = math.hypot(cx - cx, cy - ay)
        beta = math.acos(min(1.0, R / dd))
        theta = math.atan2(ay - cy, 0.0)          # C→A 方向 = -90°
        t1 = theta + beta
        t2 = theta - beta
        pts = [(cx + R * math.cos(t1), cy + R * math.sin(t1))]
        # 从 t1 逆时针扫到 t2（经过远离 A 的一侧）
        a = t1
        while a < t2 + 2 * math.pi - 2 * math.pi:
            break
        steps = 48
        a0, a1 = t1, t2 + 2 * math.pi
        for i in range(steps + 1):
            aa = a0 + (a1 - a0) * i / steps
            pts.append((cx + R * math.cos(aa), cy + R * math.sin(aa)))
        pts.append((cx, ay))
        poly(d, pts, c, w, fill=col(c[:3], fill_a) if fill_a else None)

    # ══ 共有「家族」元素：一圈大弧 + 四角刻线（七张都画，弱）══
    def family(d, t, cx, cy, F=16):
        arc(d, cx, cy, 372, 0, 360, col(t, F), 2.0)
        for a in (45, 135, 225, 315):
            r0 = 372
            line(d, [(cx + r0 * math.cos(math.radians(a)), cy + r0 * math.sin(math.radians(a))),
                     (cx + (r0 + 26) * math.cos(math.radians(a)), cy + (r0 + 26) * math.sin(math.radians(a)))],
                 col(t, 22), 2.0)

    # ── 母题 ──────────────────────────────────────────────────────
    MAIN, FINE, DOTS, FAINT = 56, 38, 78, 16

    def abyss(d, t):
        cx, cy = W / 2, H * 0.45
        family(d, t, cx, cy)
        # 旋涡：三层同心椭圆（透视成椭圆，像深渊开口）
        for i, (rx, a) in enumerate(((300, MAIN), (232, FINE), (168, FINE))):
            d.ellipse([S(cx - rx), S(cy - rx * 0.62), S(cx + rx), S(cy + rx * 0.62)],
                      outline=col(t, a), width=max(1, int(round(S(3.0 - i * 0.4)))))
        # 大眼（在旋涡里）
        arc(d, cx, cy, 150, 180, 360, col(t, 128), 3.6)
        arc(d, cx, cy, 150, 0, 180, col(t, 128), 3.6)
        arc(d, cx, cy, 96, 0, 360, col(t, DOTS), 2.6)
        dcircle(d, cx, cy, 44, col(t, 104))
        dcircle(d, cx, cy, 20, col((10, 12, 20), 255))
        # 触手：四角各两条
        for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            for i, o in enumerate((0, 74)):
                cx2, cy2 = cx + sx * (250 + o), cy + sy * (150 + o)
                arc(d, cx2, cy2, 150 + o * 0.4, 0, 360, col(t, FINE - i * 12), 2.6)
        for (x, y, r) in ((200, 236, 8), (566, 200, 6), (162, 812, 7), (604, 848, 10), (392, 924, 5)):
            d.ellipse([S(x - r), S(y - r), S(x + r), S(y + r)], outline=col(t, DOTS), width=max(1, int(round(S(2.0)))))

    def blood(d, t):
        cx, cy = W / 2, H * 0.45
        family(d, t, cx, cy)
        teardrop(d, cx, cy - 296, cy + 84, 176, col(t, MAIN), 3.4, fill_a=16)
        circle(d, cx, cy + 84, 176, col(t, MAIN), 3.4)
        circle(d, cx, cy + 84, 96, col(t, FINE), 2.4)
        # 声波弧（左右各三）
        for i in range(3):
            arc(d, cx - 296 - i * 34, cy + 84, 150 + i * 30, 118, 242, col(t, FINE - i * 8), 2.6)
            arc(d, cx + 296 + i * 34, cy + 84, 150 + i * 30, -62, 62, col(t, FINE - i * 8), 2.6)
        # 音符
        for (x, y, s) in ((176, 262, 1.0), (596, 316, 0.9), (196, 828, 0.8)):
            d.ellipse([S(x - 30 * s), S(y - 23 * s), S(x + 30 * s), S(y + 23 * s)],
                      fill=col(t, DOTS))
            line(d, [(x + 27 * s, y), (x + 27 * s, y - 104 * s)], col(t, DOTS), 2.4)
            line(d, [(x + 27 * s, y - 104 * s), (x + 62 * s, y - 90 * s)], col(t, DOTS), 2.4)
        # 谱线
        for i in range(4):
            y = 892 + i * 24
            line(d, [(104, y), (W - 104, y)], col(t, FAINT + 4), 1.6)

    def mech(d, t):
        cx, cy = W / 2, H * 0.45
        family(d, t, cx, cy)
        gear(d, cx - 60, cy, 168, 132, 12, col(t, MAIN), 3.0)
        gear(d, cx + 236, cy - 176, 104, 80, 10, col(t, FINE), 2.6)
        gear(d, cx - 268, cy + 186, 92, 70, 9, col(t, FINE), 2.6)
        # 铆钉列
        for y in range(140, 900, 68):
            for x in (78, W - 78):
                d.ellipse([S(x - 8), S(y - 8), S(x + 8), S(y + 8)], outline=col(t, DOTS), width=max(1, int(round(S(2.2)))))
        # 蒸汽：三段上飘的弧
        for i in range(3):
            for j in range(2):
                arc(d, 168 + i * 150 + j * 26, 210 - j * 40, 92 + j * 30, 190, 330, col(t, FAINT + 6), 2.2)
        # 底沿齿条 + 压力表
        for x in range(232, W - 196, 44):
            line(d, [(x, 950), (x, 984)], col(t, FAINT + 8), 2.2)
        circle(d, W - 132, 900, 46, col(t, FINE), 2.4)
        line(d, [(W - 132, 900), (W - 108, 872)], col(t, FINE), 2.4)

    def psychic(d, t):
        cx, cy = W / 2, H * 0.45
        family(d, t, cx, cy)
        circle(d, cx, cy, 272, col(t, MAIN), 3.0)
        circle(d, cx, cy, 214, col(t, FINE), 2.2)
        ticks(d, cx, cy, 272, 24, 16, col(t, FINE), 2.2, a0=7.5)
        ticks(d, cx, cy, 310, 12, 24, col(t, FAINT + 8), 2.2)
        # 六棱晶核
        poly(d, ngon(cx, cy, 132, 6, rot=-90), col(t, MAIN), 3.0, fill=col(t, FAINT))
        poly(d, ngon(cx, cy, 78, 6, rot=-90), col(t, DOTS), 2.4)
        # 上下各一条能量脊
        line(d, [(cx, cy - 272), (cx, cy - 132)], col(t, FINE), 2.4)
        line(d, [(cx, cy + 132), (cx, cy + 272)], col(t, FINE), 2.4)
        # 漂浮碎晶
        for (x, y, r, rot) in ((152, 214, 46, 12), (620, 262, 36, -20), (184, 852, 32, 30), (600, 830, 42, -8)):
            poly(d, ngon(x, y, r, 4, rot=rot), col(t, DOTS), 2.2)
        for a in (18, 138, 258):
            arc(d, cx, cy, 348, a, a + 44, col(t, FAINT + 8), 2.4)

    def scroll(d, t):
        cx, cy = W / 2, H * 0.46
        family(d, t, cx, cy)
        top, bot = cy - 250, cy + 250
        # 卷纸（低的实色） + 中缝
        d.rounded_rectangle([S(cx - 258), S(top), S(cx + 258), S(bot)], radius=S(10),
                            fill=col(mix(t, (255, 255, 255), 0.10), 14),
                            outline=col(t, FINE), width=max(1, int(round(S(2.4)))))
        # 上下卷杆（实心胶囊）
        for y in (top, bot):
            d.rounded_rectangle([S(cx - 286), S(y - 30), S(cx + 286), S(y + 30)], radius=S(30),
                                fill=col(t, 44), outline=col(t, MAIN), width=max(1, int(round(S(2.6)))))
            for sx in (-286, 286):
                dcircle(d, cx + sx, y, 30, col(t, 64))
        # 星图：折线 + 节点
        stars = [(cx - 168, cy - 158), (cx - 44, cy - 196), (cx + 78, cy - 130),
                 (cx + 176, cy - 30), (cx + 96, cy + 84), (cx - 38, cy + 34),
                 (cx - 156, cy + 118), (cx - 34, cy + 198)]
        for i in range(len(stars) - 1):
            line(d, [stars[i], stars[i + 1]], col(t, FINE), 2.0)
        for p in stars:
            dcircle(d, p[0], p[1], 8, col(t, DOTS))
        # 星点
        rng = __import__("numpy").random.RandomState(7)
        for _ in range(60):
            x, y = rng.randint(60, W - 60), rng.randint(60, H - 60)
            r = float(rng.choice([1.8, 2.4, 3.0]))
            dcircle(d, x, y, r, col(t, DOTS - 22))

    def common(d, t):
        cx, cy = W / 2, H * 0.46
        family(d, t, cx, cy)
        for r, w, a in ((104, 2.6, MAIN), (196, 2.4, FINE), (286, 2.2, FINE), (338, 2.0, FAINT + 8)):
            circle(d, cx, cy, r, col(t, a), w)
        ticks(d, cx, cy, 286, 16, 22, col(t, FAINT + 10), 2.2, a0=11.25)
        ticks(d, cx, cy, 196, 8, 18, col(t, FAINT + 10), 2.6)
        # 四芒星（凹角）
        star = []
        for i in range(8):
            a = math.radians(-90 + i * 45)
            rr = 118 if i % 2 == 0 else 38
            star.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        poly(d, star, col(t, MAIN), 2.8, fill=col(t, FAINT))
        # 四角菱形钉
        for x in (104, W - 104):
            for y in (104, H - 104):
                poly(d, ngon(x, y, 18, 4, rot=-90), col(t, DOTS), 0, fill=col(t, DOTS - 22))

    def spell(d, t):
        cx, cy = W / 2, H * 0.46
        family(d, t, cx, cy)
        # 符文柱（外框 + 内框 + 上下封口）
        d.rounded_rectangle([S(cx - 138), S(cy - 336), S(cx + 138), S(cy + 336)], radius=S(28),
                            fill=col(t, 20), outline=col(t, MAIN), width=max(1, int(round(S(3.0)))))
        d.rounded_rectangle([S(cx - 100), S(cy - 296), S(cx + 100), S(cy + 296)], radius=S(20),
                            outline=col(t, FINE), width=max(1, int(round(S(2.2)))))
        # 柱内「抛置」符文：一行一格
        rng = __import__("numpy").random.RandomState(23)
        y = cy - 238
        while y < cy + 220:
            h = int(rng.choice([18, 24, 30]))
            w = int(rng.randint(36, 76))
            x = cx - w / 2
            if rng.rand() < 0.45:
                d.rectangle([S(x), S(y), S(x + w), S(y + h)], outline=col(t, DOTS - 24), width=max(1, int(round(S(2.0)))))
            elif rng.rand() < 0.5:
                line(d, [(x, y + h / 2), (x + w, y + h / 2)], col(t, DOTS - 24), 2.2)
            else:
                dcircle(d, cx, y + h / 2, h * 0.42, col(t, DOTS - 30))
            y += h + 30
        # 两侧刻度 + 上下导引
        for i in range(9):
            yy = cy - 288 + i * 72
            line(d, [(cx - 176, yy), (cx - 152, yy)], col(t, DOTS - 20), 2.4)
            line(d, [(cx + 152, yy), (cx + 176, yy)], col(t, DOTS - 20), 2.4)
        for dx in (-196, 0, 196):
            line(d, [(cx + dx, cy - 378), (cx + dx, cy - 356)], col(t, DOTS - 20), 2.4)
            line(d, [(cx + dx, cy + 356), (cx + dx, cy + 378)], col(t, DOTS - 20), 2.4)
        for (x, y, s) in ((126, 224, 30), (636, 268, 24), (142, 828, 22), (632, 806, 28)):
            poly(d, ngon(x, y, s, 4, rot=45), col(t, FINE), 2.2)

    return {"Abyss": abyss, "Blood": blood, "Mech": mech, "Psychic": psychic,
            "Scroll": scroll, "Common": common, "Spell": spell}
