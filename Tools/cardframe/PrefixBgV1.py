# -*- coding: utf-8 -*-
"""
前缀底图 v1 —— 画窗底图（5 前缀 + 通用 + 法术通用）

规格 768x1024（3:4）
  · 2D `PrefixArtBG`：rect 64x84 → 画布 593x775（比例 0.765），贴图 0.75 → 横向拉伸 1.6%
  · 3D `PrefixBg`：SpriteRenderer，PPU 100 → 768x1024 = 7.68x10.24 世界单位，
    uniform scale 0.09 → 0.6912 x 0.9216，正好铺满 ArtAreaSize(0.69, 0.92)
  旧图是 736x1312 / 745x1299 各不相同的高饱和整色板 → 新图统一尺寸 + 统一「暗底 + 低对比母题」

设计口径（对齐卡框 v7 / 棋盘 board-layers-v2）
  底    #101A26 → #080D15 竖直渐变（比卡身再深一档，读成「凹进卡里的画窗」）
  边    径向压暗，不做倒角 / 内阴影 / 辉光
  母题  大、居中、低对比；主干 25% / 细线 18% / 点 38% —— 只当纹理，绝不抢原画
  色相  同 Icon_Prefix_*：渊#A05AE8 / 血歌#D8364E / 机械#D9873C / 灵能#45C6E8 / 神灵画卷#5CC07E
        通用 = 中性金 #C9AF65（无属性）；法术通用 = 中性钢蓝 #9EB8D4（法术无前缀，故通用）
  家族  七张共用同一圈外环 + 四角刻线，保证成套

生成：Assets/_Game/Art/Sprites/Generated/prefixbg-v1/*.png
安装：Assets/_Game/Resources/Cards/PrefixArtBG/*.png（原地覆盖，guid 不变 → 预制体不用改）
用法：仓库根目录下  python Tools/cardframe/PrefixBgV1.py
"""
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

def _root():
    d = os.getcwd()
    if os.path.isdir(os.path.join(d, 'Assets', '_Game')):
        return d
    q = HERE
    while q and q != os.path.dirname(q):
        if os.path.isdir(os.path.join(q, 'Assets', '_Game')):
            return q
        q = os.path.dirname(q)
    return os.getcwd()

ROOT = _root(); os.chdir(ROOT)
sys.path.insert(0, HERE)
from PrefixBgMotifs import make, W, H      # noqa: E402

SS = 3
OUT = "Assets/_Game/Art/Sprites/Generated/prefixbg-v1"
INSTALL = "Assets/_Game/Resources/Cards/PrefixArtBG"

FIELD_T, FIELD_B = (16, 26, 38), (8, 13, 21)
TINT = {"Psychic": (69, 198, 232), "Abyss": (160, 90, 232), "Mech": (217, 135, 60),
        "Blood": (216, 54, 78), "Scroll": (92, 192, 126),
        "Common": (201, 175, 101), "Spell": (158, 184, 212)}
CN = {"Psychic": "灵能", "Abyss": "渊", "Mech": "机械", "Blood": "血歌",
      "Scroll": "神灵画卷", "Common": "通用（无前缀）", "Spell": "法术通用"}
# 数组下标顺序：0=灵能 1=渊 2=机械 3=血歌 4=神灵画卷（与 CardDisplay2D/3D.PrefixToIndex 一致）
ORDER = ["Psychic", "Abyss", "Mech", "Blood", "Scroll", "Common", "Spell"]


def S(v):
    return v * SS


def col(rgb, a=255):
    return (int(rgb[0]), int(rgb[1]), int(rgb[2]), int(a))


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def field():
    a = np.clip(np.arange(H * SS, dtype=np.float32) / SS / (H - 1), 0, 1)[:, None]
    t = np.array(FIELD_T, np.float32); b = np.array(FIELD_B, np.float32)
    arr = np.repeat((t[None, :] * (1 - a) + b[None, :] * a)[:, None, :], W * SS, axis=1)
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    yy, xx = np.mgrid[0:H * SS, 0:W * SS]
    nx = (xx - W * SS / 2.0) / (W * SS / 2.0)
    ny = (yy - H * SS / 2.0) / (H * SS / 2.0)
    r = np.sqrt(nx * nx * 0.72 + ny * ny)
    k = np.clip(1.0 - 0.40 * np.clip((r - 0.34) / 0.95, 0, 1) ** 1.25, 0.58, 1.0)
    arr = np.array(img, np.float32)
    arr[:, :, :3] *= k[:, :, None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


MOTIF = make(S, col, mix)


def build(key):
    L = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    MOTIF[key](__import__("PIL.ImageDraw", fromlist=["ImageDraw"]).Draw(L), TINT[key])
    return Image.alpha_composite(field(), L).resize((W, H), Image.LANCZOS)


def main():
    for d in (OUT, INSTALL):
        os.makedirs(d, exist_ok=True)
    for k in ORDER:
        im = build(k)
        im.save(os.path.join(OUT, k + ".png"))
        im.save(os.path.join(INSTALL, k + ".png"))
        print("  %-8s %-12s -> %s" % (k, CN[k], os.path.join(INSTALL, k + ".png")))
    print("done", len(ORDER))


if __name__ == "__main__":
    main()
