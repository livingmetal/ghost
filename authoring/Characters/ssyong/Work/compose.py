import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

WORK = os.path.dirname(os.path.abspath(__file__))
base = Image.open(os.path.join(WORK, "base_b.png")).convert("RGBA")
creg = Image.open(os.path.join(WORK, "creg.png")).convert("RGBA")

# ---------- working-close-eye: transplant closed eyes from creg ----------
FACE_DX, FACE_DY = 11, 4  # shift creg -> base face alignment
creg_a = np.array(creg)
creg_a = np.roll(np.roll(creg_a, FACE_DY, 0), FACE_DX, 1)
creg_aligned = Image.fromarray(creg_a)

eye_mask = Image.new("L", base.size, 0)
d = ImageDraw.Draw(eye_mask)
for (cx, cy, rx, ry) in [(296, 363, 62, 42), (408, 366, 54, 38)]:
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
eye_mask = eye_mask.filter(ImageFilter.GaussianBlur(5))

close = base.copy()
close = Image.composite(creg_aligned, close, eye_mask)
# kill any residual bright-blue iris pixels left in the eye zone
ca = np.array(close)
for (x0, y0, x1, y1) in [(232, 322, 352, 404), (356, 328, 460, 406)]:
    reg = ca[y0:y1, x0:x1]
    r, g, b = reg[:, :, 0].astype(int), reg[:, :, 1].astype(int), reg[:, :, 2].astype(int)
    blue = (b > 70) & (b > r + 12) & (g > r - 10)
    if blue.any():
        # replace with the median fur color of the (non-blue) region
        fur = reg[~blue][:, :3]
        med = np.median(fur, 0).astype(np.uint8) if len(fur) else np.array([60, 45, 38], np.uint8)
        reg[blue, 0], reg[blue, 1], reg[blue, 2] = med[0], med[1], med[2]
        ca[y0:y1, x0:x1] = reg
close = Image.fromarray(ca)
close.save(os.path.join(WORK, "out_close.png"))

# ---------- graph bars: synthesize SAME glow style on both a and b ----------
BARS = [(456, 461), (466, 472), (477, 482), (487, 493)]
BASELINE = 541
BARCOL = (150, 224, 244)


def draw_graph(src, heights):
    img = src.copy()
    ar = np.array(img)
    bg = ar[503:507, 456:496, :].reshape(-1, 4).mean(0).astype(np.uint8)
    ar[506:544, 455:497] = bg          # erase old bars to panel bg
    img = Image.fromarray(ar)
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dg = ImageDraw.Draw(glow)
    for (xa, xb), h in zip(BARS, heights):
        dg.rounded_rectangle([xa - 1, BASELINE - h - 1, xb + 1, BASELINE], radius=2,
                             fill=BARCOL + (255,))
    glow = glow.filter(ImageFilter.GaussianBlur(2.2))
    img = Image.alpha_composite(img, Image.eval(glow, lambda v: v))  # soft glow
    dd = ImageDraw.Draw(img, "RGBA")
    for (xa, xb), h in zip(BARS, heights):
        dd.rounded_rectangle([xa, BASELINE - h, xb, BASELINE], radius=2, fill=BARCOL + (210,))
        dd.rounded_rectangle([xa, BASELINE - h, xb, BASELINE - h + 3], radius=1, fill=(220, 248, 255, 240))
    return img


b_img = draw_graph(base, [31, 27, 14, 24])      # working-b heights
a_img = draw_graph(base, [17, 33, 25, 13])      # working-a heights (graph "moved")

# lock alpha to base so the silhouette is pixel-identical across all 3 frames
BASE_ALPHA = np.array(base)[:, :, 3]


def lock_alpha(img):
    arr = np.array(img); arr[:, :, 3] = BASE_ALPHA; return Image.fromarray(arr)


b_img = lock_alpha(b_img)
a_img = lock_alpha(a_img)
close = lock_alpha(close)
close.save(os.path.join(WORK, "out_close.png"))
b_img.save(os.path.join(WORK, "out_b.png"))
a_img.save(os.path.join(WORK, "out_a.png"))

# previews on dark
def dark(im, fn, box=None):
    dd = Image.new("RGBA", im.size, (30, 33, 43, 255)); dd.alpha_composite(im)
    dd = dd.convert("RGB")
    if box: dd = dd.crop(box).resize(((box[2]-box[0])*3, (box[3]-box[1])*3))
    dd.save(os.path.join(WORK, fn))

dark(close, "prev_close.png")
dark(close, "prev_close_face.png", (240, 300, 470, 420))
dark(a_img, "prev_a_graph.png", (440, 490, 520, 550))
dark(b_img, "prev_b_graph.png", (440, 490, 520, 550))
print("done")
