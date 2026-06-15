#!/usr/bin/env python3
"""
Normalize ssyong character sprite PNGs so sprite swapping does not jitter.

What this does:
- Reads manifest.json and collects referenced PNG sprites.
- Skips empty or unreadable files by default.
- Optionally repairs empty files by copying a configured fallback sprite.
- Places every valid sprite on the same transparent canvas.
- Aligns the visible alpha bounding box by bottom-center anchor.
- Writes output to ./normalized by default, preserving originals.

Usage:
  py normalize_sprites.py
  py normalize_sprites.py --repair-empty
  py normalize_sprites.py --apply
  py normalize_sprites.py --canvas 642x414 --anchor-y-offset 12
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path
from typing import Iterable

try:
    from PIL import Image
except ImportError:
    print("[ERROR] Pillow is not installed.")
    print("        Run: py -m pip install pillow")
    sys.exit(1)

DEFAULT_CANVAS = (642, 414)
DEFAULT_ANCHOR_Y_OFFSET = 12

# These fallbacks prevent runtime blinking / working frames from disappearing
# when the referenced PNG exists but is empty. They are intentionally conservative.
EMPTY_FILE_FALLBACKS = {
    "awaken-close-eye.png": "awaken.png",
    "working-b.png": "working-a.png",
    "working-close-eye.png": "working-a.png",
}


def parse_canvas(value: str) -> tuple[int, int]:
    try:
        w, h = value.lower().split("x", 1)
        return int(w), int(h)
    except Exception as exc:
        raise argparse.ArgumentTypeError("canvas must look like 642x414") from exc


def collect_pngs_from_manifest(manifest_path: Path) -> list[str]:
    if not manifest_path.exists():
        return []

    data = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    sprites = data.get("visual", {}).get("sprites", {})
    found: set[str] = set()

    def walk(value):
        if isinstance(value, str) and value.lower().endswith(".png"):
            found.add(value)
        elif isinstance(value, list):
            for item in value:
                walk(item)
        elif isinstance(value, dict):
            for item in value.values():
                walk(item)

    walk(sprites)
    return sorted(found)


def alpha_bbox(img: Image.Image):
    rgba = img.convert("RGBA")
    return rgba.getchannel("A").getbbox()


def normalize_one(src_path: Path, out_path: Path, canvas: tuple[int, int], anchor_y_offset: int) -> str:
    img = Image.open(src_path).convert("RGBA")
    bbox = alpha_bbox(img)

    if bbox is None:
        return f"[SKIP] {src_path.name}: fully transparent"

    canvas_w, canvas_h = canvas
    left, top, right, bottom = bbox

    # Anchor: visible bottom-center of the sprite.
    src_anchor_x = (left + right) // 2
    src_anchor_y = bottom

    target_anchor_x = canvas_w // 2
    target_anchor_y = canvas_h - anchor_y_offset

    paste_x = target_anchor_x - src_anchor_x
    paste_y = target_anchor_y - src_anchor_y

    result = Image.new("RGBA", canvas, (0, 0, 0, 0))
    result.alpha_composite(img, (paste_x, paste_y))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    result.save(out_path)

    return (
        f"[OK] {src_path.name}: {img.size[0]}x{img.size[1]} -> "
        f"{canvas_w}x{canvas_h}, paste=({paste_x},{paste_y})"
    )


def backup_originals(base_dir: Path, filenames: Iterable[str]) -> Path:
    backup_dir = base_dir / "_backup_before_normalize"
    backup_dir.mkdir(exist_ok=True)

    for name in filenames:
        src = base_dir / name
        if src.exists() and src.is_file() and src.stat().st_size > 0:
            shutil.copy2(src, backup_dir / name)

    return backup_dir


def repair_empty_files(base_dir: Path, filenames: Iterable[str]) -> list[str]:
    messages: list[str] = []
    names = set(filenames)

    for target_name, fallback_name in EMPTY_FILE_FALLBACKS.items():
        if target_name not in names:
            continue

        target = base_dir / target_name
        fallback = base_dir / fallback_name

        if target.exists() and target.stat().st_size > 0:
            continue

        if not fallback.exists() or fallback.stat().st_size == 0:
            messages.append(f"[WARN] cannot repair {target_name}: fallback {fallback_name} missing or empty")
            continue

        shutil.copy2(fallback, target)
        messages.append(f"[REPAIR] {target_name}: copied from {fallback_name}")

    return messages


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--canvas", type=parse_canvas, default=DEFAULT_CANVAS)
    parser.add_argument("--anchor-y-offset", type=int, default=DEFAULT_ANCHOR_Y_OFFSET)
    parser.add_argument("--out", default="normalized")
    parser.add_argument("--apply", action="store_true", help="overwrite original PNGs after creating a backup")
    parser.add_argument("--repair-empty", action="store_true", help="copy fallback sprites into empty referenced files")
    parser.add_argument("--all-png", action="store_true", help="process every PNG in the folder, not only manifest references")
    args = parser.parse_args()

    base_dir = Path(__file__).resolve().parent
    manifest_path = base_dir / "manifest.json"

    if args.all_png:
        filenames = sorted(p.name for p in base_dir.glob("*.png"))
    else:
        filenames = collect_pngs_from_manifest(manifest_path)

    if not filenames:
        print("[ERROR] No PNG files found to normalize.")
        return 2

    print(f"[INFO] base: {base_dir}")
    print(f"[INFO] canvas: {args.canvas[0]}x{args.canvas[1]}")
    print(f"[INFO] files: {', '.join(filenames)}")

    if args.repair_empty:
        for message in repair_empty_files(base_dir, filenames):
            print(message)

    out_dir = base_dir / args.out
    ok_files: list[str] = []

    for name in filenames:
        src = base_dir / name
        out = out_dir / name

        if not src.exists():
            print(f"[WARN] {name}: missing")
            continue

        if src.stat().st_size == 0:
            print(f"[WARN] {name}: empty. Run with --repair-empty or replace this PNG manually.")
            continue

        try:
            print(normalize_one(src, out, args.canvas, args.anchor_y_offset))
            ok_files.append(name)
        except Exception as exc:
            print(f"[ERROR] {name}: {exc}")

    if args.apply and ok_files:
        backup_dir = backup_originals(base_dir, ok_files)
        for name in ok_files:
            shutil.copy2(out_dir / name, base_dir / name)
        print(f"[APPLY] overwritten originals. Backup: {backup_dir}")
    else:
        print(f"[DONE] normalized files written to: {out_dir}")
        print("       Review them first. To overwrite originals, run: py normalize_sprites.py --repair-empty --apply")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
