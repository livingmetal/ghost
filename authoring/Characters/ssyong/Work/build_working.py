"""
Rebuild the 3 working sprites from ONE shared base (working-b) so the body /
silhouette is pixel-identical:
  working-b          = base (eyes open)
  working-a          = base with the terminal GRAPH region swapped from orig a
  working-close-eye  = base with the EYE region swapped from orig close-eye
Only the swapped interior regions differ -> identical outline, no flicker.
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from working_realign import (make_transparent, main_blob_mask, body_mask,
                             register, CANVAS_W, CANVAS_H, TARGET_BODY_W,
                             GROUND_Y, CENTER_X)

WORK = os.path.dirname(os.path.abspath(__file__))
AUTHORING_ROOT = os.path.dirname(WORK)
ORIG = os.path.join(AUTHORING_ROOT, "Originals")


def placed(im, final_scale, ox, oy):
    nw, nh = max(1, round(im.width * final_scale)), max(1, round(im.height * final_scale))
    c = Image.new("RGBA", (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
    c.alpha_composite(im.resize((nw, nh), Image.LANCZOS), (round(ox), round(oy)))
    return c


def build_frames():
    ref = make_transparent(Image.open(os.path.join(ORIG, "working-b.png")))
    rmask, rbbox = main_blob_mask(ref)
    rbody = body_mask(rmask, rbbox)
    rh = rbbox[3] - rbbox[1]
    rx0, ry0, rx1, ry1 = rbbox
    scale_place = TARGET_BODY_W / (rx1 - rx0)
    place = (CENTER_X - scale_place * (rx0 + rx1) / 2, GROUND_Y - scale_place * ry1)

    base = placed(ref, scale_place, place[0], place[1])

    out = {"working-b.png": base}
    for f in ["working-a.png", "working-close-eye.png"]:
        im = make_transparent(Image.open(os.path.join(ORIG, f)))
        mask, bbox = main_blob_mask(im)
        iou, s, (ty, tx) = register(rbody, rh, body_mask(mask, bbox), bbox)
        fs = scale_place * s
        ox = scale_place * tx + place[0]
        oy = scale_place * ty + place[1]
        out[f] = placed(im, fs, ox, oy)
        print(f, "IoU=%.3f" % iou)
    return base, out


if __name__ == "__main__":
    base, reg = build_frames()
    base.save(os.path.join(WORK, "base_b.png"))
    reg["working-a.png"].save(os.path.join(WORK, "areg.png"))
    reg["working-close-eye.png"].save(os.path.join(WORK, "creg.png"))
    # comparison crops (graph + eyes) on dark bg
    def ondark(im):
        d = Image.new("RGBA", im.size, (25, 28, 38, 255)); d.alpha_composite(im); return d.convert("RGB")
    GRAPH = (470, 455, 565, 545)
    EYES = (260, 300, 440, 390)
    for nm, im in [("base", base), ("areg", reg["working-a.png"]), ("creg", reg["working-close-eye.png"])]:
        ondark(im).crop(GRAPH).resize((285, 270)).save(os.path.join(WORK, f"cmp_graph_{nm}.png"))
        ondark(im).crop(EYES).resize((360, 180)).save(os.path.join(WORK, f"cmp_eyes_{nm}.png"))
    print("done")
