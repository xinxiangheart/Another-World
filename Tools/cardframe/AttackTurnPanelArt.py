# -*- coding: utf-8 -*-
"""攻击回合伤害面板（AttackTurnDamagePanel）素材 —— Resources/UI/AttackTurn/*.png

形状按用户口述：左边一个显示头像的圆，右边一条填满的跑道型底，数字压在跑道底上。
材质语言与 HudButtonV1 / TopBarV1 同源：深蓝黑石面 + 金细线。

产物（全部落 Assets/_Game/Resources/UI/AttackTurn/）：
  TurnDmg_Plate.png       576x216  跑道底（9 宫格 border 108/54，pixelsPerUnit 200）
  TurnDmg_AvatarDisc.png  256x256  头像底盘（不透明圆盘，运行时当 Mask 图元）
  TurnDmg_AvatarRing.png  256x256  金环（中心透明，压在头像上面）
  TurnDmg_Orb.png         128x128  结算粒子（金珠 + 柔光）

尺寸口径（重要）：面板运行时是 292 x 108 UI 像素，高 = 头像圆直径 = 跑道圆头直径。
  9 宫格 border 在 UI 里的实际尺寸 = border_px * 100 / pixelsPerUnit，
  所以贴图 border 定 108/54、pixelsPerUnit 定 200 → 运行时正好 54/27：
  左右圆头各 54（= 108 高的半径），上下边条各 27，中段 54 纯平。改贴图尺寸时这三者要一起动。

9 宫格为什么安全：竖直剖面做成「顶 54px 渐变 → 中段纯平 → 底 54px 渐变」，
两处接缝的颜色与中段完全相等，纵向拉伸看不出台阶；中段不留任何横向细节。

生成：仓库根目录下  python Tools/cardframe/AttackTurnPanelArt.py
"""
import os
import math
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
OUTDIR = os.path.join(ROOT, "Assets/_Game/Resources/UI/AttackTurn")
PREVIEW = os.path.join(ROOT, "Tools/cardframe/preview")
PREVIEW_BG = os.path.join(ROOT, "Assets/_Game/Art/Sprites/Generated/preview-board-v5.png")

# 与 HudButtonV1 / TopBarV1 同一套色
INK    = (0x06, 0x09, 0x0E)
GOLD   = (0xC8, 0xA4, 0x4A)
GOLDL  = (0xE4, 0xCB, 0x84)
GOLDD  = (0x8A, 0x6F, 0x2E)
COOL   = (0x8E, 0xA2, 0xB4)
FACE_T = (0x1E, 0x29, 0x38)
FACE_M = (0x14, 0x1C, 0x28)
FACE_B = (0x09, 0x0E, 0x17)
CREAM  = (0xD6, 0xC2, 0x98)


def clip(c):
    return tuple(max(0, min(255, int(round(v)))) for v in c)


def mul(c, k, a=255):
    return clip((c[0] * k, c[1] * k, c[2] * k)) + (int(a),)


def rr(d, i0, i1, rad, fill, size):
    """一圈圆角矩形环：inset i0 填满，再用 inset i1 抠空。"""
    w, h = size
    d.rounded_rectangle([i0, i0, w - 1 - i0, h - 1 - i0], radius=rad, fill=fill)
    if i1 is not None:
        d.rounded_rectangle([i1, i1, w - 1 - i1, h - 1 - i1],
                            radius=max(0.0, rad - (i1 - i0)), fill=(0, 0, 0, 0))


# ══════════════════════════════════════════════════════════════════
# 1. 跑道底（按 2x 出图：贴图 216 高 → 运行时 108）
# ══════════════════════════════════════════════════════════════════
DESIGN_H = 384              # 设计稿高度（下面所有像素都按这个高度写）
DESIGN_W = 1024
SCALE = 0.5625              # 设计稿 → 出货 = 216/384
OUT_W, OUT_H = int(DESIGN_W * SCALE), int(DESIGN_H * SCALE)      # 576 x 216
BAND = int(96 * SCALE)      # 上下边条 54
BORDER = (int(192 * SCALE), BAND, int(192 * SCALE), BAND)        # 108 / 54
PPU = 200                   # 让 border 在 UI 里显示成 54 / 27（= 100/PPU 的系数）
SS = 2                      # 设计稿超采样


