# Character Authoring Assets

This directory contains source art, references, and offline asset-processing
tools. It is not part of the runtime character asset tree and is not copied to
build or publish output.

## Rules

- Runtime manifests must reference files under
  `src/LivingMetalGhost/Assets/Characters`, not this directory.
- Do not add automatic image-generation or character-generation integrations.
- Treat files here as input material for deliberate, reviewed asset work.
- Export approved runtime sprites into the relevant runtime character directory.
- Preserve canvas, alignment, identity, and fallback rules documented in
  `docs/SPRITE_ARCHITECTURE.md`.
- Keep generated caches such as `__pycache__` and `*.pyc` untracked.

## Layout

- `Characters/<id>/Originals`: retained source images.
- `Characters/<id>/Work`: intermediate files and one-off composition scripts.
- `Characters/<id>/References`: visual reference material.
- `Characters/<id>/Rigging`: future rigging designs and draft assets.
- `Characters/<id>/Tools`: offline export and normalization tools.

Some historical tools require source inputs that were never tracked. Their
paths now resolve within the relevant character authoring directory, but the
missing inputs must be supplied deliberately before those tools can run.

## Recovered Legacy Material

The initial contents were recovered from the removed
`src/LivingMetalGhost/Ghost` snapshot:

- Orkia body, face, hair, head, and reference assets;
- ssyong sprite-normalization scripts and instructions.

These files were preserved because no byte-identical active copy existed.

The active runtime tree was subsequently reduced to manifests, sidecar runtime
metadata, and shipped sprite files. Existing `_work`, `_original`, `References`,
and `Rigging` material was moved here without changing runtime sprite paths.
