# Plan: Structured Narrative for Story Mode

## Goal

Develop story mode from a single Scene/Summary/Mood/Tension blob into a small
structured narrative engine, inspired by Crack's "즉흥 서사" (the player drives a
branching, stateful story). Keep it local, heuristic-light, and within the
existing mode boundaries (no project/advanced memory bleed).

Out of scope for now (user decisions): VN background panel, TTS/STT.

## Design decision: how structured state updates

The roleplay loop already parses a hidden `[mood: x]` tag from the model reply
(`ConversationService.ParseMoodTaggedResponse`) and updates state with token-free
heuristics (`RoleplayStateUpdater`). Structured state reuses that pattern:

- The model may append one hidden machine line at the end of a roleplay reply:

  ```text
  [story: done=G1,G2; flag=elf_name_known; affinity=+1]
  ```

- `ConversationService` strips any `[story: ...]` block from the visible text
  (so it never reaches the chat bubble) and passes the parsed updates to
  `RoleplayStateUpdater`.
- Heuristics (Tension keywords etc.) stay as a fallback; the tag only adds
  explicit, reliable transitions.

This needs no extra LLM call and degrades gracefully: a weak model that omits the
tag simply keeps the current heuristic behavior.

## Stories

### G1 — Structured objectives (first vertical slice)

- `StoryObjective { Id, Text, Done }`; `StoryState.Objectives`.
- `StoryTemplate` + `story-default.json`: author initial objectives.
- `StoryTemplateCatalog`: parse objectives.
- `StoryStateStore.ApplyTemplate`: seed objectives from the template.
- `PromptAssembler`: render active objectives and instruct the model to pursue
  them and emit `[story: done=<id>]` when one is achieved.
- `ConversationService`: parse/strip `[story: ...]`, pass completed ids on.
- `RoleplayStateUpdater`: mark objectives done.
- `MainViewModel.GetRoleplayStateSummary`: show objective checklist text.

### G2 — Relationship meter + fact flags

- `StoryState.Affinity` (clamped int) and `StoryState.Flags` (discovered facts).
- Same `[story: affinity=±n; flag=...]` tag drives them.
- Render affinity as a descriptor and known flags in the prompt.

### G3 — UI surfacing

- Objective checklist / affinity indicator in the roleplay state view (and
  later a small in-scene HUD).

### G4 — Multi-scene arcs

- Advance scene/objectives as a chapter completes; template carries an ordered
  scene list.

## Completion gate

Each story builds clean (`scripts\verify.ps1`) and the tag parse/strip and state
transitions are verified with an isolated harness over the real code before the
slice is called done. Live model behavior (does the model emit the tag well) is
user-verified in the app.

## Risks

- Model tag reliability varies; heuristics remain the floor.
- A persisted `story_state.json` from before this change has no objectives until
  roleplay reset re-seeds from the template.
- Keep the hidden tag out of the visible bubble and out of roleplay memory text.