def build_plate():
    W, H = DESIGN_W, DESIGN_H
    w, h = W * SS, H * SS
    rows = np.zeros((h, 3), np.float32)
    t_top = np.array(FACE_T, np.float32)
    t_mid = np.array(FACE_M, np.float32)
    t_bot = np.array(FACE_B, np.float32)
    band = 96
    for y in range(h):
        yy = y / float(SS)
        if yy < band:
            rows[y] = t_top + (t_mid - t_top) * (yy / float(band))
        elif yy > H - band:
            k = (yy - (H - band)) / float(band)
            rows[y] = t_mid + (t_bot - t_mid) * k
        else:
            rows[y] = t_mid
    arr = np.repeat(rows[:, None, :], w, axis=1).astype(np.uint8)
    face = Image.fromarray(arr, "RGB").convert("RGBA")

    R = (H / 2.0) * SS
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, h - 1], radius=R, fill=255)
    face.putalpha(mask)

    k = SS
    rim = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(rim)
    # 外柔边 → 金亮线 → 金 → 金暗 → 墨线 → 内侧淡亮线（设计稿像素）
    rr(d, 0,      k * 3,  R,          mul(INK, 1.0, 140), (w, h))
    rr(d, k * 3,  k * 8,  R - k * 3,  GOLDL + (238,), (w, h))
    rr(d, k * 8,  k * 14, R - k * 8,  GOLD + (216,), (w, h))
    rr(d, k * 14, k * 17, R - k * 14, GOLDD + (176,), (w, h))
    rr(d, k * 17, k * 20, R - k * 17, INK + (205,), (w, h))
    rr(d, k * 20, k * 23, R - k * 20, CREAM + (40,), (w, h))

    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.alpha_composite(face)
    out.alpha_composite(rim)
    # 外圈一点同色辉光，把底板从战场上托起来（很轻，不抢卡面）
    a = np.asarray(out).astype(np.float32)
    g = np.zeros_like(a)
    g[:, :, 0], g[:, :, 1], g[:, :, 2] = GOLD
    g[:, :, 3] = np.clip(a[:, :, 3] * 0.30, 0, 255)
    glow = Image.fromarray(g.astype(np.uint8)).filter(ImageFilter.GaussianBlur(3.0 * SS))
    res = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    res.alpha_composite(glow)
    res.alpha_composite(out)
    return res.resize((W, H), Image.LANCZOS).resize((OUT_W, OUT_H), Image.LANCZOS)


# ══════════════════════════════════════════════════════════════════
# 2. 头像底盘 / 金环（运行时画成 108x108，2.4x 出图够用）
# ══════════════════════════════════════════════════════════════════
AW = 256
AS = 4
ACX = ACY = AW / 2.0
AR = AW / 2.0


def av_box(r):
    return [(ACX - r) * AS, (ACY - r) * AS, (ACX + r) * AS, (ACY + r) * AS]


