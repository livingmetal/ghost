# Story and Roleplay

Story mode is a fictional visual-novel/ORPG-style experience. Its state, memory,
and prompts are isolated from Daily conversation and Advanced work.

## Player Input

The current grammar is:

```text
plain text      -> spoken dialogue heard by characters
**text**        -> visible action or scene narration
(text)          -> private inner thought
```

Single asterisks are intentionally not action syntax. Text such as `*A*B*`
remains spoken dialogue.

The character must not quote, answer, acknowledge, or reveal awareness of a
private thought. If the input contains only private thought, the character may
continue with a small ambient beat without reacting to the thought.

## Player Agency

The application and model must not decide the player's:

- actions;
- dialogue;
- thoughts;
- emotions;
- choices.

Story responses should advance one small beat at a time and end with a concrete
reaction, hook, or immediate question. Prewritten choice lists are not the
default UI.

## State and Templates

Story state is stored under:

```text
%APPDATA%\LivingMetalGhost\Stories\default
```

Character-specific opening templates are loaded from:

```text
Assets/Characters/<Character>/story-default.json
```

The active character is selected through application configuration. A persisted
Story state can override template content until the Story state is reset.

Primary implementation files:

- `Core/Roleplay/Services/RoleplayInputFormatter.cs`
- `Core/Roleplay/Services/RoleplayStateUpdater.cs`
- `Core/Roleplay/Services/StoryStateStore.cs`
- `Core/Roleplay/Services/StoryTemplateCatalog.cs`
- `Core/Roleplay/Services/StoryMemoryDigestParser.cs`
- `Core/Roleplay/Models/StoryState.cs`
- `Core/Roleplay/Models/StoryTemplate.cs`
- `Core/Conversation/Services/PromptAssembler.cs`
- `Core/Conversation/Services/ConversationService.cs`

## Isolation Rules

- Story facts are fictional and must not become project memory.
- Advanced/project memory must not be injected into Story prompts.
- Story mode must not execute local commands, tools, Git, builds, or agents.
- User-visible Story text must not mention prompts, modes, app internals,
  settings, logs, or local files.
- Parser labels and internal control tags must not appear in visible output.

## Rendering

Story output may contain separate action/narration and spoken-dialogue lines.
Mixed inline styling is a presentation concern and should not change the input
grammar or Story state format.

Future visual-novel presentation work may improve layout, but it must preserve
the lightweight character-centered concept and current state compatibility.
