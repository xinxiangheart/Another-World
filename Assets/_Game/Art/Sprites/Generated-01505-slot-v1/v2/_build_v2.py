from pathlib import Path
import math

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parent
ASSETS = ROOT / "assets"
ASSETS.mkdir(parents=True, exist_ok=True)

W, H = 540, 960
SS = 4

STEEL = (142, 162, 180, 255)
STEEL_DIM = (62, 78, 94, 255)
STEEL_DARK = (34, 47, 61, 255)
GOLD = (200, 164, 74, 255)
GOLD_BRIGHT = (228, 203, 132, 255)
GOLD_DARK = (138, 111, 46, 255)
AMBER = (224, 166, 75, 255)
AMBER_HOT = (245, 198, 112, 255)


def px(value):
    return int(round(value * SS))


def layer(width=W, height=H):
    return Image.new("RGBA", (px(width), px(height)), (0, 0, 0, 0))


def flatten(img, width=W, height=H):
    return img.resize((width, height), Image.LANCZOS)


def rounded(draw, box, radius, fill=None, outline=None, width=1):
    draw.rounded_rectangle(
        tuple(px(v) for v in box), radius=px(radius), fill=fill,
        outline=outline, width=px(width),
    )


def ellipse(draw, box, fill=None, outline=None, width=1):
    draw.ellipse(tuple(px(v) for v in box), fill=fill, outline=outline, width=px(width))


def polygon(draw, points, fill=None, outline=None, width=1):
    draw.polygon([(px(x), px(y)) for x, y in points], fill=fill)
    if outline:
        draw.line(
            [(px(x), px(y)) for x, y in points + [points[0]]],
            fill=outline, width=px(width), joint="curve",
        )


def make_slot_plate():
    img = layer()
    d = ImageDraw.Draw(img)
    rounded(d, (22, 20, 518, 940), 61, fill=(5, 7, 10, 242), outline=STEEL_DIM, width=3)
    rounded(d, (31, 29, 509, 931), 53, outline=(20, 28, 38, 255), width=2)
    rounded(d, (41, 39, 499, 921), 46, outline=(91, 73, 37, 120), width=1)
    return flatten(img)


def make_tack(size=320):
    img = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = size / 2
    pin = [(c, 39), (c + 6, 78), (c + 13, 151), (c - 13, 151), (c - 6, 78)]
    polygon(d, pin, fill=(10, 16, 24, 255), outline=STEEL_DARK, width=2)
    polygon(d, [(c, 39), (c + 4, 83), (c + 1, 139), (c - 1, 139), (c - 4, 83)],
            fill=(116, 137, 154, 255))
    polygon(d, [(c, 39), (c + 2.5, 73), (c, 79), (c - 2.5, 73)],
            fill=(196, 211, 220, 255))

    head = [(c, c - 61), (c + 61, c), (c, c + 61), (c - 61, c)]
    polygon(d, head, fill=(12, 19, 28, 255), outline=STEEL, width=3)
    inner = [(c, c - 47), (c + 47, c), (c, c + 47), (c - 47, c)]
    polygon(d, inner, fill=(18, 27, 38, 255), outline=GOLD_DARK, width=2)
    tiny = [(c, c - 34), (c + 34, c), (c, c + 34), (c - 34, c)]
    polygon(d, tiny, fill=(10, 16, 24, 255), outline=(86, 68, 35, 255), width=1)
    ellipse(d, (c - 10, c - 10, c + 10, c + 10), fill=GOLD_DARK, outline=GOLD_BRIGHT, width=1)
    ellipse(d, (c - 4, c - 5, c + 1, c), fill=GOLD_BRIGHT)
    return img.resize((size, size), Image.LANCZOS)