def build_avatar_disc():
    w = AW * AS
    y, x = np.mgrid[0:w, 0:w].astype(np.float32)
    dx = (x - ACX * AS) / (AR * AS)
    dy = (y - ACY * AS) / (AR * AS)
    d = np.sqrt(dx * dx + dy * dy)
    lit = np.clip(1.0 - 0.42 * np.clip(d, 0, 1) ** 1.5 - 0.10 * (dy + 1) * 0.5, 0, 1)
    base = np.array(FACE_M, np.float32)[None, None, :] * lit[:, :, None]
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB").convert("RGBA")

    mask = Image.new("L", (w, w), 0)
    ImageDraw.Draw(mask).ellipse(av_box(AR), fill=255)
    img.putalpha(mask)

    lay = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    d2 = ImageDraw.Draw(lay)

    def ring(i0, i1, col):
        d2.ellipse(av_box(AR - i0 / AS), fill=col)
        d2.ellipse(av_box(max(0.0, AR - i1 / AS)), fill=(0, 0, 0, 0))

    ring(0.0, 5.0, INK + (215,))
    ring(5.0, 7.5, GOLDD + (190,))

    sh = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sh)
    sd.ellipse(av_box(AR - 6.0 / AS), fill=(0, 0, 0, 120))
    sd.ellipse(av_box(AR - 20.0 / AS), fill=(0, 0, 0, 0))
    sh = sh.filter(ImageFilter.GaussianBlur(6.0 * AS))
    inner = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    inner.paste(sh, (0, 0), mask)

    out = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    out.alpha_composite(img)
    out.alpha_composite(inner)
    out.alpha_composite(lay)
    return out.resize((AW, AW), Image.LANCZOS)


def build_avatar_ring():
    w = AW * AS
    lay = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)

    def band(r0, r1, col):
        d.ellipse(av_box(AR * r1), fill=col)
        d.ellipse(av_box(AR * r0), fill=(0, 0, 0, 0))

    band(0.845, 0.870, INK + (190,))
    band(0.870, 0.905, GOLDD + (205,))
    band(0.905, 0.945, GOLD + (232,))
    band(0.945, 0.975, GOLDL + (245,))
    band(0.975, 1.000, INK + (225,))
    d.arc(av_box(AR * 0.94), start=-72, end=6, fill=GOLDL + (255,), width=int(0.022 * AR * AS))
    out = lay.filter(ImageFilter.GaussianBlur(0.4 * AS))
    return out.resize((AW, AW), Image.LANCZOS)


# ══════════════════════════════════════════════════════════════════
# 3. 结算粒子
# ══════════════════════════════════════════════════════════════════
OW = 128
OS = 4


def build_orb():
    w = OW * OS
    y, x = np.mgrid[0:w, 0:w].astype(np.float32)
    c = w / 2.0
    d = np.clip(np.sqrt((x - c) ** 2 + (y - c) ** 2) / (w / 2.0), 0, 1)
    core = np.clip(1.0 - d / 0.30, 0, 1) ** 0.8
    halo = np.clip(1.0 - d, 0, 1) ** 2.2
    rgb = (np.array(GOLDL, np.float32)[None, None, :] * core[:, :, None]
           + np.array(GOLD, np.float32)[None, None, :] * (halo * 0.85)[:, :, None])
    rgb = np.clip(rgb, 0, 255)
    a = np.clip(core * 255 + halo * 165, 0, 255)
    img = np.dstack([rgb, a[:, :, None]]).astype(np.uint8)
    return Image.fromarray(img, "RGBA").resize((OW, OW), Image.LANCZOS)


# ══════════════════════════════════════════════════════════════════
# 安装
# ══════════════════════════════════════════════════════════════════
META_TMPL = open(os.path.join(ROOT, "Tools/cardframe/_meta_tmpl.txt"), "r", encoding="utf-8").read()
DEFAULT_BORDER = "  spriteBorder: {x: 32, y: 32, z: 32, w: 32}"
DEFAULT_PPU = "  spritePixelsToUnits: 100"


