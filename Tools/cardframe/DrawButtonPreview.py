# -*- coding: utf-8 -*-
"""抽牌按钮数字的三态预览：常规 / 有择牌机会（闪金） / 机会归零（灰） / 点击瞬间（跳一下）。
用真实素材：DrawCircle.png 按钮贴图 + NotoSerifCJKsc-Black（与场景里的 TMP 字体同源）。"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
BTN = os.path.join(ROOT, "Assets/_Game/Resources/UI/HUD/DrawCircle.png")
FONT = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf")
OUT = os.path.join(ROOT, "Tools/cardframe/preview/drawbutton-states.png")

W, H = 1920, 1080
BTN_W, BTN_H = 180, 120        # 场景里 Image 的 SizeDelta
SCALE = 3                      # 放大看
FIT = 0.47                     # 每个面板最多并排 3 个按钮，整体缩到能放下
FS = 54 * SCALE * FIT * 0.55   # 场景 fontSize 54（auto-size），这里取视觉近似

IDLE = (253, 248, 240, 255)
GOLD_DIM = (200, 150, 45, 255)
GOLD_LIT = (255, 236, 150, 255)
GREY = (115, 115, 122, 255)


def btn_sprite():
    im = Image.open(BTN).convert("RGBA")
    return im.resize((BTN_W * SCALE, BTN_H * SCALE), Image.LANCZOS)


def panel(base, x0, y0, w, h, title, sub, states):
    """states: [(数字, 颜色, 数字缩放)]，在面板里并排画。
    按钮贴图恒定不缩放 —— 代码里只缩 drawCountText.transform，美术不动。"""
    d = ImageDraw.Draw(base)
    d.rectangle([x0, y0, x0 + w - 1, y0 + h - 1], fill=(16, 18, 24, 255), outline=(48, 50, 60, 255))
    f_t = ImageFont.truetype(FONT, 30)
    f_s = ImageFont.truetype(FONT, 20)

    sprite = btn_sprite()
    sprite = sprite.resize((int(sprite.width * FIT), int(sprite.height * FIT)), Image.LANCZOS)
    n = len(states)
    slot = w / n
    for i, (num, col, ts) in enumerate(states):
        cx = x0 + slot * (i + 0.5)
        cy = y0 + h * 0.62
        base.alpha_composite(sprite, (int(cx - sprite.width / 2), int(cy - sprite.height / 2)))
        f_num = ImageFont.truetype(FONT, max(8, int(FS * ts)))
        d.text((cx, cy + 4), num, font=f_num, fill=col, anchor="mm")

    # 标签最后画，压在按钮之上
    d.text((x0 + 24, y0 + 20), title, font=f_t, fill=(240, 234, 220, 255), anchor="lm")
    d.text((x0 + 24, y0 + 54), sub, font=f_s, fill=(170, 165, 150, 255), anchor="lm")


def main():
    base = Image.new("RGBA", (W, H), (10, 12, 16, 255))
    d = ImageDraw.Draw(base)
    f = ImageFont.truetype(FONT, 36)
    d.text((60, 46), "抽牌按钮 · 数字四态（美术不动，只有文字变）", font=f, fill=(240, 234, 220, 255), anchor="lm")

    pad, gw, gh = 40, (W - 60 * 3) // 2, (H - 200) // 2
    x_left, x_right = 60, 60 + gw + 60
    y_top, y_bot = 110, 110 + gh + 40

    panel(base, x_left, y_top, gw, gh,
          "① 常规（没有择牌机会）", "数字保持预制体原色",
          [("5", IDLE, 1.0)])

    panel(base, x_right, y_top, gw, gh,
          "② 有择牌机会 → 闪亮亮的金色", "在暗金 ↔ 亮金之间呼吸（goldShineCycle 0.9s）",
          [("5", GOLD_DIM, 1.0), ("5", GOLD_LIT, 1.0)])

    panel(base, x_left, y_bot, gw, gh,
          "③ 抽牌机会归零 → 灰（优先于金）", "remainingDraws <= 0",
          [("1", GREY, 1.0), ("0", GREY, 1.0)])

    panel(base, x_right, y_bot, gw, gh,
          "④ 点击抽牌：只有数字文本跳一下", "按钮贴图不动 · 以字形中心为原点放大 → 最高点换数 → 落回",
         [("5", IDLE, 1.0), ("5", IDLE, 1.5), ("4", IDLE, 1.0)])

    base.convert("RGB").save(OUT, quality=95)
    print("OK", OUT, base.size)


main()
