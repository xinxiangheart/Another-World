# -*- coding: utf-8 -*-
"""前缀底图 v2 —— 画窗底图（5 前缀 + 通用 + 法术通用）

v1（PrefixBgV1.py）的问题：母题全在中央，实机被召唤物立绘挡掉 → 认不出前缀；
且七张共用一个暗底，彼此差别只在中央那点花纹上。

v2 改两件事：
  ① 身份搬到**画窗边带 + 四角 + 上下缘**（原画在左右各留 5~17%、上下 3~15%、四角全露，
     那里才是唯一永远看得见的地方）。
  ② **每个前缀一套自己的底**：色相 + 明度走向都不同，不再共用一个暗底。

规格
  6 张 = 768x1024（3:4）→ 2D `PrefixArtBG` 节点 64x84；3D `PrefixBg` scale 0.09（PPU 100 → 0.6912x0.9216）
  法术 = 768x768（**方形**，因为法术画窗是 64x64）→ 3D `PrefixBg` scale 0.09 → 0.6912x0.6912
  旧图 736x1312 / 745x1299 各不相同的高饱和整色板。

生成：Assets/_Game/Art/Sprites/Generated/prefixbg-v2/*.png
安装：Assets/_Game/Resources/Cards/PrefixArtBG/*.png（原地覆盖，guid 不变 → 预制体不用改）
用法：仓库根目录下  python Tools/cardframe/PrefixBgV2.py
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw

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
import PrefixBgMotifs                       # v1 母题（只取中央那层当纹理）
import PrefixBgMotifsV2 as V2               # v2 边缘体系

SS = 3
OUT = "Assets/_Game/Art/Sprites/Generated/prefixbg-v2"
INSTALL = "Assets/_Game/Resources/Cards/PrefixArtBG"

BASE_W, BASE_H = 768, 1024
SIZE = {}
for _k in ("Psychic", "Abyss", "Mech", "Blood", "Scroll", "Common"):
    SIZE[_k] = (BASE_W, BASE_H)
SIZE["Spell"] = (768, 768)

# ══ 每个前缀自己的底 ══════════════════════════════════════════════
# (顶色, 底色, 明度走向 mode, 强度 amt)
#   vignette 边暗中间亮 / invert 中心最黑向外亮 / top 上亮下暗 / bottom 下亮上暗
#   corner 左上受光 / midband 中间一条横带亮 / flat 几乎不平
FIELDS = {
    "Psychic": ((26, 64, 84), (9, 24, 38), "vignette", 0.26),   # 冷青：中间渗冷光
    "Abyss":   ((56, 28, 82), (15, 9, 30), "invert",   0.34),   # 紫：中心最黑，向外发亮
    "Mech":    ((68, 52, 34), (22, 17, 13), "corner",  0.30),   # 暖褐钢：左上受光
    "Blood":   ((72, 26, 36), (24, 8, 15), "bottom",   0.34),   # 酒红：血往下积
    "Scroll":  ((26, 62, 50), (8, 26, 22), "midband",  0.26),   # 墨绿：卷面横带受光
    "Common":  ((38, 42, 50), (18, 20, 26), "flat",    0.00),   # 中性炭灰：最素，无属性（与法术的蓝分开）
    "Spell":   ((32, 48, 74), (12, 18, 34), "top",     0.30),   # 钢蓝：上亮下暗（符文柱受光）
}

# 花纹色相（与 Icon_Prefix_* 同值）
TINT = {"Psychic": (69, 198, 232), "Abyss": (160, 90, 232), "Mech": (217, 135, 60),
        "Blood": (216, 54, 78), "Scroll": (92, 192, 126),
        "Common": (201, 175, 101), "Spell": (158, 184, 212)}
CN = {"Psychic": "灵能", "Abyss": "渊", "Mech": "机械", "Blood": "血歌",
      "Scroll": "神灵画卷", "Common": "通用（无前缀）", "Spell": "法术通用"}
# 数组下标顺序：0=灵能 1=渊 2=机械 3=血歌 4=神灵画卷（与 CardDisplay2D/3D.PrefixToIndex 一致）
ORDER = ["Psychic", "Abyss", "Mech", "Blood", "Scroll", "Common", "Spell"]

CENTER_ALPHA = 0.40     # 中央母题整体透明度（v1 是 1.0）—— 只当纹理，不承担识别


def S(v):
    return v * SS


def col(rgb, a=255):
    return (int(rgb[0]), int(rgb[1]), int(rgb[2]), int(a))


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def field(key, wd, ht):
    top, bot, mode, amt = FIELDS[key]
    a = np.clip(np.arange(ht * SS, dtype=np.float32)[:, None] / (ht * SS - 1), 0, 1)
    t = np.array(top, np.float32); b = np.array(bot, np.float32)
    base = t[None, :] * (1 - a) + b[None, :] * a
    arr = np.repeat(base[:, None, :], wd * SS, axis=1)
    yy, xx = np.mgrid[0:ht * SS, 0:wd * SS]
    nx = (xx - wd * SS / 2.0) / (wd * SS / 2.0)
    ny = (yy - ht * SS / 2.0) / (ht * SS / 2.0)
    r = np.sqrt(nx * nx * 0.72 + ny * ny)
    if mode == "vignette":
        k = 1.0 - amt * np.clip((r - 0.28) / 1.0, 0, 1) ** 1.2
    elif mode == "invert":
        k = 1.0 - amt * np.clip(1.0 - r, 0, 1) ** 0.9
    elif mode == "top":
        k = 1.0 + amt * (0.5 - (ny + 1) / 2.0)
    elif mode == "bottom":
        k = 1.0 + amt * ((ny + 1) / 2.0 - 0.5)
    elif mode == "corner":
        k = 1.0 + amt * (0.5 - (nx + ny) / 2.0)
    elif mode == "midband":
        k = 1.0 + amt * (1.0 - np.clip(np.abs(ny) / 0.80, 0, 1)) - amt * 0.35
    else:
        k = np.ones_like(r)
    # 中央再压一道（角色站的位置），让原画更跳、边带更显
    k = k * (1.0 - 0.20 * np.exp(-(r / 0.42) ** 2))
    k = np.clip(k, 0.34, 1.32)
    arr *= k[:, :, None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


CENTER = PrefixBgMotifs.make(S, lambda rgb, a=255: col(rgb, int(a * CENTER_ALPHA)), mix)
EDGES = V2.make(S, col, mix)


def build(key):
    wd, ht = SIZE[key]
    # ── 中央纹理（v1 母题，压到 40%）──
    C = Image.new("RGBA", (BASE_W * SS, BASE_H * SS), (0, 0, 0, 0))
    CENTER[key](ImageDraw.Draw(C), TINT[key])
    if (wd, ht) != (BASE_W, BASE_H):
        cy = int(BASE_H * 0.46 * SS)                 # v1 母题的中心 y
        y0 = max(0, cy - ht * SS // 2)
        C = C.crop((0, y0, wd * SS, y0 + ht * SS))
    # ── 边缘体系（v2，满色）──
    E = Image.new("RGBA", (wd * SS, ht * SS), (0, 0, 0, 0))
    EDGES[key](ImageDraw.Draw(E), TINT[key], wd, ht)
    im = Image.alpha_composite(field(key, wd, ht), C)
    im = Image.alpha_composite(im, E)
    return im.resize((wd, ht), Image.LANCZOS)


def main():
    for d in (OUT, INSTALL):
        os.makedirs(d, exist_ok=True)
    for k in ORDER:
        im = build(k)
        im.save(os.path.join(OUT, k + ".png"))
        im.save(os.path.join(INSTALL, k + ".png"))
        print("  %-8s %-12s %s  %s" % (k, CN[k], im.size, os.path.join(INSTALL, k + ".png")))
    print("done", len(ORDER))


if __name__ == "__main__":
    main()