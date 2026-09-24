# -*- coding: utf-8 -*-
"""HUD 圆形按钮 v1 —— 预览
出两张：preview/hud-btn-v1.png（1:1 + 3x + 老图对照）、preview/hud-btn-v1-insitu.png（实机原位对照）
"""
import os, math
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TARGET = os.path.join(ROOT, "Assets/_Game/Resources/UI/HUD")
ARCHIVE = os.path.join(ROOT, "Assets/_Game/Art/Sprites/Generated/hud-btn-v1")
OLD = os.path.join(os.environ.get("TEMP", "."), "hudbtn-backup-20260924")
SHOT = os.path.join(os.environ.get("TEMP", "."), "codex-clipboard-b6a8575e-28d4-4052-a852-461aae5990d4.png")
PREVIEW = os.path.join(ROOT, "Tools/cardframe/preview")
FONT = os.path.join(ROOT, "Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf")
BG = (0x11, 0x16, 0x1D)
PANEL = (0x1A, 0x1F, 0x27)
INK = (0x06, 0x09, 0x0E)
NODE_W, NODE_H = 180, 120


def render(path, w=NODE_W, h=NODE_H, digit=None):
    im = Image.open(path).convert("RGBA").resize((w, h), Image.LANCZOS)
    if digit is not None:
        d = ImageDraw.Draw(im)
        f = ImageFont.truetype(FONT, int(41 * h / NODE_H))
        bb = d.textbbox((0, 0), digit, font=f)
        cx = w / 2.0 - (bb[0] + bb[2]) / 2.0
        cy = h / 2.0 - (bb[1] + bb[3]) / 2.0
        mask = Image.new("L", (w + 24, h + 24), 0)
        ImageDraw.Draw(mask).text((cx + 12, cy + 12), digit, font=f, fill=255)
        layer = Image.new("RGBA", (w + 24, h + 24), (0, 0, 0, 0))
        layer.paste(Image.new("RGBA", layer.size, INK + (255,)), (0, 0), mask.filter(ImageFilter.MaxFilter(5)))
        layer.paste(Image.new("RGBA", layer.size, (0xFD, 0xF8, 0xF0, 255)), (0, 0), mask)
        im.alpha_composite(layer, (-12, -12))
    return im


def cell(sheet, x, y, img, label):
    d = ImageDraw.Draw(sheet)
    d.rectangle([x - 8, y - 8, x + img.width + 8, y + img.height + 8], fill=PANEL)
    sheet.alpha_composite(img, (x, y))
    d.text((x - 4, y + img.height + 12), label, fill=(0xC8, 0xA4, 0x4A))


def sheet_main():
    rows = [("抽牌 DrawCircle.png", "DrawCircle.png"), ("隐藏手牌 EyeCircle.png", "EyeCircle.png")]
    cw, ch = NODE_W * 3 + 40, NODE_H * 3 + 40
    sheet = Image.new("RGBA", (1500, 60 + len(rows) * (ch + 80)), BG + (255,))
    d = ImageDraw.Draw(sheet)
    d.text((20, 18), "HUD 圆形按钮 v1 —— 1:1 实机尺寸(180x120) + 3x；带数字 = 数字压在盘心的实际观感", fill=(0xE4, 0xCB, 0x84))
    for i, (title, name) in enumerate(rows):
        y = 60 + i * (ch + 80)
        d.text((20, y - 4), title, fill=(0xE4, 0xCB, 0x84))
        digit = "5" if "Draw" in name else None
        cell(sheet, 30, y + 26, render(os.path.join(OLD, name), digit=digit), "老图 1:1")
        cell(sheet, 30 + cw, y + 26, render(os.path.join(TARGET, name), digit=digit), "新图 1:1")
        cell(sheet, 30 + 2 * cw, y + 26,
             render(os.path.join(TARGET, name), NODE_W * 3, NODE_H * 3, digit=digit), "新图 3x")
    out = os.path.join(PREVIEW, "hud-btn-v1.png")
    sheet.convert("RGB").save(out)
    return out


def sheet_insitu():
    """把新/老图按场景坐标贴回真实截图：抽牌 (810,-420)、隐藏 (808.5,-253.5)，节点 180x120。"""
    shot = Image.open(SHOT).convert("RGB")
    k = 1476.0 / 1920.0
    ox, oy = -3.2, 121.8   # 标定：用截图里两个按钮的实际中心反解（k=0.769 已由两按钮间距验证）
    out = Image.new("RGB", (2 * 620 + 60, 720 + 76), (0x11, 0x16, 0x1D))
    d0 = ImageDraw.Draw(out)
    for col, (tag, base) in enumerate((("老图（实机截图原样）", OLD), ("新图（贴回原坐标）", TARGET))):
        canvas = shot.copy()
        if col == 1:
            for name, cx, cy, digit in (("DrawCircle.png", 810.0, -420.0, "5"),
                                        ("EyeCircle.png", 808.5, -253.5, None)):
                node = render(os.path.join(base, name), NODE_W, NODE_H, digit)
                w, h = int(round(NODE_W * k)), int(round(NODE_H * k))
                node = node.resize((w, h), Image.LANCZOS)
                px = int(round((960 + cx) * k)) - w // 2
                py = int(round(oy + (540 - cy) * k)) - h // 2
                canvas.paste(node, (px, py), node)
        crop = canvas.crop((1180, 590, 1476, 950))
        crop = crop.resize((crop.width * 2, crop.height * 2), Image.NEAREST)
        x = 30 + col * 620
        out.paste(crop, (x, 40))
        d0.text((x, 16), tag, fill=(0xE4, 0xCB, 0x84))
    d0.text((30, 742), "左：实机截图原样（老图）    右：新图按原坐标贴回（只覆盖贴图，场景坐标与尺寸未动）", fill=(0x8E, 0xA2, 0xB4))
    out_path = os.path.join(PREVIEW, "hud-btn-v1-insitu.png")
    out.save(out_path)
    return out_path


if __name__ == "__main__":
    print(sheet_main())
    print(sheet_insitu())