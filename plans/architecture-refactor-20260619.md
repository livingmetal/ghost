# Architecture Refactor Plan - 2026-06-19

## Goal

Reduce repository and code coupling while preserving current behavior. Each
phase must leave the application buildable and independently reviewable.

This plan does not include automatic character generation, image-generation API
integration, new external APIs, or implementation of a new sprite-parts
composition engine.

## Baseline

Expected baseline tag:

```text
refactor-baseline-20260619
```

The tag was not present in the local tag list during the documentation refactor.
Before deleting legacy files or performing broad moves:

1. check whether the tag exists on the remote;
2. fetch tags if appropriate;
3. otherwise create an agreed local baseline tag from the intended commit;
4. record the baseline commit SHA in the change report.

Initial Release build diagnosis succeeded with zero warnings and zero errors.
The build regenerates `Assets/App/LivingMetalGhost.ico`, so generated icon
changes must be separated from intended refactoring.

## Current Problems

- `AGENT.md`, `AGENTS.md`, `README.md`, and `plans/*` previously mixed current
  rules, handoff notes, implementation details, and future roadmaps.
- `src/LivingMetalGhost/Ghost` contains 251 tracked legacy duplicate files and is
  excluded from the active project by explicit MSBuild rules.
- `MainViewModel` coordinates UI state, conversations, Story flow, sprite state,
  logging, approvals, agent jobs, patch proposals, and app commands.
- `ConversationService` owns common chat, Story chat, Advanced context, history,
  response cleanup, Story memory digestion, and Advanced logging.
- `CharacterHost` combines WPF hosting, asset loading, image caching, renderer
  selection, animation timers, and manifest-layer composition.
- Runtime character assets, source-art work files, references, generated
  intermediates, and future rigging designs share the same tree.
- Some tracked generated files, including Python bytecode under character work
  directories, have no runtime purpose.

## Phase 1 - Canonical Documentation

Scope:

- make `AGENTS.md` canonical;
- keep `AGENT.md` as a compatibility pointer;
- keep `README.md` focused on product and build entry points;
- document current architecture, Story behavior, and sprite boundaries under
  `docs/`;
- mark `plans/` as future-oriented.

Verification:

```powershell
git diff --check
dotnet build .\src\LivingMetalGhost\LivingMetalGhost.csproj -c Release
```

Status: completed. Canonical documentation, current architecture boundaries,
Story rules, sprite boundaries, and the phased refactor plan are now separated.

## Phase 2 - Characterization Tests

Add tests before moving production code.

Targets:

- mode precedence and Daily/Story/Advanced isolation;
- roleplay input parsing and Story-state persistence;
- character manifest loading and missing-asset fallback;
- command policy and workspace guard behavior;
- prompt composition boundaries where deterministic assertions are practical.

No production folder moves in this phase.

Verification:

```powershell
.\scripts\verify.ps1
```

Initial folder slice completed:

- moved existing Story/Roleplay models and services into
  `Core/Roleplay/{Models,Services}`;
- moved matching tests into `tests/.../Core/Roleplay`;
- retained existing namespaces and behavior for compatibility.

## Phase 3 - Mode Coordination Seam

Extract mode transition and session-coordination behavior from `MainViewModel`.

Candidate types:

```text
Application/Modes/ModeCoordinator.cs
Application/Modes/ModeState.cs
Application/Conversation/DailySessionController.cs
Application/Roleplay/StorySessionController.cs
```

Keep WPF commands and observable properties in the view model. Move state
transition rules and service orchestration behind injected services.

Risk:

- window visibility currently depends on `MainWindow` observing view-model
  properties;
- startup explicitly disables Story mode while preserving Story state.

## Phase 4 - Conversation and Roleplay Separation

Split mode-specific behavior without changing provider contracts.

Candidates:

- common provider invocation and history service;
- Daily prompt policy;
- Story prompt/context builder and memory updater;
- Advanced context and session logging;
- response mood/control-tag parser.

Keep `ConversationService` as a facade until call sites and tests are stable.

Risk:

- prompt text changes can alter visible behavior without compile failures;
- persisted Story state must remain compatible.

Initial folder slice completed:

- moved conversation orchestration, prompt assembly, logging, and common
  conversation/LLM contracts into `Core/Conversation`;
- moved LLM provider implementations into `Infrastructure/Llm`;
- retained production namespaces and runtime behavior for compatibility.

## Phase 5 - Character Presentation Boundary

Introduce interfaces around catalog and renderer selection while preserving the
current WPF output.

Candidates:

```text
Core/Characters/ICharacterCatalog.cs
Core/Characters/CharacterProfile.cs
Core/Characters/CharacterVisualState.cs
UI/CharacterPresentation/ICharacterRenderer.cs
UI/CharacterPresentation/SpriteRenderer.cs
UI/CharacterPresentation/ModularSpriteRenderer.cs
```

