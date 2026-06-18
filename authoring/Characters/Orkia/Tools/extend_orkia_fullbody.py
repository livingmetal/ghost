from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import shutil

import numpy as np
from PIL import Image


TARGET_SIZE = (1024, 1536)
TARGET_BBOX_TOP = 70
TARGET_UPPER_BBOX_WIDTH = 590
KEEP_UNTIL_Y = 820
FADE_END_Y = 1120
CENTER_FAST_FADE_START = 430
CENTER_FAST_FADE_END = 590
CENTER_SOFT_FADE_START = 390
CENTER_SOFT_FADE_END = 630


@dataclass
class Paths:
    root: Path
    base_dir: Path
    reference_path: Path
    backup_dir: Path


def load_paths() -> Paths:
    root = Path(__file__).resolve().parents[4]
    authoring_root = Path(__file__).resolve().parents[1]
    base_dir = root / "src" / "LivingMetalGhost" / "Assets" / "Characters" / "Orkia" / "CharacterBases"
    reference_path = authoring_root / "References" / "orkia-fullbody-neutral-reference-v1.png"
    backup_dir = authoring_root / "SourceBases" / "fullbody-crop-20260613"
    return Paths(root, base_dir, reference_path, backup_dir)


def fit_reference(reference_path: Path) -> Image.Image:
    ref = Image.open(reference_path).convert("RGBA")
    scale = TARGET_SIZE[1] / ref.height
    resized = ref.resize(
        (round(ref.width * scale), TARGET_SIZE[1]),
        Image.Resampling.LANCZOS,
    )

    canvas = Image.new("RGBA", TARGET_SIZE, (0, 0, 0, 0))
    offset_x = (TARGET_SIZE[0] - resized.width) // 2
    canvas.alpha_composite(resized, (offset_x, 0))
    return canvas


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        return (0, 0, 0, 0)
    return bbox


def compute_transform(image: Image.Image) -> tuple[float, int, int]:
    left, top, right, _ = alpha_bbox(image)
    bbox_width = max(1, right - left)
    scale = TARGET_UPPER_BBOX_WIDTH / bbox_width
    scale = float(min(0.83, max(0.75, scale)))

    resized_bbox_left = left * scale
    resized_bbox_top = top * scale
    resized_bbox_width = bbox_width * scale

    x = round((TARGET_SIZE[0] - resized_bbox_width) / 2 - resized_bbox_left)
    y = round(TARGET_BBOX_TOP - resized_bbox_top)
    return scale, x, y


def create_overlay_mask(width: int, height: int, offset_x: int, offset_y: int) -> Image.Image:
    y_indices = np.arange(height, dtype=np.float32) + offset_y
    x_indices = np.arange(width, dtype=np.float32) + offset_x
    global_y = np.repeat(y_indices[:, None], width, axis=1)
    global_x = np.repeat(x_indices[None, :], height, axis=0)

    row_mask = np.where(
        global_y <= KEEP_UNTIL_Y,
        255.0,
        np.where(
            global_y >= FADE_END_Y,
            0.0,
            255.0 * (1.0 - ((global_y - KEEP_UNTIL_Y) / (FADE_END_Y - KEEP_UNTIL_Y))),
        ),
    )

    fast_band = (global_x >= CENTER_FAST_FADE_START) & (global_x <= CENTER_FAST_FADE_END)
    soft_band = (global_x >= CENTER_SOFT_FADE_START) & (global_x <= CENTER_SOFT_FADE_END) & ~fast_band

    fast_factor = np.clip(1.0 - ((global_y - KEEP_UNTIL_Y) / 180.0), 0.0, 1.0)
    soft_factor = np.clip(1.0 - ((global_y - KEEP_UNTIL_Y) / 260.0), 0.15, 1.0)

    row_mask = np.where(fast_band, row_mask * fast_factor, row_mask)
    row_mask = np.where(soft_band, row_mask * soft_factor, row_mask)

    return Image.fromarray(np.clip(row_mask, 0, 255).astype(np.uint8), "L")


def extend_sprite(source: Path, original_source: Path, reference_canvas: Image.Image) -> None:
    original = Image.open(original_source).convert("RGBA")
    scale, offset_x, offset_y = compute_transform(original)
    resized = original.resize(
        (round(original.width * scale), round(original.height * scale)),
        Image.Resampling.LANCZOS,
    )

    overlay_mask = create_overlay_mask(resized.width, resized.height, offset_x, offset_y)
    alpha = np.array(resized.getchannel("A"), dtype=np.float32)
    mask = (np.array(overlay_mask, dtype=np.float32) * (alpha / 255.0)).astype(np.uint8)

    overlay = Image.new("RGBA", TARGET_SIZE, (0, 0, 0, 0))
    overlay.paste(resized, (offset_x, offset_y), Image.fromarray(mask, "L"))

    result = reference_canvas.copy()
    result.alpha_composite(overlay)
    result.save(source)


def backup_file(source: Path, backup_dir: Path) -> None:
    backup_dir.mkdir(parents=True, exist_ok=True)
    destination = backup_dir / source.name
    if not destination.exists():
        shutil.copy2(source, destination)


def main() -> None:
    paths = load_paths()
    reference_canvas = fit_reference(paths.reference_path)

    for source in sorted(paths.base_dir.glob("approved-*.png")):
        backup_file(source, paths.backup_dir)
        original_source = paths.backup_dir / source.name
        extend_sprite(source, original_source if original_source.exists() else source, reference_canvas)
        print(f"Updated {source.name}")


if __name__ == "__main__":
    main()
