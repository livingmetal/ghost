"""
Natural re-composition of yakumo-idle (eyes open) onto yakumo-blink:
Poisson seamless cloning transfers idle's eye detail while matching blink's
skin tone / lighting at the boundary -> looks redrawn, no visible seam.
Alpha is locked to blink so the silhouette stays pixel-identical.
"""
import os
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFilter

D = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
blink = Image.open(os.path.join(D, "yakumo-blink.png")).convert("RGBA")
idle = Image.open(os.path.join(D, "yakumo-idle.png")).convert("RGBA")

# eye regions in shared coords (idle ~ blink position, head tilt -> left eye lower)
EYES = [
    dict(c=(480, 304), rx=50, ry=33),   # left
    dict(c=(583, 280), rx=48, ry=33),   # right
]

blink_bgr = cv2.cvtColor(np.array(blink)[:, :, :3], cv2.COLOR_RGB2BGR)
idle_bgr = cv2.cvtColor(np.array(idle)[:, :, :3], cv2.COLOR_RGB2BGR)

dst = blink_bgr.copy()
for e in EYES:
    mask = np.zeros(dst.shape[:2], np.uint8)
    cx, cy = e["c"]
    cv2.ellipse(mask, (cx, cy), (e["rx"], e["ry"]), 0, 0, 360, 255, -1)
    dst = cv2.seamlessClone(idle_bgr, dst, mask, (cx, cy), cv2.NORMAL_CLONE)

res_rgb = cv2.cvtColor(dst, cv2.COLOR_BGR2RGB)
res = np.dstack([res_rgb, np.array(blink)[:, :, 3]])   # lock alpha to blink
Image.fromarray(res).save(os.path.join(D, "_work", "out_idle2.png"))


def prev(arr, fn, box=None, sc=6):
    im = Image.fromarray(arr)
    bg = Image.new("RGBA", im.size, (245, 245, 248, 255)); bg.alpha_composite(im)
    bg = bg.convert("RGB")
    if box:
        bg = bg.crop(box).resize(((box[2]-box[0])*sc, (box[3]-box[1])*sc))
    bg.save(os.path.join(D, "_work", fn))


prev(res, "p2_full.png")
prev(res, "p2_L.png", (430, 255, 545, 330))
prev(res, "p2_R.png", (545, 250, 660, 325))
print("done")