`CharacterHost` remains the WPF host. The first extraction should move image
loading/cache and strategy selection, not introduce a new rigging engine.

Risk:

- WPF timers and dependency-property refresh order;
- blink/speaking frame continuity;
- framing and scale compatibility.

Initial folder slice completed:

- moved character catalog, visual models, prompt metadata, and presentation
  policy into `Core/Characters`;
- moved `CharacterHost` into `UI/CharacterPresentation`;
- retained production namespaces and XAML class identity for compatibility;
- did not add a rigging engine or image-generation integration.

## Phase 6 - Workbench and Approval Boundary

Consolidate workspace, patch, command policy, approvals, and agent execution
under an Advanced-only application service.

Goals:

- remove service-location calls from `MainViewModel.Approvals.cs`;
- inject approval/workbench services;
- enforce mode checks before execution services;
- keep all workspace writes explicitly approval-gated.

Risk:

- loosening command policy during relocation;
- accidentally exposing Advanced operations to Daily or Story mode.

Initial folder slice completed:

- moved workspace read/diff/patch utilities into `Core/Workbench/Workspace`;
- moved command policy and external-agent adapters into
  `Core/Workbench/Agents`;
- moved Advanced stores and related models into
  `Core/Workbench/{Services,Models}`;
- moved matching tests into `tests/.../Core/Workbench`;
- retained production namespaces and behavior for compatibility.

## Phase 7 - Repository Hygiene

Perform only after baseline confirmation and inventory.

### Delete Candidates

- `src/LivingMetalGhost/Ghost` after unique-file comparison and reference audit;
- tracked `__pycache__/*.pyc`;
- obsolete duplicate READMEs after their unique content is merged;
- generated work intermediates proven reproducible and unused.

### Move Candidates

- character `_work` files and reference art out of runtime asset paths;
- Orkia rigging design documents to an authoring/design area;
- asset-processing scripts to a dedicated tools or authoring directory;
- stable architecture decisions from completed plans into `docs/`.

### Rename Candidates

- code-level `Story` naming may remain for compatibility; user-facing naming can
  use Roleplay consistently;
- `Core/Services` classes should move into responsibility-specific namespaces
  only after extraction;
- avoid renaming `LivingMetalGhost` assembly or application data paths during
  this refactor.

Do not combine deletion, asset-path changes, and renderer changes in one step.

System folder slice completed before repository cleanup:

- moved configuration and security helpers into `Core/System`;
- moved local facts, meal parsing, and reminders into `Core/System`;
- moved the Windows tray adapter into `UI/DesktopShell`;
- retained production namespaces and runtime behavior for compatibility.

## Phase 8 - Physical Directory Moves

Move files and namespaces only after logical seams and tests exist.

Target shape:

```text
src/LivingMetalGhost/
  Application/
    Modes/
    Conversation/
    Roleplay/
    Workbench/
  Core/
    Characters/
    Conversation/
    Roleplay/
    Workbench/
    System/
  Infrastructure/
    Configuration/
    Persistence/
    Security/
    Llm/
    Agents/
  UI/
    DesktopShell/
    CharacterPresentation/
    Views/
    ViewModels/
```

This is a target organization, not a mandate to create all directories at once.

Initial UI folder slice completed:

- moved the main desktop window under `UI/DesktopShell`;
- grouped Daily chat and speech-bubble windows under `UI/Daily`;
- moved the Story window under `UI/Roleplay`;
- grouped Advanced workbench, workspace settings, and project-memory windows
  under `UI/Workbench`;
- grouped settings and conversation-log windows under `UI/Settings`;
- placed common, Roleplay, Workbench, and Settings view-model files with their
  owning UI areas;
- retained XAML class identities and namespaces for compatibility.

Application and infrastructure folder slice completed:

- moved request routing and user-facing skills into `Application/Skills`;
- moved LLM provider adapters and local provider detectors into
  `Infrastructure/Llm`;
- retained production namespaces and DI registrations for compatibility.

## Verification Gate for Every Phase

1. Confirm `git status` before changes.
2. Keep the phase focused on one responsibility.
3. Run focused tests while editing.
4. Run `.\scripts\verify.ps1` before completion.
5. Check `git status` again and isolate generated icon changes.
6. Record changed files, commands, results, manual checks, and remaining risks.

For UI or sprite changes, perform a manual smoke test of:

- app startup;
- Daily chat opening;
- Story mode opening and input;
- Advanced workbench opening;
- character idle, blink, speaking, and mood transitions;
- tray hide/restore.

## Deferred Work

- automatic character creation;
- image-generation APIs;
- automatic sprite-part extraction;
- a new rigging/composition engine;
- VRM or 3D rendering;
- automatic external-agent startup;
- automatic patch application;
- broad multi-project or plugin decomposition.
