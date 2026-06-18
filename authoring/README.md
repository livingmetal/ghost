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

## Recovered Legacy Material

The initial contents were recovered from the removed
`src/LivingMetalGhost/Ghost` snapshot:

- Orkia body, face, hair, head, and reference assets;
- ssyong sprite-normalization scripts and instructions.

These files were preserved because no byte-identical active copy existed.