def make_gear(size=512):
    img = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = size / 2
    for k in range(8):
        a = math.radians(k * 45 - 90)
        half = math.radians(9)
        r0, r1 = c * 0.66, c * 0.88
        pts = [
            (c + math.cos(a - half) * r0, c + math.sin(a - half) * r0),
            (c + math.cos(a - half) * r1, c + math.sin(a - half) * r1),
            (c + math.cos(a + half) * r1, c + math.sin(a + half) * r1),
            (c + math.cos(a + half) * r0, c + math.sin(a + half) * r0),
        ]
        polygon(d, pts, fill=(12, 19, 28, 255), outline=STEEL_DIM, width=2)

    ellipse(d, (c * 0.18, c * 0.18, c * 1.82, c * 1.82),
            fill=(10, 16, 24, 255), outline=STEEL, width=3)
    ellipse(d, (c * 0.27, c * 0.27, c * 1.73, c * 1.73),
            outline=(36, 50, 65, 255), width=2)
    ellipse(d, (c * 0.38, c * 0.38, c * 1.62, c * 1.62),
            outline=GOLD_DARK, width=3)
    ellipse(d, (c * 0.45, c * 0.45, c * 1.55, c * 1.55),
            fill=(7, 12, 18, 255), outline=STEEL_DIM, width=2)

    for k in range(4):
        a = math.radians(45 + k * 90)
        sx = c + math.cos(a) * c * 0.38
        sy = c + math.sin(a) * c * 0.38
        ellipse(d, (sx - 6, sy - 6, sx + 6, sy + 6), fill=GOLD_DARK, outline=GOLD_BRIGHT, width=1)

    ellipse(d, (c - 26, c - 56, c + 26, c - 4), fill=AMBER, outline=(49, 34, 17, 255), width=3)
    polygon(d, [(c - 12, c - 25), (c + 12, c - 25), (c + 18, c + 55), (c - 18, c + 55)],
            fill=AMBER, outline=(49, 34, 17, 255), width=3)
    ellipse(d, (c - 12, c - 42, c + 6, c - 24), fill=AMBER_HOT)
    d.arc((px(c * 0.24), px(c * 0.24), px(c * 1.76), px(c * 1.76)),
          start=198, end=252, fill=STEEL, width=px(4))
    return img.resize((size, size), Image.LANCZOS)


def make_link(long_side, short_side, stroke, angle):
    pad = 20
    size = int(math.ceil(max(long_side, short_side) + pad * 2))
    img = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = size / 2
    box = (c - long_side / 2, c - short_side / 2, c + long_side / 2, c + short_side / 2)
    shadow_box = (box[0] + 2, box[1] + 4, box[2] + 2, box[3] + 4)
    rounded(d, shadow_box, short_side / 2, outline=(0, 0, 0, 145), width=stroke + 4)
    rounded(d, box, short_side / 2, outline=(73, 91, 108, 255), width=stroke)
    rounded(d, box, short_side / 2, outline=(8, 13, 20, 255), width=max(2, stroke - 4))
    return img.rotate(angle, expand=True, resample=Image.BICUBIC)


def draw_chain(img, start, end, progress=1.0):
    x0, y0 = start
    x1, y1 = end
    dx, dy = x1 - x0, y1 - y0
    length = math.hypot(dx, dy)
    theta = math.degrees(math.atan2(dy, dx))
    shadow = layer()
    sd = ImageDraw.Draw(shadow)
    sd.line([(px(x0), px(y0)), (px(x0 + dx * progress), px(y0 + dy * progress))],
            fill=(0, 0, 0, 135), width=px(25), joint="curve")
    shadow = shadow.filter(ImageFilter.GaussianBlur(px(2.2)))
    img.alpha_composite(shadow)

    long_side, short_side, stroke = 46, 26, 6
    spacing = 26
    link_count = int(length / spacing) + 1
    cut = length * max(0.0, min(1.0, progress))
    for i in range(link_count):
        distance = i * spacing
        if distance > cut:
            break
        t = distance / length
        cx = x0 + dx * t
        cy = y0 + dy * t
        link_angle = theta if i % 2 == 0 else theta + 90
        link = make_link(long_side, short_side, stroke, -link_angle)
        pos = (int(round(cx * SS - link.width / 2)), int(round(cy * SS - link.height / 2)))
        img.alpha_composite(link, pos)
        if i % 4 == 1:
            dot = layer()
            dd = ImageDraw.Draw(dot)
            ellipse(dd, (cx - 2, cy - 2, cx + 2, cy + 2), fill=GOLD, outline=GOLD_BRIGHT, width=0.5)
            img.alpha_composite(flatten(dot))


def paste_scaled(img, asset, center, target_width):
    width = max(1, int(round(target_width * SS)))
    height = max(1, int(round(asset.height * width / asset.width)))
    resized = asset.resize((width, height), Image.LANCZOS)
    pos = (int(round(center[0] * SS - width / 2)), int(round(center[1] * SS - height / 2)))
    img.alpha_composite(resized, pos)


def draw_tack(img, asset, center, angle, target_width=96):
    rotated = asset.rotate(angle, expand=True, resample=Image.BICUBIC)
    paste_scaled(img, rotated, center, target_width)


