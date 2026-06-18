# Character asset prompt metadata

## Task

Add prompt package metadata for character asset generation without adding any generation API integration.

## Changed files

- `config.template.json`
- `src/LivingMetalGhost/Core/Models/CharacterImagePromptModels.cs`
- `src/LivingMetalGhost/Core/Services/CharacterImagePromptBuilder.cs`
- `src/LivingMetalGhost/Assets/Characters/Orkia/image-prompt.json`
- `src/LivingMetalGhost/Assets/Characters/Orkia/README.md`

## Changes

- Kept `character_profiles` empty in the config template so manifest defaults stay the baseline.
- Added reusable prompt package records.
- Added a pure builder that reads character-side prompt metadata and returns text packages only.
- Added Orkia locked identity, negative drift rules, references, view/pose/mood text.
- Documented Orkia prompt source-of-truth rules.

## Explicit non-goals

- No generation API integration.
- No automatic file generation.
- No automatic replacement of character assets.
- No changes to the public `CharacterProfile` constructor shape.

## Verification

Not run locally in this session.

Expected CI/local verification:

```powershell
.\scripts\verify.ps1 -Configuration Release
```

## Remaining risk

- The builder is currently infrastructure-only. It is not yet wired into UI or commands.
- No unit tests exist for the builder yet.