def install(img, name, border=None, ppu=None):
    os.makedirs(OUTDIR, exist_ok=True)
    dst = os.path.join(OUTDIR, name)
    img.save(dst)
    meta = dst + ".meta"
    guid = None
    if os.path.exists(meta):
        for line in open(meta, "r", encoding="utf-8"):
            if line.startswith("guid:"):
                guid = line.split(":", 1)[1].strip()
                break
    if not guid:
        guid = "".join(random.choice("0123456789abcdef") for _ in range(32))
    src = META_TMPL.replace("__GUID__", guid)
    if border is not None:
        src = src.replace(DEFAULT_BORDER,
                          "  spriteBorder: {x: %d, y: %d, z: %d, w: %d}" % border)
    if ppu is not None:
        src = src.replace(DEFAULT_PPU, "  spritePixelsToUnits: %s" % ppu)
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(src)
    print("installed", name, img.size, "border=%s ppu=%s" % (border, ppu))


# ══════════════════════════════════════════════════════════════════
# 预览（模拟实机：1920x1080 的战场上贴两块面板）
# ══════════════════════════════════════════════════════════════════
PANEL_W, PANEL_H = 292, 108


def plate_sliced(plate, w, h):
    """按 Unity 的 9 宫格口径合成：左右圆头 54、上下边条 27，中段拉伸。"""
    bl, bt = BORDER[0], BORDER[1]                      # 裁切用：贴图像素
    dbl = int(round(bl * 100.0 / PPU))                 # 绘制用：UI 像素
    dbt = int(round(bt * 100.0 / PPU))
    src_w, src_h = plate.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    # 四角
    out.paste(plate.crop((0, 0, bl, bt)).resize((dbl, dbt), Image.LANCZOS), (0, 0))
    out.paste(plate.crop((src_w - bl, 0, src_w, bt)).resize((dbl, dbt), Image.LANCZOS), (w - dbl, 0))
    out.paste(plate.crop((0, src_h - bt, bl, src_h)).resize((dbl, dbt), Image.LANCZOS), (0, h - dbt))
    out.paste(plate.crop((src_w - bl, src_h - bt, src_w, src_h)).resize((dbl, dbt), Image.LANCZOS), (w - dbl, h - dbt))
    # 上下边条
    top = plate.crop((bl, 0, src_w - bl, bt)).resize((w - 2 * dbl, dbt), Image.LANCZOS)
    bot = plate.crop((bl, src_h - bt, src_w - bl, src_h)).resize((w - 2 * dbl, dbt), Image.LANCZOS)
    out.paste(top, (dbl, 0))
    out.paste(bot, (dbl, h - dbt))
    # 左右边条
    lef = plate.crop((0, bt, bl, src_h - bt)).resize((dbl, h - 2 * dbt), Image.LANCZOS)
    rig = plate.crop((src_w - bl, bt, src_w, src_h - bt)).resize((dbl, h - 2 * dbt), Image.LANCZOS)
    out.paste(lef, (0, dbt))
    out.paste(rig, (w - dbl, dbt))
    # 中段
    mid = plate.crop((bl, bt, src_w - bl, src_h - bt)).resize((w - 2 * dbl, h - 2 * dbt), Image.LANCZOS)
    out.paste(mid, (dbl, dbt))
    return out


