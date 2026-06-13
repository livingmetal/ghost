from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
REFERENCES = (
    ROOT
    / "src"
    / "LivingMetalGhost"
    / "Assets"
    / "Characters"
    / "Orkia"
    / "References"
)
SOURCE = REFERENCES / "orkia-fullbody-user-ratio-alpha-v1.png"
OUTPUT = REFERENCES / "orkia-fullbody-shorter-legs-alpha-v2.png"

# Keep the thighs and knee placement unchanged. Compress only the long
# stocking-covered calf section, then move the boots upward intact.
CALF_TOP = 1030
BOOT_TOP = 1450
CALF_TARGET_HEIGHT = 300


image = Image.open(SOURCE).convert("RGBA")
top = image.crop((0, 0, image.width, CALF_TOP))
calf = image.crop((0, CALF_TOP, image.width, BOOT_TOP))
bottom = image.crop((0, BOOT_TOP, image.width, image.height))

calf = calf.resize((image.width, CALF_TARGET_HEIGHT), Image.Resampling.LANCZOS)
new_height = top.height + calf.height + bottom.height
result = Image.new("RGBA", (image.width, new_height), (0, 0, 0, 0))
result.alpha_composite(top, (0, 0))
result.alpha_composite(calf, (0, top.height))
result.alpha_composite(bottom, (0, top.height + calf.height))

bbox = result.getchannel("A").getbbox()
if bbox is None:
    raise RuntimeError("Shortened full-body base has no visible pixels")

# Preserve the original horizontal center while trimming unused bottom space.
result = result.crop((0, 0, result.width, min(result.height, bbox[3] + 55)))
result.save(OUTPUT, optimize=True)
print(f"Wrote {OUTPUT} ({result.width}x{result.height})")
