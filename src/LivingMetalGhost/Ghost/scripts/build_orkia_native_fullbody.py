from pathlib import Path

from PIL import Image, ImageChops, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ORKIA = ROOT / "src" / "LivingMetalGhost" / "Assets" / "Characters" / "Orkia"
CHARACTER_BASES = ORKIA / "CharacterBases"
SOURCE_BASES = CHARACTER_BASES / "_backup_fullbody_crop_20260613"
FULL_BODY_BASE = ORKIA / "References" / "orkia-fullbody-shorter-legs-alpha-v2.png"
NEUTRAL_SOURCE = SOURCE_BASES / "approved-neutral.png"
MISSING_POSE_FALLBACKS = {
    "approved-apologetic.png": "approved-thinking-blush.png",
    "approved-determined.png": "approved-strict.png",
    "approved-listening.png": "approved-neutral.png",
}


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Image has no visible pixels")
    return bbox


def place_source(
    source: Image.Image,
    canvas_size: tuple[int, int],
    scale: float,
    base_box: tuple[int, int, int, int],
) -> Image.Image:
    resized = source.resize(
        (round(source.width * scale), round(source.height * scale)),
        Image.Resampling.LANCZOS,
    )
    resized_box = alpha_bbox(resized)
    base_center_x = (base_box[0] + base_box[2]) / 2
    x = round(base_center_x - resized.width / 2)
    y = round(base_box[1] - resized_box[1])
    layer = Image.new("RGBA", canvas_size)
    layer.alpha_composite(resized, (x, y))
    return layer


def build_sprite(
    source_path: Path,
    destination_path: Path,
    base: Image.Image,
    neutral_layer: Image.Image,
    scale: float,
    base_box: tuple[int, int, int, int],
) -> None:
    source = Image.open(source_path).convert("RGBA")
    pose_layer = place_source(source, base.size, scale, base_box)

    # Extract only pixels that differ from the original neutral pose. The
    # complete skirt, thighs, stockings, and boots always come from one base.
    difference = ImageChops.difference(pose_layer, neutral_layer)
    difference = difference.convert("RGB")
    red, green, blue = difference.split()
    change_mask = ImageChops.lighter(ImageChops.lighter(red, green), blue)
    change_mask = change_mask.point(lambda value: 255 if value >= 12 else 0)
    change_mask = change_mask.filter(ImageFilter.MaxFilter(11))
    change_mask = change_mask.filter(ImageFilter.GaussianBlur(1.2))

    # Old pose sources end mid-thigh; never allow their lower edge into output.
    cutoff = Image.new("L", base.size, 0)
    cutoff.paste(255, (0, 0, base.width, 910))
    change_mask = ImageChops.multiply(change_mask, cutoff)
    change_mask = ImageChops.multiply(change_mask, pose_layer.getchannel("A"))
    pose_layer.putalpha(change_mask)

    result = Image.alpha_composite(base.copy(), pose_layer)
    result.save(destination_path, optimize=True)


def main() -> None:
    base = Image.open(FULL_BODY_BASE).convert("RGBA")
    neutral = Image.open(NEUTRAL_SOURCE).convert("RGBA")
    base_box = alpha_bbox(base)
    neutral_box = alpha_bbox(neutral)
    scale = (base_box[2] - base_box[0]) / (neutral_box[2] - neutral_box[0])
    neutral_layer = place_source(neutral, base.size, scale, base_box)

    sources = sorted(SOURCE_BASES.glob("approved-*.png"))
    if not sources:
        raise RuntimeError("Orkia pose sources are missing.")

    for source in sources:
        build_sprite(
            source,
            CHARACTER_BASES / source.name,
            base,
            neutral_layer,
            scale,
            base_box,
        )

    for destination_name, source_name in MISSING_POSE_FALLBACKS.items():
        build_sprite(
            SOURCE_BASES / source_name,
            CHARACTER_BASES / destination_name,
            base,
            neutral_layer,
            scale,
            base_box,
        )


if __name__ == "__main__":
    main()
