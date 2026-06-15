from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ORKIA = ROOT / "src" / "LivingMetalGhost" / "Assets" / "Characters" / "Orkia"
BASE = ORKIA / "References" / "orkia-fullbody-shorter-legs-alpha-v2.png"
SOURCE = (
    ORKIA
    / "CharacterBases"
    / "_backup_fullbody_crop_20260613"
    / "approved-arms-crossed.png"
)
OUTPUT = ORKIA / "CharacterBases" / "approved-arms-crossed.png"


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Image has no visible pixels")
    return bbox


base = Image.open(BASE).convert("RGBA")
source = Image.open(SOURCE).convert("RGBA")
neutral = Image.open(
    ORKIA
    / "CharacterBases"
    / "_backup_fullbody_crop_20260613"
    / "approved-neutral.png"
).convert("RGBA")

base_box = alpha_bbox(base)
neutral_box = alpha_bbox(neutral)
scale = (base_box[2] - base_box[0]) / (neutral_box[2] - neutral_box[0])
source = source.resize(
    (round(source.width * scale), round(source.height * scale)),
    Image.Resampling.LANCZOS,
)
source_box = alpha_bbox(source)
x = round((base_box[0] + base_box[2]) / 2 - source.width / 2)
y = round(base_box[1] - source_box[1])

layer = Image.new("RGBA", base.size)
layer.alpha_composite(source, (x, y))
# Switch inside the dark skirt hem, where both images share nearly identical
# pixels. This leaves exactly one set of thighs and removes the cropped source.
cut_y = 880
alpha = layer.getchannel("A")
alpha_pixels = alpha.load()
for py in range(cut_y, base.height):
    for px in range(base.width):
        alpha_pixels[px, py] = 0
layer.putalpha(alpha)

result = Image.alpha_composite(base, layer)
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
result.save(OUTPUT, optimize=True)
