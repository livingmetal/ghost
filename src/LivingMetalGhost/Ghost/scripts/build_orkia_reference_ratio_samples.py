from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ORKIA = ROOT / "src" / "LivingMetalGhost" / "Assets" / "Characters" / "Orkia"
BASE = ORKIA / "References" / "orkia-fullbody-user-ratio-alpha-v1.png"
SOURCE = ORKIA / "CharacterBases" / "_backup_fullbody_crop_20260613"
OUTPUT = ORKIA / "References" / "reference-ratio-samples"


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Image has no visible pixels")
    return bbox


def build_sample(source_path: Path, output_path: Path) -> None:
    base = Image.open(BASE).convert("RGBA")
    pose = Image.open(source_path).convert("RGBA")
    base_box = alpha_bbox(base)
    pose_box = alpha_bbox(pose)

    # Match the established full-body silhouette by visible character width.
    target_width = base_box[2] - base_box[0]
    source_width = pose_box[2] - pose_box[0]
    scale = target_width / source_width
    resized = pose.resize(
        (round(pose.width * scale), round(pose.height * scale)),
        Image.Resampling.LANCZOS,
    )
    resized_box = alpha_bbox(resized)

    base_center_x = (base_box[0] + base_box[2]) / 2
    resized_center_x = (resized_box[0] + resized_box[2]) / 2
    x = round(base_center_x - resized_center_x)
    y = round(base_box[1] - resized_box[1])

    layer = Image.new("RGBA", base.size)
    layer.alpha_composite(resized, (x, y))

    # Retain the original pose above the skirt while using the coherent
    # reference body from the upper thighs downward.
    mask = layer.getchannel("A")
    vertical = Image.new("L", base.size, 0)
    draw = ImageDraw.Draw(vertical)
    fade_start = round(base.height * 0.465)
    fade_end = round(base.height * 0.501)
    draw.rectangle((0, 0, base.width, fade_start), fill=255)
    for row in range(fade_start, fade_end):
        value = round(255 * (fade_end - row) / (fade_end - fade_start))
        draw.line((0, row, base.width, row), fill=value)
    vertical = vertical.filter(ImageFilter.GaussianBlur(radius=3))
    mask = ImageChops.multiply(mask, vertical)
    layer.putalpha(mask)

    base_alpha = ImageChops.multiply(base.getchannel("A"), ImageChops.invert(vertical))
    base.putalpha(base_alpha)
    result = Image.alpha_composite(base, layer)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    result.save(output_path, optimize=True)


def main() -> None:
    for name in ("approved-neutral.png", "approved-thinking.png"):
        build_sample(SOURCE / name, OUTPUT / name)


if __name__ == "__main__":
    main()