def build_effect(tack_asset, gear_asset, tack_count=4, chain_progress=1.0, gear_scale=1.0, gear_offset=0.0):
    img = layer()
    plate = Image.open(ASSETS / "SlotPlate_v2.png").convert("RGBA").resize((px(W), px(H)), Image.LANCZOS)
    img.alpha_composite(plate)
    corners = [(76, 88), (464, 88), (464, 872), (76, 872)]
    if chain_progress > 0:
        draw_chain(img, corners[0], corners[2], chain_progress)
        draw_chain(img, corners[1], corners[3], chain_progress)

    tack_angles = [135, 225, 315, 45]
    for i in range(max(0, min(4, tack_count))):
        cx, cy = corners[i]
        inward = {0: (1.0, 1.0), 1: (-1.0, 1.0), 2: (-1.0, -1.0), 3: (1.0, -1.0)}[i]
        normal = math.hypot(*inward)
        center = (cx + inward[0] / normal * 26, cy + inward[1] / normal * 26)
        draw_tack(img, tack_asset, center, tack_angles[i], 88)

    shadow = layer()
    sd = ImageDraw.Draw(shadow)
    gcx, gcy = W / 2, H / 2 + gear_offset
    gr = 184 * gear_scale / 2
    ellipse(sd, (gcx - gr, gcy - gr + 5, gcx + gr, gcy + gr + 5), fill=(0, 0, 0, 155))
    shadow = shadow.filter(ImageFilter.GaussianBlur(px(3)))
    img.alpha_composite(shadow)
    paste_scaled(img, gear_asset, (gcx, gcy), 184 * gear_scale)
    return flatten(img)


def compose_preview(effect, path, actual=False):
    if actual:
        effect = effect.resize((108, 192), Image.LANCZOS)
        effect = effect.resize((432, 768), Image.NEAREST)
        out = Image.new("RGB", (560, 856), (9, 14, 22))
        out.paste(effect, (64, 44), effect)
        out.save(path)
        return
    out = Image.new("RGB", (720, 1120), (9, 14, 22))
    outer = ImageDraw.Draw(out)
    outer.rounded_rectangle((38, 28, 682, 1092), radius=22, outline=(32, 44, 58), width=2)
    out.paste(effect, (90, 80), effect)
    out.save(path)


def make_sheet(frames, path):
    tile_w = 300
    gap = 16
    out = Image.new("RGB", (tile_w * len(frames) + gap * (len(frames) + 1), 620), (9, 14, 22))
    for i, frame in enumerate(frames):
        x = gap + i * (tile_w + gap)
        y = 30
        shadow = ImageDraw.Draw(out)
        shadow.rounded_rectangle((x - 8, y - 8, x + 268 + 8, y + 480 + 8), radius=14, fill=(12, 18, 27))
        f = frame.resize((268, 480), Image.LANCZOS)
        out.paste(f, (x, y), f)
    out.save(path)


def export_chains():
    corners = [(76, 88), (464, 88), (464, 872), (76, 872)]
    chain_a = layer()
    draw_chain(chain_a, corners[0], corners[2], 1.0)
    flatten(chain_a).save(ASSETS / "SlotLock_ChainA_01505.png")
    chain_b = layer()
    draw_chain(chain_b, corners[1], corners[3], 1.0)
    flatten(chain_b).save(ASSETS / "SlotLock_ChainB_01505.png")


def main():
    plate = make_slot_plate()
    plate.resize((W, H), Image.LANCZOS).save(ASSETS / "SlotPlate_v2.png")
    tack = make_tack()
    tack.save(ASSETS / "Tack_v2.png")
    gear = make_gear()
    gear.save(ASSETS / "GearLock_v2.png")
    export_chains()

    final = build_effect(tack, gear)
    compose_preview(final, ROOT / "preview_v2_final_540.png")
    compose_preview(final, ROOT / "preview_v2_actual_4x.png", actual=True)

    frames = [
        build_effect(tack, gear, tack_count=1, chain_progress=0.0, gear_scale=0.0, gear_offset=-260),
        build_effect(tack, gear, tack_count=4, chain_progress=0.28, gear_scale=0.0, gear_offset=-260),
        build_effect(tack, gear, tack_count=4, chain_progress=0.92, gear_scale=1.16, gear_offset=-92),
        build_effect(tack, gear, tack_count=4, chain_progress=1.0, gear_scale=1.0, gear_offset=0),
    ]
    make_sheet(frames, ROOT / "preview_v2_sheet.png")
    print("wrote", ROOT)


if __name__ == "__main__":
    main()
