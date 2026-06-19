# Character and Sprite Architecture

This document defines the current character presentation boundary and the
allowed direction for future rigged sprite work.

## Current Behavior

Characters are discovered from `manifest.json` files under:

```text
Assets/Characters
%APPDATA%\LivingMetalGhost\Characters
```

The current runtime supports existing manifest-defined visual profiles:

- full-frame sprite images and speaking frames;
- manifest-layer modular states already used by current assets.

`Core/Characters/Models/CharacterCatalog.cs` loads manifests and resolves asset
paths. `Core/Characters/Presentation/SpriteDirector.cs` maps conversation state
to a mood. `UI/CharacterPresentation/CharacterHost.*` renders the selected
visual, handles framing, caches images, and runs blink/speaking/mood animation.

These behaviors are production compatibility requirements during refactoring.

## Mode Policy

- The neutral visual state is always `idle`.
- Blinking is an idle-state renderer behavior.
- Speaking frames are controlled by the separate `IsSpeaking` signal, so mouth
  movement does not require a speaking mood.
- Daily/idle mode uses an expressive state only when the LLM supplies a valid
  mood.
- Roleplay mode has an independent conversation channel and uses an expressive
  state only when the LLM supplies a valid mood for visual-novel expression.
- Advanced mode shares conversation history with Daily mode but does not use
  LLM-selected moods.
- Advanced visual behavior will be deterministic and limited to approved
  blinking, pose changes, and speaking frames.

`CharacterExpressionPolicy` owns neutral fallback, explicit-mood normalization,
and post-speech hold timing. `SpriteDirector` remains a compatibility facade for
the current ViewModel.

This policy defines an integration boundary only. The current refactor does not
implement new sprite animation, pose assets, rigging, or composition behavior.

## Current Scope

- Keep the current selected character working.
- Keep current sprite and manifest-layer rendering behavior.
- Preserve mood mapping, speaking frames, blinking, framing, and scale.
- Do not add automatic character creation.
- Do not add an image-generation API.
- Do not build a new rigging or part-composition engine during architecture
  cleanup.

Existing image prompt metadata and rigging drafts are asset-authoring references,
not authorization to add runtime image generation.

`Core/Characters/Services/CharacterImagePromptBuilder.cs` only assembles
authoring metadata. It must not call an image-generation API.

## Target Boundary

Future render strategies should fit behind a stable character-presentation
contract:

```text
Conversation result
  -> Character visual state
      -> Character presentation coordinator
          -> Current sprite renderer
          -> Current manifest-layer renderer
          -> Future optional rigged renderer
```

The conversation layer should produce semantic state such as `thinking`,
`concerned`, or `speaking`. It must not select PNG paths or WPF layers.

The renderer should receive:

- character identity;
- semantic visual state;
- speaking status;
- framing and scale settings.

It should return or display a visual frame without knowing about LLM providers,
Story persistence, command execution, or agent state.

## Future Rigging Constraints

A future rigged renderer may compose approved sprite parts, but it must:

- remain optional;
- use manifest-defined assets;
- fall back to current rendering when data or parts are missing;
- preserve the same logical canvas and framing behavior;
- avoid changing character identity or generating assets automatically;
- be introduced independently from UI mode and conversation refactoring.

The immediate architectural goal is only to create a seam where another renderer
could be added later.

## Asset Policy

Runtime assets, authoring sources, references, and experimental rigging files
should eventually be separated:

```text
Character/
  runtime/       manifests and shipped assets
  authoring/     source art and generation/editing scripts
  design/        rigging policy and drafts
```

Do not perform this move until current project copy rules and manifest paths are
covered by tests or a verification script.

## Refactoring Sequence

1. Characterize current manifest loading and fallback behavior.
2. Extract visual-profile models from the static catalog implementation.
3. Introduce a character catalog interface.
4. Extract image loading/cache from `CharacterHost`.
5. Separate sprite and manifest-layer render strategies.
6. Keep `CharacterHost` as the WPF host/coordinator.
7. Consider a future rigged renderer only after the boundary is stable.

No part-composition implementation is included in the current refactor scope.
