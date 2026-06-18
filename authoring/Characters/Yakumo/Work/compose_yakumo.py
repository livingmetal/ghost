"""
Build yakumo-idle from yakumo-blink (eyes-open variant sharing blink's silhouette):
  base   = yakumo-blink  (eyes closed)
  result = base with the OPEN eyes transplanted from the original yakumo-idle,
           aligned per-eye, then alpha locked to base -> identical silhouette.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

WORK = Path(__file__).resolve().parent
REPOSITORY_ROOT = Path(__file__).resolve().parents[4]
RUNTIME_DIR = (
    REPOSITORY_ROOT / "src" / "LivingMetalGhost" / "Assets" / "Characters" / "Yakumo"
)
blink = Image.open(RUNTIME_DIR / "yakumo-blink.png").convert("RGBA")
idle = Image.open(RUNTIME_DIR / "yakumo-idle.png").convert("RGBA")

# per-eye: (blink_center_x, blink_center_y, idle_center_x, idle_center_y, rx, ry)
# offset applied = blink_center - idle_center
EYES = [
    dict(bc=(480, 304), ic=(480, 304), rx=48, ry=34),   # left eye (lower: head tilt)
    dict(bc=(583, 278), ic=(583, 278), rx=46, ry=34),   # right eye
]
FEATHER = 7

idle_np = np.array(idle)
res = blink.copy()
for e in EYES:
    dx = e["bc"][0] - e["ic"][0]
    dy = e["bc"][1] - e["ic"][1]
    shifted = Image.fromarray(np.roll(np.roll(idle_np, dy, 0), dx, 1))
    m = Image.new("L", blink.size, 0)
    d = ImageDraw.Draw(m)
    cx, cy = e["bc"]
    d.ellipse([cx - e["rx"], cy - e["ry"], cx + e["rx"], cy + e["ry"]], fill=255)
    m = m.filter(ImageFilter.GaussianBlur(FEATHER))
    res = Image.composite(shifted, res, m)

# lock alpha to blink so silhouette is pixel-identical
arr = np.array(res); arr[:, :, 3] = np.array(blink)[:, :, 3]
res = Image.fromarray(arr)
res.save(WORK / "out_idle.png")

# previews on light + zoom
def prev(im, fn, box=None, sc=4):
    bg = Image.new("RGBA", im.size, (245, 245, 248, 255)); bg.alpha_composite(im)
    bg = bg.convert("RGB")
    if box:
        bg = bg.crop(box).resize(((box[2]-box[0])*sc, (box[3]-box[1])*sc))
    bg.save(WORK / fn)

prev(res, "prev_idle_full.png")
prev(res, "prev_idle_eyes.png", (430, 240, 630, 310))
prev(blink, "prev_blink_eyes.png", (430, 240, 630, 310))
print("done")
