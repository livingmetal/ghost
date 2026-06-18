# Orkia Offline Tools

These scripts process authoring inputs and export approved images into the
runtime Orkia `CharacterBases` directory. They do not call image-generation
APIs.

Expected authoring inputs live under:

```text
authoring/Characters/Orkia/
  References/
  SourceBases/fullbody-crop-20260613/
```

Several historical source files were never tracked. A script should fail
clearly when a required input is absent; do not silently substitute generated
or downloaded material.
