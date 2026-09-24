# -*- coding: utf-8 -*-
"""择牌面板（Pick Draw）布局预览。
用真实卡面素材拼出：① 选择者视角（三张正面，选一张，其余打「明弃」）
② 旁观者视角（三张牌背 → 明弃的两张翻正面对其展示）。
画布取游戏参考分辨率 1920x1080，卡牌尺寸按 Player.Scale2DCard 的 x3（250x437）。
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
SRC = os.path.join(ROOT, "Tools/cardframe/preview/cardframe-v7-card.png")
OUT = os.path.join(ROOT, "Tools/cardframe/preview/pickdraw-panel.png")
FONT_BLACK = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf")
FONT_REG = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Regular.otf")

W, H = 1920, 1080           # CanvasScaler 参考分辨率
CARD_W, CARD_H = 250, 437   # 83.33x146.33 的预制体尺寸 x3
GAP = 0.16                  # 与 PickDrawUI.gapRatio 一致
DIM = (10, 10, 15, 173)     # 约等于 0.68 alpha 的压暗底


def load_pieces():
    im = Image.open(SRC).convert("RGBA")
    bg = im.getpixel((2, 2))
    w, h = im.size

    def not_bg(px):
        return abs(px[0] - bg[0]) + abs(px[1] - bg[1]) + abs(px[2] - bg[2]) > 24

    cols = []
    for x in range(w):
        hit = False
        for y in range(0, h, 7):
            if not_bg(im.getpixel((x, y))):
                hit = True
                break
        cols.append(hit)

    runs = []
    start = None
    for x in range(w):
        if cols[x] and start is None:
            start = x
        elif not cols[x] and start is not None:
            if x - start > 40:
                runs.append((start, x - 1))
            start = None
    if start is not None:
        runs.append((start, w - 1))
    assert len(runs) == 3, f"预期 3 段，实际 {len(runs)}: {runs}"

    def crop(run):
        x0, x1 = run
        sub = im.crop((x0, 0, x1 + 1, h))
        bbox = sub.getbbox()
        return sub.crop(bbox)

    return crop(runs[1]), crop(runs[2])   # 正面（带原画）、卡背


def fit(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def draw_face(front, back, is_back=False):
    return fit(back if is_back else front, CARD_W, CARD_H)


def text(d, xy, s, font, fill, anchor="mm"):
    d.text(xy, s, font=font, fill=fill, anchor=anchor)


def panel(front, back, mode):
    base = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(base)
    d.rectangle([0, 0, W - 1, H - 1], fill=DIM)

    f_title = ImageFont.truetype(FONT_BLACK, 46)
    f_hint = ImageFont.truetype(FONT_REG, 23)
    f_badge = ImageFont.truetype(FONT_BLACK, 32)

    if mode == "chooser":
        title, hint = "择　牌", "选择一张加入手牌　其余两张将对对方明弃"
    else:
        title, hint = "对方择牌", "对方已选定　未选中的两张对其明弃"

    # Unity 坐标 y 向上：标题在卡顶上方 108px、提示在上方 52px
    text(d, (W // 2, H // 2 - (CARD_H // 2 + 108)), title, f_title, (251, 246, 236, 255))
    text(d, (W // 2, H // 2 - (CARD_H // 2 + 52)), hint, f_hint, (216, 201, 170, 242))

    spacing = CARD_W * (1 + GAP)
    picked = 0  # 选择者选中的那张（示例：#0）
    for i in range(3):
        cx = int(W // 2 + (i - 1) * spacing)
        y0 = H // 2 - CARD_H // 2

        # 旁观者始终先看牌背；只有被明弃的那两张翻正面
        is_back = (mode == "spectator_idle") or (mode == "spectator_reveal" and i == picked)
        face = draw_face(front, back, is_back)

        if mode == "chooser":
            if i == picked:
                y0 -= 26
                face = face.resize((int(CARD_W * 1.10), int(CARD_H * 1.10)), Image.LANCZOS)
                cx2, y2 = cx - face.width // 2, y0 - (face.height - CARD_H) // 2
                base.alpha_composite(face, (cx2, y2))
            else:
                face = face.copy()
                face.putalpha(face.split()[3].point(lambda a: int(a * 0.42)))
                base.alpha_composite(face, (cx - CARD_W // 2, y0))
                bx0 = cx - int(CARD_W * 0.92) // 2
                by0 = y0 + int(CARD_H * 0.70)
                d.rounded_rectangle([bx0, by0, bx0 + int(CARD_W * 0.92), by0 + 66],
                                    radius=8, fill=(158, 41, 38, 240))
                text(d, (cx, by0 + 33), "明弃", f_badge, (255, 245, 235, 255))
        elif mode == "spectator_idle":
            base.alpha_composite(face, (cx - CARD_W // 2, y0))
        else:  # spectator_reveal：明弃的两张（#1 #2）翻正面
            revealed = i != picked
            base.alpha_composite(face, (cx - CARD_W // 2, y0))
            if revealed:
                bx0 = cx - int(CARD_W * 0.92) // 2
                by0 = y0 + int(CARD_H * 0.70)
                d.rounded_rectangle([bx0, by0, bx0 + int(CARD_W * 0.92), by0 + 66],
                                    radius=8, fill=(158, 41, 38, 240))
                text(d, (cx, by0 + 33), "明弃", f_badge, (255, 245, 235, 255))
    return base


def strip(label, img, f):
    lab = Image.new("RGBA", (W, 46), (16, 18, 24, 255))
    dd = ImageDraw.Draw(lab)
    dd.text((18, 23), label, font=f, fill=(232, 226, 210, 255), anchor="lm")
    out = Image.new("RGBA", (W, 46 + img.height), (16, 18, 24, 255))
    out.alpha_composite(lab, (0, 0))
    out.alpha_composite(img, (0, 46))
    return out


def main():
    front, back = load_pieces()
    f = ImageFont.truetype(FONT_BLACK, 26)

    parts = [
        strip("① 选择者：三张正面依次亮出 → 点一张加入手牌，其余两张打「明弃」", panel(front, back, "chooser"), f),
        strip("② 旁观者：同一位置只有三张牌背（不吃射线，不打断对方操作）", panel(front, back, "spectator_idle"), f),
        strip("③ 旁观者：对方选定后，被明弃的两张翻正面对其展示 → 随后弃掉（不再回牌库）", panel(front, back, "spectator_reveal"), f),
    ]
    total_h = sum(p.height for p in parts) + 12 * (len(parts) - 1)
    out = Image.new("RGBA", (W, total_h), (10, 12, 16, 255))
    y = 0
    for p in parts:
        out.alpha_composite(p, (0, y))
        y += p.height + 12
    out.convert("RGB").save(OUT, quality=95)
    print("OK", OUT, out.size)


main()