def build_preview():
    from PIL import ImageFont
    W, H = 1920, 1080
    if os.path.exists(PREVIEW_BG):
        bg = Image.open(PREVIEW_BG).convert("RGBA").resize((W, H), Image.LANCZOS)
    else:
        bg = Image.new("RGBA", (W, H), (12, 14, 20, 255))
    bg = Image.blend(bg, Image.new("RGBA", (W, H), (0, 0, 0, 255)), 0.32)

    plate = Image.open(os.path.join(OUTDIR, "TurnDmg_Plate.png")).convert("RGBA")
    disc = Image.open(os.path.join(OUTDIR, "TurnDmg_AvatarDisc.png")).convert("RGBA")
    ring = Image.open(os.path.join(OUTDIR, "TurnDmg_AvatarRing.png")).convert("RGBA")
    orb = Image.open(os.path.join(OUTDIR, "TurnDmg_Orb.png")).convert("RGBA")

    font = None
    for path in (r"C:\Windows\Fonts\NotoSerifSC-Black.otf",
                 r"C:\Windows\Fonts\msyhbd.ttc",
                 r"C:\Windows\Fonts\simhei.ttf"):
        if os.path.exists(path):
            try:
                font = ImageFont.truetype(path, 62)
                break
            except Exception:
                pass
    if font is None:
        font = ImageFont.load_default()

    def text(cx, cy, s, size, col, outline=(16, 6, 6), f=None, anchor="mm"):
        f = f or font
        d = ImageDraw.Draw(bg)
        for ox in (-2, -1, 0, 1, 2):
            for oy in (-2, -1, 0, 1, 2):
                if ox or oy:
                    d.text((cx + ox, cy + oy), s, font=f, fill=outline + (255,), anchor=anchor)
        d.text((cx, cy), s, font=f, fill=col + (255,), anchor=anchor)

    def panel(cx, cy, ringcol, alpha=255):
        p = plate_sliced(plate, PANEL_W, PANEL_H)
        dd = disc.resize((PANEL_H, PANEL_H), Image.LANCZOS)
        face = Image.new("RGBA", (PANEL_H, PANEL_H), (0, 0, 0, 0))
        fd = ImageDraw.Draw(face)
        fd.ellipse([PANEL_H * 0.10, PANEL_H * 0.10, PANEL_H * 0.90, PANEL_H * 0.90], fill=ringcol + (255,))
        fd.polygon([(PANEL_H * 0.50, PANEL_H * 0.22), (PANEL_H * 0.78, PANEL_H * 0.86), (PANEL_H * 0.22, PANEL_H * 0.86)],
                   fill=(26, 30, 40, 255))
        m = Image.new("L", (PANEL_H, PANEL_H), 0)
        ImageDraw.Draw(m).ellipse([0, 0, PANEL_H - 1, PANEL_H - 1], fill=255)
        face.putalpha(m)
        comp = Image.new("RGBA", (PANEL_H, PANEL_H), (0, 0, 0, 0))
        comp.alpha_composite(dd)
        comp.alpha_composite(face)
        comp.alpha_composite(ring.resize((PANEL_H, PANEL_H), Image.LANCZOS))
        if alpha < 255:
            comp.putalpha(comp.getchannel("A").point(lambda v: v * alpha // 255))
            p.putalpha(p.getchannel("A").point(lambda v: v * alpha // 255))
        out = Image.new("RGBA", (PANEL_W, PANEL_H), (0, 0, 0, 0))
        out.alpha_composite(p)
        out.alpha_composite(comp)
        bg.alpha_composite(out, (int(cx - PANEL_W / 2), int(cy - PANEL_H / 2)))

    CX = 22 + PANEL_W // 2
    # 与 AttackTurnDamagePanel 的默认位（opponentPos 260 / selfPos 140，相对屏幕竖直中线）对齐
    panel(CX, 280, (94, 132, 196))
    text(CX + 56, 280, "12", 62, (255, 96, 86))
    panel(CX, 400, (196, 152, 84))
    text(CX + 56, 400, "7", 62, (255, 96, 86))

    # 正在飞向面板的伤害数字
    text(820, 470, "-3", 54, (255, 96, 86))
    # 结算粒子：从面板飞向左下生命值
    o = orb.resize((46, 46), Image.LANCZOS)
    bg.alpha_composite(o, (250, 560))
    bg.alpha_composite(orb.resize((26, 26), Image.LANCZOS), (210, 660))

    out = os.path.join(PREVIEW, "attackturn-panel.png")
    bg.convert("RGB").save(out)
    print("preview", out)


def main():
    install(build_plate(), "TurnDmg_Plate.png", border=BORDER, ppu=PPU)
    install(build_avatar_disc(), "TurnDmg_AvatarDisc.png")
    install(build_avatar_ring(), "TurnDmg_AvatarRing.png")
    install(build_orb(), "TurnDmg_Orb.png")
    build_preview()


if __name__ == "__main__":
    main()
