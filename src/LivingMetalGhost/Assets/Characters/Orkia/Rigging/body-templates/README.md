# Orkia Body Templates

These SVG files are not final character art.

They are pose-fitting templates for future Orkia puppet-rig assets. Use them as transparent guide layers when drawing body sprites, outfit overlays, and accessories.

## Rules

1. Keep the canvas at `300 x 598` unless the main Orkia visual size changes.
2. Keep anchors stable. Clothes and accessories should fit these anchors first.
3. Do not paint final art directly into these files. Copy the template into a drawing program as a guide layer.
4. Save derived final PNG parts under `../parts/`, `../outfits/`, or `../accessories/`.
5. Original approved full-state images under `../CharacterBases/` remain untouched.

## Template set

| File | Purpose |
|---|---|
| `neutral_stand.svg` | First/default pose for idle, listening, and speaking. |
| `thinking_tilt.svg` | Slight head tilt for thinking, skeptical, confused, and concerned states. |
| `explain_hand.svg` | One-hand explanation pose for teaching or architecture explanation. |

## Anchor convention

The SVGs mark these anchors:

```text
head
neck
torso
left_shoulder
right_shoulder
left_elbow
right_elbow
left_hand
right_hand
hip
```

The matching numeric anchor values live in `../rig-manifest.draft.json`.
