import numpy as np
from PIL import Image, ImageDraw, ImageFont

SHOT = r"C:\Users\22589\AppData\Local\Temp\codex-clipboard-441fceba-9a42-465d-80e7-ade0a4ab0655.png"
DRAW = r"C:\Users\22589\AppData\Local\Temp\codex-clipboard-b6a8575e-28d4-4052-a852-461aae5990d4.png"
OTF  = r"Assets/_Game/Fonts/NotoSerifCJKsc-Black.otf"
OUT  = r"Tools/cardframe/preview/hud-text-font-swap.png"

K = 0.765
EM = 21 * 2 * K
FACE, OLINE = (255, 255, 255), (14, 11, 9)
K_OLD = (21 / 59.0) * 2 * K

shot = Image.open(SHOT).convert("RGB")
arr = np.asarray(shot).astype(float)
X0, Y0, W, H = 96, 118, 300, 74
before = shot.crop((X0, Y0, X0 + W, Y0 + H))
after = before.copy()
d = ImageDraw.Draw(after)

# 逐行在缺口左右两侧取色后线性插值填掉旧数字（顶栏底板有轻微横向渐变，插值比取单列干净）
for x1, x2 in [(137, 171), (232, 250), (327, 346)]:
    for y in range(H):
        ya = Y0 + y
        cl = arr[ya, x1 - 4]
        cr = arr[ya, x2 + 4]
        n = x2 - x1 + 1
        for i in range(n):
            t = (i + 1) / (n + 1)
            px = tuple(int(round(v)) for v in (cl * (1 - t) + cr * t))
            d.point((x1 - X0 + i, y), fill=px)

font = ImageFont.truetype(OTF, int(round(EM)))
BASE = 152.5 - Y0
def pen(x_ink_old, bearing_old_fu):
    return x_ink_old - bearing_old_fu * K_OLD - X0
d.text((pen(137, 2.359), BASE), "20", font=font, fill=FACE, stroke_width=1, stroke_fill=OLINE, anchor="ls")
d.text((pen(234, 2.891), BASE), "0",  font=font, fill=FACE, stroke_width=1, stroke_fill=OLINE, anchor="ls")
d.text((pen(330, 5.188), BASE), "1",  font=font, fill=FACE, stroke_width=1, stroke_fill=OLINE, anchor="ls")

coin = Image.open(DRAW).convert("RGB").crop((1325, 818, 1395, 888))

Z = 4
rows = [(before, "BEFORE  real screenshot  |  NotoSansSC SDF  +  TMP_Stat_Bold.mat"),
        (after,  "AFTER   Serif Black otf re-render, same icons/frame  |  NotoSerifCJKsc-Black SDF  +  TMP_Orb_Black_Outline.mat"),
        (coin,   "REFERENCE  real screenshot  |  draw-count text already on TMP_Orb_Black_Outline, fontSize 54")]
pad, lab = 16, 26
CW, CH = W * Z, H * Z
CW2, CH2 = coin.width * Z, coin.height * Z
Wtot = max(CW, CW2 + 560) + pad * 2
Htot = pad + 3 * (lab + pad) + CH + CH + CH2 + 96
canvas = Image.new("RGB", (Wtot, Htot), (20, 21, 24))
dd = ImageDraw.Draw(canvas)
f = ImageFont.truetype(r"C:\Windows\Fonts\consola.ttf", 15)
y = pad
for img, label in rows:
    dd.text((pad, y), label, font=f, fill=(200, 200, 205)); y += lab
    big = img.resize((img.width * Z, img.height * Z), Image.NEAREST)
    canvas.paste(big, (pad, y)); y += big.height + pad
for n in [
    "top-bar labels: fontSize 21 x parent scale 2  ->  effective 42 canvas em  ->  32.1 px in this 1476x999 Game view",
    "digit advance  old 0.5551 em -> new 0.6120 em  (+10.3%); ink still starts at x=137 px, pen x unchanged",
    "ascent / descent / cap ratios differ by <0.4% between the two faces  ->  baseline and vertical centring do not move",
    "text box is 96x34 local with margin.x=40  ->  56 local (=112 canvas) of room; \"20\" uses ~26 local  ->  no wrap, no overflow",
    "outline px = _OutlineWidth(0.28) x _ScaleRatioA(0.8333) x 0.5 x effective scale  ->  grows with font size, so 42 em reads thinner than 54 em",
]:
    dd.text((pad, y), n, font=f, fill=(150, 152, 158)); y += 19
canvas.save(OUT)
print(OUT, canvas.size)