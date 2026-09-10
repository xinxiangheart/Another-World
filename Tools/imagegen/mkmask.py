"""Build an OpenAI-style edit mask: opaque white, TRANSPARENT where the edit applies."""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

src, dst, box, feather = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
x0, y0, x1, y1 = [int(v) for v in box.split(",")]

im = Image.open(src).convert("RGBA")
W, H = im.size
region = Image.new("L", (W, H), 0)
ImageDraw.Draw(region).rounded_rectangle([x0, y0, x1, y1], radius=int(min(x1-x0, y1-y0)*0.28), fill=255)
region = region.filter(ImageFilter.GaussianBlur(feather))

alpha = Image.fromarray((255 - np.asarray(region)).astype(np.uint8), "L")
mask = Image.merge("RGBA", [Image.new("L", (W, H), 255)]*3 + [alpha])
mask.save(dst)

# preview: red overlay where the edit applies
over = im.copy()
red = Image.new("RGBA", (W, H), (255, 0, 0, 255))
over = Image.composite(red, over, alpha.point(lambda v: 255 if v < 128 else 0))
over = Image.blend(im, over, 0.65)
bg = Image.new("RGBA", (W, H), (255, 255, 255, 255))
Image.alpha_composite(bg, over).convert("RGB").save(dst.replace(".png", "-preview.jpg"), quality=88)
sel = np.asarray(alpha) < 128
print(f"mask {W}x{H}  region x[{x0},{x1}] y[{y0},{y1}] feather={feather}  edit px={int(sel.sum())} ({100.0*sel.mean():.1f}%)")
