"""
Re-align the 3 'working' sprites so their BODY (back) silhouette is identical,
removing the subtle size flicker. Steps:
  - reference = working-b
  - register working-a / working-close-eye to the reference using the BODY
    region only (head excluded), maximizing mask IoU over scale + translation
  - apply ONE common placement to all three -> uniform body size & position
"""
import os
import numpy as np
from collections import deque
from PIL import Image

SRC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ORIG = os.path.join(SRC, "_original")
OUT = os.path.join(SRC, "_work", "out")

CANVAS_W, CANVAS_H = 1024, 640
TARGET_BODY_W = 720
GROUND_Y = 600
CENTER_X = 512
HEAD_FRAC = 0.40   # left fraction of body bbox treated as head -> excluded from registration

REF = "working-b.png"
MOVING = ["working-a.png", "working-close-eye.png"]


def make_transparent(im):
    im = im.convert("RGBA"); a = np.array(im); h, w = a.shape[:2]
    rgb = a[:, :, :3].astype(int)
    bg = (rgb > 238).all(axis=2) | (a[:, :, 3] < 16)
    vis = np.zeros((h, w), bool); dq = deque()
    for y in range(h):
        for x in (0, w - 1):
            if bg[y, x] and not vis[y, x]: vis[y, x] = True; dq.append((y, x))
    for x in range(w):
        for y in (0, h - 1):
            if bg[y, x] and not vis[y, x]: vis[y, x] = True; dq.append((y, x))
    while dq:
        y, x = dq.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not vis[ny, nx] and bg[ny, nx]:
                vis[ny, nx] = True; dq.append((ny, nx))
    a[vis, 3] = 0
    return Image.fromarray(a)


def main_blob_mask(im):
    """boolean mask (native res) of largest alpha component + its bbox."""
    a = np.array(im); full = a[:, :, 3] > 30
    H, W = full.shape; sw = 256; sh = max(1, round(H * sw / W))
    small = np.array(Image.fromarray(full.astype(np.uint8) * 255)
                     .resize((sw, sh), Image.NEAREST)) > 0
    labels = np.zeros((sh, sw), int); cur = 0; best = (0, None, None)
    for sy in range(sh):
        for sx in range(sw):
            if small[sy, sx] and labels[sy, sx] == 0:
                cur += 1; dq = deque([(sy, sx)]); labels[sy, sx] = cur; cnt = 0
                minx = maxx = sx; miny = maxy = sy
                while dq:
                    y, x = dq.popleft(); cnt += 1
                    minx = min(minx, x); maxx = max(maxx, x)
                    miny = min(miny, y); maxy = max(maxy, y)
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < sh and 0 <= nx < sw and small[ny, nx] and labels[ny, nx] == 0:
                            labels[ny, nx] = cur; dq.append((ny, nx))
                if cnt > best[0]:
                    best = (cnt, cur, (minx, miny, maxx, maxy))
    comp = (labels == best[1])
    mask = np.array(Image.fromarray(comp.astype(np.uint8) * 255).resize((W, H), Image.NEAREST)) > 0
    fx = W / sw; fy = H / sh
    minx, miny, maxx, maxy = best[2]
    bbox = (minx * fx, miny * fy, (maxx + 1) * fx, (maxy + 1) * fy)
    return mask, bbox


def body_mask(mask, bbox):
    """zero out the head (left HEAD_FRAC of the blob bbox)."""
    x0, y0, x1, y1 = bbox
    cut = int(x0 + (x1 - x0) * HEAD_FRAC)
    m = mask.copy(); m[:, :cut] = False
    return m


def fft_best_shift(A, B):
    """shift (dy,dx) to apply to B so it best overlaps A (max intersection)."""
    H = max(A.shape[0], B.shape[0]); W = max(A.shape[1], B.shape[1])
    Ap = np.zeros((H, W)); Ap[:A.shape[0], :A.shape[1]] = A
    Bp = np.zeros((H, W)); Bp[:B.shape[0], :B.shape[1]] = B
    corr = np.fft.irfft2(np.fft.rfft2(Ap) * np.conj(np.fft.rfft2(Bp)), s=(H, W))
    idx = np.unravel_index(np.argmax(corr), corr.shape)
    dy = idx[0] if idx[0] <= H // 2 else idx[0] - H
    dx = idx[1] if idx[1] <= W // 2 else idx[1] - W
    return dy, dx


