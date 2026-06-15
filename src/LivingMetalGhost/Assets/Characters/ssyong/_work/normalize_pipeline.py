"""
Normalize ssyong cat sprites:
  1) transparent background (flood-fill near-white from borders)
  2) align by the cat's BACK: scale every sprite so the main body blob has a
     uniform width, then anchor by body bottom (ground line) + horizontal
     center, so only the head / speech bubble appear to move.
  3) uniform canvas size with transparent background.
"""
import os, glob
import numpy as np
from collections import deque
from PIL import Image

SRC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # .../ssyong
OUT = os.path.join(SRC, "_work", "out")
os.makedirs(OUT, exist_ok=True)

CANVAS_W, CANVAS_H = 1024, 640
TARGET_BODY_W = 720      # main-body blob width after scaling
GROUND_Y      = 600      # body bottom lands here
CENTER_X      = 512      # main-body horizontal center lands here

FILES = ["awaken.png", "awaken-close-eye.png",
         "speak-a.png", "speak-b.png",
         "working-a.png", "working-b.png", "working-close-eye.png",
         "sleep.png"]


def make_transparent(im):
    """Return RGBA with near-white border background removed (flood fill)."""
    im = im.convert("RGBA")
    a = np.array(im)
    h, w = a.shape[:2]
    rgb = a[:, :, 3] if False else a[:, :, :3].astype(int)
    nearwhite = (rgb > 238).all(axis=2)
    already_transparent = a[:, :, 3] < 16
    bg = nearwhite | already_transparent
    vis = np.zeros((h, w), bool)
    dq = deque()
    for y in range(h):
        for x in (0, w - 1):
            if bg[y, x] and not vis[y, x]:
                vis[y, x] = True; dq.append((y, x))
    for x in range(w):
        for y in (0, h - 1):
            if bg[y, x] and not vis[y, x]:
                vis[y, x] = True; dq.append((y, x))
    while dq:
        y, x = dq.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not vis[ny, nx] and bg[ny, nx]:
                vis[ny, nx] = True; dq.append((ny, nx))
    a[vis, 3] = 0
    return Image.fromarray(a)


def main_blob_bbox(im):
    """BBox of the largest connected alpha component (excludes detached
    speech bubbles / motion lines). Computed on a downscaled mask for speed."""
    a = np.array(im)
    mask_full = a[:, :, 3] > 30
    H, W = mask_full.shape
    sw = 256
    sh = max(1, round(H * sw / W))
    small = np.array(Image.fromarray(mask_full.astype(np.uint8) * 255)
                     .resize((sw, sh), Image.NEAREST)) > 0
    # label
    labels = np.zeros((sh, sw), int)
    cur = 0
    best = (0, None)
    for sy in range(sh):
        for sx in range(sw):
            if small[sy, sx] and labels[sy, sx] == 0:
                cur += 1
                dq = deque([(sy, sx)]); labels[sy, sx] = cur
                cnt = 0
                minx = maxx = sx; miny = maxy = sy
                while dq:
                    y, x = dq.popleft(); cnt += 1
                    if x < minx: minx = x
                    if x > maxx: maxx = x
                    if y < miny: miny = y
                    if y > maxy: maxy = y
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < sh and 0 <= nx < sw and small[ny, nx] and labels[ny, nx] == 0:
                            labels[ny, nx] = cur; dq.append((ny, nx))
                if cnt > best[0]:
                    best = (cnt, (minx, miny, maxx, maxy))
    minx, miny, maxx, maxy = best[1]
    fx = W / sw; fy = H / sh
    return (minx * fx, miny * fy, (maxx + 1) * fx, (maxy + 1) * fy)


def normalize(fname):
    im = make_transparent(Image.open(os.path.join(SRC, fname)))
    bx0, by0, bx1, by1 = main_blob_bbox(im)
    bw = bx1 - bx0
    s = TARGET_BODY_W / bw
    nw, nh = round(im.width * s), round(im.height * s)
    im_s = im.resize((nw, nh), Image.LANCZOS)
    body_cx = (bx0 + bx1) / 2 * s
    body_bottom = by1 * s
    off_x = round(CENTER_X - body_cx)
    off_y = round(GROUND_Y - body_bottom)
    canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
    canvas.alpha_composite(im_s, (off_x, off_y))
    return canvas


if __name__ == "__main__":
    for f in FILES:
        out = normalize(f)
        out.save(os.path.join(OUT, f))
        print("wrote", f)
