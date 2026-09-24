# -*- coding: utf-8 -*-
"""攻击回合伤害面板 —— 四段分解图（预览用，不出货素材）。

复用 AttackTurnPanelArt.py 里的跑道底 / 头像圆 / 金环 / 粒子与 9 宫格口径，
把一整轮演出按四步画出来：跳出 → 飞进面板累加 → 相减只留受击方 → 粒子飞向生命值扣血弹字。
输出 Tools/cardframe/preview/attackturn-panel-steps.png。
"""
import os
from PIL import Image, ImageDraw, ImageFont
import AttackTurnPanelArt as art

S = 0.75                                  # 画面缩放（面板 219x81，9 宫格边条仍成立）
FW, FH, CAP = int(1920 * S), int(1080 * S), 36
PW, PH = int(art.PANEL_W * S), int(art.PANEL_H * S)
PX = int(168 * S)
PY_TOP, PY_BOT = int(280 * S), int(400 * S)   # 与 AttackTurnDamagePanel 默认位一致（屏幕坐标）
NOTCH = 34

CAPTIONS = [
    "① 伤害数字从攻击者身上跳出，到最高点停住",
    "② 数字飞进对应面板，落地时面板数字跳一下",
    "③ 结算：两个数字相减，只留掉血的那一方",
    "④ 从面板飞出一粒子到生命值，到达才扣血弹字",
]


def load_font(size):
    for path in (r"C:\Windows\Fonts\NotoSerifSC-Bold.otf",
                 r"C:\Windows\Fonts\msyhbd.ttc",
                 r"C:\Windows\Fonts\simhei.ttf"):
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size)
            except Exception:
                pass
    return ImageFont.load_default()


def main():
    f_cap = load_font(20)
    f_small = load_font(17)
    f_num = load_font(33)
    f_big = load_font(30)

    def num(f, cx, cy, s, font, a=255, col=(255, 96, 86)):
        d = ImageDraw.Draw(f)
        for ox in (-2, -1, 0, 1, 2):
            for oy in (-2, -1, 0, 1, 2):
                if ox or oy:
                    d.text((cx + ox, cy + oy), s, font=font, fill=(16, 6, 6, a), anchor="mm")
        d.text((cx, cy), s, font=font, fill=col + (a,), anchor="mm")

    def load(name):
        return Image.open(os.path.join(art.OUTDIR, name)).convert("RGBA")

    plate, disc, ring, orb = (load("TurnDmg_Plate.png"), load("TurnDmg_AvatarDisc.png"),
                              load("TurnDmg_AvatarRing.png"), load("TurnDmg_Orb.png"))

    if os.path.exists(art.PREVIEW_BG):
        board = Image.open(art.PREVIEW_BG).convert("RGBA").resize((FW, FH), Image.LANCZOS)
    else:
        board = Image.new("RGBA", (FW, FH), (12, 14, 20, 255))

    def new_frame():
        f = Image.blend(board.copy(), Image.new("RGBA", (FW, FH), (0, 0, 0, 255)), 0.45)
        ImageDraw.Draw(f).rectangle(
            [PX - PW // 2 - 10, PY_TOP - PH // 2 - 16, PX + PW // 2 + 10, PY_BOT + PH // 2 + 16],
            outline=(200, 164, 74, 80), width=2)
        return f

    def panel(f, cy, col, alpha=255, value=None, va=255):
        p = art.plate_sliced(plate, PW, PH)
        c = Image.new("RGBA", (PH, PH), (0, 0, 0, 0))
        face = Image.new("RGBA", (PH, PH), (0, 0, 0, 0))
        fd = ImageDraw.Draw(face)
        fd.ellipse([PH * 0.10, PH * 0.10, PH * 0.90, PH * 0.90], fill=col + (255,))
        fd.polygon([(PH * 0.5, PH * 0.2), (PH * 0.8, PH * 0.88), (PH * 0.2, PH * 0.88)], fill=(26, 30, 40, 255))
        m = Image.new("L", (PH, PH), 0)
        ImageDraw.Draw(m).ellipse([0, 0, PH - 1, PH - 1], fill=255)
        face.putalpha(m)
        c.alpha_composite(disc.resize((PH, PH), Image.LANCZOS))
        c.alpha_composite(face)
        c.alpha_composite(ring.resize((PH, PH), Image.LANCZOS))
        out = Image.new("RGBA", (PW, PH), (0, 0, 0, 0))
        out.alpha_composite(p)
        out.alpha_composite(c)
        if alpha < 255:
            out.putalpha(out.getchannel("A").point(lambda v: v * alpha // 255))
        f.alpha_composite(out, (PX - PW // 2, int(cy - PH / 2)))
        if value is not None:
            num(f, PX + int(PW * 0.20), cy - 1, value, f_small, a=va)

    def cap(f, s):
        d = ImageDraw.Draw(f)
        d.rectangle([0, FH - CAP, FW, FH], fill=(8, 10, 14, 235))
        d.text((14, FH - CAP + 9), s, font=f_cap, fill=(214, 194, 152, 255))

    frames = []

    f = new_frame()
    panel(f, PY_TOP, (94, 132, 196), value="12")
    panel(f, PY_BOT, (196, 152, 84), value="7")
    num(f, int(470 * S), int(185 * S), "-3", f_big)
    cap(f, CAPTIONS[0]); frames.append(f)

    f = new_frame()
    panel(f, PY_TOP, (94, 132, 196), value="15")
    panel(f, PY_BOT, (196, 152, 84), value="7")
    num(f, int(305 * S), int(170 * S), "-3", f_big, a=205)
    cap(f, CAPTIONS[1]); frames.append(f)

    f = new_frame()
    panel(f, PY_TOP, (94, 132, 196), value="8")
    panel(f, PY_BOT, (196, 152, 84), alpha=105, value="3", va=105)
    cap(f, CAPTIONS[2]); frames.append(f)

    f = new_frame()
    panel(f, PY_TOP, (94, 132, 196), value="5")
    f.alpha_composite(orb.resize((int(34 * S), int(34 * S)), Image.LANCZOS),
                      (int(232 * S), int(322 * S)))
    f.alpha_composite(orb.resize((int(20 * S), int(20 * S)), Image.LANCZOS),
                      (int(186 * S), int(398 * S)))
    ImageDraw.Draw(f).rounded_rectangle(
        [int(40 * S), int(470 * S), int(300 * S), int(512 * S)], radius=6,
        fill=(18, 22, 30, 235), outline=(200, 164, 74, 190), width=2)
    num(f, int(118 * S), int(491 * S), "-5", f_small)
    cap(f, CAPTIONS[3]); frames.append(f)

    out = Image.new("RGB", (FW * 2, FH * 2), (10, 12, 16))
    for i, fr in enumerate(frames):
        out.paste(fr.convert("RGB"), ((i % 2) * FW, (i // 2) * FH))
    dst = os.path.join(art.PREVIEW, "attackturn-panel-steps.png")
    out.save(dst)
    print("storyboard", dst, out.size)


main()