def iou_at(A, B, dy, dx):
    Bs = np.roll(np.roll(B, dy, 0), dx, 1)
    if dy >= 0: Bs[:dy, :] = False
    else: Bs[dy:, :] = False
    if dx >= 0: Bs[:, :dx] = False
    else: Bs[:, dx:] = False
    Hh = min(A.shape[0], Bs.shape[0]); Ww = min(A.shape[1], Bs.shape[1])
    a = A[:Hh, :Ww]; b = Bs[:Hh, :Ww]
    inter = (a & b).sum(); union = (a | b).sum()
    return inter / union if union else 0


def register(ref_body_native, ref_h, mov_body_native, mov_bbox):
    """find scale s and translation t (R full-space) aligning mov body to ref body."""
    g = 256.0 / ref_body_native.shape[1]              # native R -> small R
    A = np.array(Image.fromarray(ref_body_native.astype(np.uint8) * 255)
                 .resize((round(ref_body_native.shape[1] * g), round(ref_body_native.shape[0] * g)), Image.NEAREST)) > 0
    mh = mov_bbox[3] - mov_bbox[1]
    s0 = ref_h / mh
    best = (-1, None, None)
    for s in s0 * np.linspace(0.80, 1.20, 41):
        nw = max(1, round(mov_body_native.shape[1] * s * g))
        nh = max(1, round(mov_body_native.shape[0] * s * g))
        B = np.array(Image.fromarray(mov_body_native.astype(np.uint8) * 255).resize((nw, nh), Image.NEAREST)) > 0
        dy, dx = fft_best_shift(A, B)
        sc = iou_at(A, B, dy, dx)
        if sc > best[0]:
            best = (sc, s, (dy / g, dx / g))   # t back to R full-space
    return best  # (iou, scale, (ty,tx))


def main():
    # reference
    ref_im = make_transparent(Image.open(os.path.join(ORIG, REF)))
    ref_mask, ref_bbox = main_blob_mask(ref_im)
    ref_body = body_mask(ref_mask, ref_bbox)
    ref_h = ref_bbox[3] - ref_bbox[1]

    # placement from reference full main blob (head+body) -> consistent with other sprites
    rx0, ry0, rx1, ry1 = ref_bbox
    scale_place = TARGET_BODY_W / (rx1 - rx0)
    rcx = (rx0 + rx1) / 2
    place = (CENTER_X - scale_place * rcx, GROUND_Y - scale_place * ry1)  # (ox, oy)

    # native blob width = proxy for the real detail each frame carries
    transforms = {REF: (1.0, (0.0, 0.0), ref_im, rx1 - rx0)}
    for f in MOVING:
        im = make_transparent(Image.open(os.path.join(ORIG, f)))
        mask, bbox = main_blob_mask(im)
        mb = body_mask(mask, bbox)
        iou, s, (ty, tx) = register(ref_body, ref_h, mb, bbox)
        print(f"{f}: IoU={iou:.3f} scale={s:.4f} t=({tx:.1f},{ty:.1f})")
        transforms[f] = (s, (tx, ty), im, bbox[2] - bbox[0])

    # sharpness matching: level every frame down to the lowest real resolution
    # (working-a) so fast frame swaps don't flicker in detail.
    min_w = min(t[3] for t in transforms.values())

    for f, (s, (tx, ty), im, native_w) in transforms.items():
        final_scale = scale_place * s
        ox = scale_place * tx + place[0]
        oy = scale_place * ty + place[1]
        soft = min_w / native_w          # <=1 ; downsample sharper frames first
        nw, nh = max(1, round(im.width * final_scale)), max(1, round(im.height * final_scale))
        if soft < 0.999:
            iw, ih = max(1, round(im.width * final_scale * soft)), max(1, round(im.height * final_scale * soft))
            im_s = im.resize((iw, ih), Image.LANCZOS).resize((nw, nh), Image.LANCZOS)
        else:
            im_s = im.resize((nw, nh), Image.LANCZOS)
        canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
        canvas.alpha_composite(im_s, (round(ox), round(oy)))
        canvas.save(os.path.join(OUT, f))
        print("wrote", f, "final_scale=%.4f soft=%.3f" % (final_scale, soft))


if __name__ == "__main__":
    main()
