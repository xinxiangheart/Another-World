# -*- coding: utf-8 -*-
"""验证浮字落点：按真实棋盘几何（100px/世界单位）画四排槽位，标出改前 / 改后的数字位置。"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = r"C:\Users\22589\Documents\GitHub\Another-World"
FONTS = os.path.join(ROOT, "Assets/_Game/Fonts")
OUT = os.path.join(ROOT, "Tools/cardframe/preview/floater-anchor.png")
UI = lambda s: ImageFont.truetype(os.path.join(FONTS, "NotoSerifCJKsc-Black.otf"), s)

K = 100.0            # px / 世界单位（卡高 1.78 → 178px）
CW, CH = 1.0 * K, 1.776 * K
ROWS = [(2.3, "敌方前排"), (4.6, "敌方后排"), (-0.27, "己方前排"), (-2.57, "己方后排")]
XS = [-3.0, 0.0, 3.0]

W, H = 1280, 900
c = Image.new("RGB", (W, H), (17, 19, 25))
d = ImageDraw.Draw(c)
ox, oy = 190, 830        # 世界原点(0,0) 落在画布上的位置（y 向上）


def px(x, y):
    return (ox + x * K, oy - y * K)


d.text((30, 20), "伤害浮字落点：按真实棋盘几何（1 世界单位 = 100px，卡牌 100×178px）", font=UI(26), fill=(238, 233, 222))
for ry, name in ROWS:
    for x in XS:
        cx, cy = px(x, ry)
        d.rectangle([cx - CW / 2, cy - CH / 2, cx + CW / 2, cy + CH / 2],
                    fill=(30, 33, 42), outline=(96, 101, 116), width=2)
    d.text((30, px(0, ry)[1] - 12), name, font=UI(19), fill=(140, 145, 158))

# 被打的那张：己方前排中位
tx, ty = 0.0, -0.27
cx, cy = px(tx, ty)
d.rectangle([cx - CW / 2, cy - CH / 2, cx + CW / 2, cy + CH / 2], outline=(214, 180, 96), width=3)


def number(text, wx, wy, face, rim, size):
    pad = size
    box = Image.new("RGBA", (size * 4, size * 2), (0, 0, 0, 0))
    f = UI(size)
    bx, by = box.size[0] // 2, box.size[1] // 2
    sh = Image.new("RGBA", box.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).text((bx + 3, by + 3), text, font=f, fill=(0, 0, 0, 205), anchor="mm", stroke_width=5, stroke_fill=(0, 0, 0, 205))
    box.alpha_composite(sh.filter(ImageFilter.GaussianBlur(2.4)))
    ImageDraw.Draw(box).text((bx, by), text, font=f, fill=face, anchor="mm", stroke_width=5, stroke_fill=rim)
    box = box.crop(box.getbbox())
    gx, gy = px(wx, wy)
    c.paste(box, (int(gx - box.size[0] / 2), int(gy - box.size[1] / 2)), box)


# 改前：卡中心 + 2.5 世界单位
number("-7", tx, ty + 2.5, (255, 107, 105, 255), (77, 17, 15, 255), 58)
d.line([cx, cy - CH / 2, cx, px(tx, ty + 2.5)[1]], fill=(196, 92, 88), width=2)
d.text((cx + 14, px(tx, ty + 2.5)[1] - 12), "改前：中心 +2.5 → 顶边再往上 1.6（≈ 后一排的格子里）",
       font=UI(20), fill=(222, 130, 126))

# 改后：包围盒顶边 + 0.42
ty2 = ty + 1.776 / 2 + 0.42
number("-7", tx + 1.35, ty2, (255, 107, 105, 255), (77, 17, 15, 255), 58)
d.line([px(tx + 1.35, ty2)[0] - 8, px(tx + 1.35, ty2)[1], px(tx + 1.35, ty2)[0] - 60, px(tx + 1.35, ty2)[1]],
       fill=(120, 200, 130), width=1)
d.text((px(tx + 1.35, ty2)[0] - 66, px(tx + 1.35, ty2)[1] - 34), "改后", font=UI(20), fill=(130, 210, 140))
d.text((px(tx + 1.35, ty2)[0] - 66, px(tx + 1.35, ty2)[1] - 12), "顶边+0.42", font=UI(20), fill=(130, 210, 140))
d.text((30, 60), "卡中心 +2.5 个单位 = 顶边再往上 1.6，比一整排间距（2.3）还夸张 → 数字落在后一排身上，看不出是从哪张卡弹的。", font=UI(20), fill=(150, 146, 136))
d.text((30, 86), "改后按模型包围盒取「顶边 + 0.42」：数字底边离卡顶约 12px，两排之间还剩余量，Z 用模型中心的 Z。", font=UI(20), fill=(150, 146, 136))
c.save(OUT)
print("ok", OUT)
