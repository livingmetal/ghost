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
%APPDATA%\LivingMetalGhost\story
```

Older `Stories\default` state and memory files are copied into the canonical
directory on first load when no canonical file exists. A migration marker
prevents cleared canonical state from being restored again on a later launch.

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
- `Core/Roleplay/Services/RoleplayWriterService.cs`
- `Core/Roleplay/Services/RoleplayCharacterService.cs`
- `Core/Roleplay/Services/RoleplayDirectorService.cs`
- `Core/Roleplay/Services/RoleplayMemoryDigestService.cs`
- `Core/Roleplay/Models/StoryState.cs`
- `Core/Roleplay/Models/StoryTemplate.cs`
- `Core/Conversation/Services/PromptAssembler.cs`
- `Core/Conversation/Services/ConversationService.cs`

## Four-API Turn Pipeline

Story mode gives each configured endpoint one responsibility:

1. Writer creates `story_plan.json` when no usable plan exists. The plan is
   persisted and supplied to later Character prompts as optional continuity
   guidance, never as a script that overrides player agency. Character ID,
   schema version, and a Writer-settings fingerprint invalidate stale plans.
2. Character produces the only user-visible roleplay response using the
   isolated Roleplay history, character manifest, current state, and plan. The
   UI displays this response before waiting for Director and Memory work.
3. Director observes the completed turn and returns a JSON state proposal.
   The application validates text lengths, metric names, ranges, and maximum
   per-turn deltas before persisting it. The deterministic updater is the
   fallback when Director is disabled or fails.
4. Memory consolidates the latest six completed turns into compact fictional
   facts. A persisted checkpoint prevents the same successful interval from
   being digested twice, while a failed interval is retried on the next turn.

Writer, Director, and Memory are best-effort helpers: their provider or parser
failures do not discard a successful Character response. Cancellation still
propagates so closing or stopping a turn remains immediate.

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
