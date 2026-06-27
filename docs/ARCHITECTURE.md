# Ghost Architecture

This document describes the current application structure and the boundaries
recommended for incremental refactoring. It documents the system as it exists;
future feature proposals belong under `plans/`.

## Product Modes

Ghost presents one character through three isolated experiences:

```text
Desktop Shell
  -> Daily conversation
  -> Story / roleplay session
  -> Advanced workbench
```

Daily and Story modes are non-executing experiences. Advanced mode is the only
mode allowed to reach workspace, command, or external-agent capabilities, and
those operations remain approval-gated.

## Current Runtime Structure

The application is a single WPF project:

```text
src/LivingMetalGhost/
  App.xaml.cs              composition root and startup
  Application/             request routing and user-facing skills
  UI/                      WPF views, controls, and view models
    DesktopShell/          main character window and tray integration
    Daily/                 daily chat and speech bubble
    Roleplay/              Story window
    Workbench/             Advanced workspace and project-memory windows
    Settings/              settings and conversation-log windows
  Core/                    models, services, persistence, workspace utilities
    Characters/            character manifests and presentation policy
    Conversation/          conversation orchestration and contracts
    Roleplay/              Story models and services
    System/                configuration, facts, reminders, and security
    Workbench/             workspace, policy, agent, and Advanced-mode state
  Infrastructure/Llm/      LLM provider adapters and local detectors
  Assets/Characters/       character manifests and visual assets
```

The test project is `tests/LivingMetalGhost.Tests`.

## Current Responsibility Map

### Desktop UI

Primary files:

- `UI/DesktopShell/MainWindow.*`
- `UI/Daily/ChatWindow.*`
- `UI/Daily/SpeechBubbleWindow.*`
- `UI/Roleplay/StoryWindow.*`
- `UI/Workbench/AdvancedWorkbenchWindow.*`
- `UI/Workbench/WorkspaceSettingsWindow.*`
- `UI/Workbench/ProjectMemory*`
- `UI/Settings/*`
- `UI/DesktopShell/ViewModels/MainViewModel.cs`
- `UI/DesktopShell/ViewModels/MainViewModel.DesktopServices.cs`
- `UI/DesktopShell/ViewModels/MainViewModel.ChatPlacement.cs`
- `UI/Daily/ViewModels/MainViewModel.CompanionConversation.cs`
- `UI/CharacterPresentation/ViewModels/MainViewModel.CharacterPresentation.cs`
- `UI/Roleplay/ViewModels/MainViewModel.Roleplay.cs`
- `UI/Workbench/ViewModels/MainViewModel.*.cs`
- `UI/Settings/ViewModels/*`

The desktop shell owns window visibility, tray behavior, the WPF timer adapter,
and mode-specific companion windows. Pure window placement calculations live in
`Application/Desktop/CompanionWindowPlacement.cs`. Proactive Daily/Story
schedule state lives in `Application/Desktop/ProactivePresenceScheduler.cs`.

`MainViewModel` is now split into presentation-specific partials. Application
services own mode rules, session orchestration, runtime setting interpretation,
conversation entry points, and log metadata.

Current debt: `MainWindow` still coordinates companion-window creation and
visibility. Workbench approval cards and patch proposals remain in UI partials.

The first physical UI refactor grouped windows by product experience while
retaining their existing XAML class identities and namespaces. View-model
partials and application services now hold the extracted behavior, while the
common `MainViewModel` remains the observable composition point.

### Character and Sprite Presentation

Primary files:

- `Core/Characters/Models/CharacterCatalog.cs`
- `Core/Characters/Models/CharacterImagePromptModels.cs`
- `Core/Characters/Services/CharacterImagePromptBuilder.cs`
- `Core/Characters/Presentation/SpriteDirector.cs`
- `UI/CharacterPresentation/CharacterHost.*`
- `Assets/Characters/*/manifest.json`

`CharacterCatalog` loads character manifests. `SpriteDirector` maps application
state to visual mood. `CharacterHost` performs WPF rendering, frame cycling,
blinking, motion, image caching, framing, and manifest-layer composition.

Current debt: asset loading, render strategy selection, animation scheduling,
and WPF drawing are concentrated in `CharacterHost`.

### Conversation and LLM Interaction

Primary files:

- `Core/Conversation/Services/ConversationService.cs`
- `Core/Conversation/Services/PromptAssembler.cs`
- `Core/Conversation/Services/ConversationLogService.cs`
- `Core/Conversation/Requests/ConversationRequestFactory.cs`
- `Core/Conversation/Responses/ConversationResponseProcessor.cs`
- `Core/Conversation/Models/*`
- `Infrastructure/Llm/*`

`ConversationService` is a compatibility facade that selects a provider,
executes requests, records completed history, and triggers mode-specific
post-turn work.

Conversation history is split into two channels:

- `Companion`: shared by Daily/idle conversation and Advanced workbench
  conversation;
- `Roleplay`: used only by Story mode and never injected into Companion
  requests.

Daily and Story responses may use LLM-selected semantic moods. Advanced
responses do not request or accept LLM mood selection; their visual behavior is
reserved for deterministic application rules.

`Core/Characters/Presentation/CharacterMoodResolver.cs` filters requested moods
against the selected character manifest before applying the mode expression
policy. Conversation orchestration no longer interprets sprite or modular-state
capabilities directly. Prompt assembly uses the same resolver when advertising
available moods, preventing prompt and response validation from drifting apart.

`Core/Conversation/History/ConversationHistoryStore.cs` owns bounded in-memory
history, channel resolution, snapshots, and role counts. `ConversationService`
uses this store instead of owning history collections directly.

`ExternalConversationTurnRecorder` normalizes tool and agent results before
adding them to Companion history. `ConversationService.RememberExternalTurn`
remains as a compatibility facade for existing skills.

`Core/Conversation/Responses/ConversationResponseParser.cs` owns leading mood
tag parsing and removal of legacy Roleplay metadata tags.

`Core/Conversation/Responses/CharacterSpeechSanitizer.cs` owns final dialogue
cleanup, including stock-phrase removal and whitespace normalization.

`ConversationResponseProcessor` composes mood-tag parsing, Roleplay legacy-tag
cleanup, speech sanitization, and manifest-aware mood resolution. All LLM
conversation entry points use the same response pipeline.

`ConversationRequestFactory` composes system prompts, hidden-trait directives,
mode-specific history, model options, and repository context into `LlmRequest`.
Provider selection and execution remain in `ConversationService`.

`Core/Conversation/Personality/HiddenTraitScheduler.cs` owns hidden-trait
activation state. It follows the same Companion/Roleplay channel split as
conversation history.

Current debt: Daily, Story, and Advanced provider execution still shares one
facade. Provider selection and mode-specific post-turn triggers remain there,
but request assembly, response cleanup, history storage, repository context,
Story memory digestion, and Advanced logging have explicit boundaries.

### Roleplay and Visual-Novel Mode

Primary files:

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
- `Core/Roleplay/Models/RoleplayMemoryEntry.cs`

This area owns player-input parsing, Story templates, scene state, fictional
memory, opening text, and post-turn updates. Roleplay runtime files now share
the canonical `%APPDATA%\LivingMetalGhost\story` directory; legacy
`Stories\default` state and memory are migrated on first load.

The Story turn pipeline is split by endpoint responsibility. `RoleplayWriterService`
creates and persists a reusable plan, `RoleplayCharacterService` produces the
visible response, and `RoleplayDirectorService` proposes a structured post-turn
state update. Director values pass through deterministic validation and
per-turn delta limits in `RoleplayStateUpdater`; failure falls back to the
existing heuristic update rather than failing the visible turn.

`RoleplayMemoryDigestService` owns the six-turn best-effort LLM memory digest
and replacement of compact Story facts. A persisted digest checkpoint prevents
duplicate consolidation at the same turn count. General conversation
orchestration only triggers it after a completed Roleplay turn.

`Application/Roleplay/RoleplaySessionController.cs` owns Roleplay activation,
reset, state snapshots, opening text, conversation turns, and idle turns.
`MainViewModel` no longer accesses `StoryStateStore` directly.

`Core/Roleplay/Prompts/RoleplayPromptPolicy.cs` owns fictional isolation,
player-agency rules, input syntax, visual-novel response style, scene state, and
compact memory injection. General `PromptAssembler` selects this policy without
containing Roleplay-specific prompt text.

Current debt: Story WPF presentation remains in the Roleplay view-model partial
and `StoryWindow`. Shared message pacing is handled by
`AssistantMessagePresenter`.

### Agent, Tool, and Command Execution

Primary files:

- `Core/Workbench/Agents/*`
- `Core/Workbench/Services/*`
- `Core/Workbench/Models/*`
- `Application/Skills/CodingAgentSkill.cs`
- `Application/Skills/GitCommandSkill.cs`
- `Core/Workbench/Workspace/*`

`CommandPolicyService` classifies commands. Workspace services provide read,
diff, patch, and context capabilities. Agent executors adapt external tools.

`Core/Workbench/Prompts/AdvancedPromptPolicy.cs` owns Advanced-only safety,
approval, reusable workspace-memory, and repository-snapshot prompt rules.
General `PromptAssembler` selects this policy without directly accessing
Workbench persistence.

`Core/Workbench/Services/AdvancedConversationSupport.cs` owns per-turn
repository context creation and Advanced session-log metadata. General
`ConversationService` delegates to this boundary instead of depending directly
on workspace and session stores.

Current debt: approval handling uses service-location calls from
`UI/Workbench/ViewModels/MainViewModel.Approvals.cs`, and command/workbench concerns cross UI,
`Skills`, and the Workbench area.

### Local Facts, Timers, Notifications, and Parsers

Primary files:

- `Core/System/Facts/*`
- `Core/System/Reminders/*`
- `UI/DesktopShell/TrayIconService.cs`
- `Application/Skills/SlashIntentSkill.cs`
- `Application/SlashAgents/*`

These are local-first utilities and should remain independent from LLM provider
implementations where possible.

`SlashIntentSkill` only detects a single leading slash. `SlashIntentPlanner`
uses the basic conversation model to select one allowlisted capability and
extract arguments. Independent handlers load Korea date/time, KAIST Munji menu
data, Open-Meteo regional weather, or reminders. The verified result is passed
through `SlashAgentResponseComposer` for character-voice narration. If planning
or narration fails, deterministic routing and raw verified facts remain
available. Double-slash input is never intercepted.

### Configuration, Persistence, and Infrastructure

Primary files:

- `Core/System/Config/*`
- `Core/System/Security/*`
- `Core/Conversation/Services/ConversationLogService.cs`
- `Core/Workbench/Services/ProjectMemoryStore.cs`
- `Core/Workbench/Services/AdvancedSessionLogService.cs`
- `Core/Workbench/Services/WorkspaceStore.cs`

These services own file formats, paths, secrets, and durable state under
`%APPDATA%\LivingMetalGhost`.

## Recommended Module Boundaries

Refactor toward these logical modules without immediately creating multiple
assemblies:

```text
UI/DesktopShell
  -> Application/ModeCoordination
      -> Core/Conversation
      -> Core/Roleplay
      -> Core/Characters
      -> Core/Workbench
      -> Core/System
          -> Providers and platform adapters
```

### UI/DesktopShell

Owns WPF windows, controls, binding, desktop placement, and user interaction.
It should depend on application-facing services rather than provider or storage
implementations.

### Application/ModeCoordination

Owns mode transitions and coordinates Daily, Story, and Advanced sessions. This
is the first useful seam to extract from `MainViewModel`.

`ConversationModeCoordinator` now owns the pure mode rules used by the desktop
shell:

- Daily and Advanced are the two Companion conversation presentations;
- Story state remains enabled while Advanced is active, but Roleplay UI is
  suspended until Advanced closes;
- Daily chat and speech-bubble overlays are suppressed during Story or
  Advanced presentation.

`RoleplaySessionController` now owns the Roleplay session lifecycle used by the
desktop shell. The UI consumes session operations without directly coordinating
Roleplay persistence and conversation services.

`UI/Presentation/AssistantMessagePresenter.cs` owns shared message chunking and
typing animation for Daily, Roleplay, and Advanced presentations. View models
retain only speaking-state and mood coordination around that presenter.

Daily/Advanced submitted input remains visible while the assistant is
responding. It is cleared after response presentation only when the user has not
edited the input in the meantime; failed sends remain available for retry.
Daily chat also renders the submitted text as an outgoing user bubble until the
assistant response presentation completes, so the sent message remains visible
even when focus or binding updates affect the editor.

Character selection, scale persistence, speaking-state coordination, and
post-speech mood timing are physically isolated in
`UI/CharacterPresentation/ViewModels/MainViewModel.CharacterPresentation.cs`.
This is a compatibility seam only; no renderer, rigging, or sprite asset behavior
is changed.

Desktop conversation commands are physically grouped by presentation:
`UI/Daily/ViewModels/MainViewModel.CompanionConversation.cs` owns shared
Daily/Advanced send and proactive flows, while
`UI/Roleplay/ViewModels/MainViewModel.Roleplay.cs` owns Story activation, send,
idle, reset, and state summary flows.

`Application/Desktop/DesktopRuntimeSettingsService.cs` owns proactive-chat
interval normalization and Advanced provider availability detection. The
DesktopShell applies the returned state without interpreting provider names or
configuration fallback rules.

`UI/DesktopShell/ViewModels/MainViewModel.DesktopServices.cs` groups provider
labels, runtime availability application, app-command dispatch, settings/log
windows, and conversation log adaptation. The base `MainViewModel.cs` now
contains composition, shared observable state, and shared collections.

`Application/Conversation/CompanionConversationController.cs` owns skill routing
and proactive Companion conversation entry points. `ConversationTurnLogWriter`
owns provider-aware log metadata and persistence for all conversation modes.

### Core/Conversation

Owns conversation requests, history, response normalization, and provider-facing
contracts. Mode-specific prompt fragments should be supplied through explicit
policies or builders.

The first physical conversation refactor moved conversation orchestration,
prompt assembly, logs, and common contracts under `Core/Conversation`.
Provider implementations later moved under `Infrastructure/Llm`. Public
namespaces remain unchanged during this compatibility step.

### Application

Owns request routing and user-facing skills that coordinate Core services and
infrastructure adapters. The first physical application refactor moved the
existing `Skills` folder under `Application/Skills` without changing namespaces.

### Core/Roleplay

Owns Story parsing, templates, state, memory, and Story-specific prompt context.
It must not depend on workbench or command execution.

The first physical folder refactor moved existing Story models and services under
`Core/Roleplay` without changing their public namespaces. Namespace migration is
deferred until callers can be updated in a separate buildable step.

### Core/Characters

Owns character definitions, manifest loading, visual-state contracts, and asset
resolution. WPF rendering remains in UI.

The first physical character refactor moved the catalog, prompt metadata, and
presentation policy under `Core/Characters`. The WPF `CharacterHost` moved under
`UI/CharacterPresentation`. Public namespaces and the XAML `x:Class` remain
unchanged during this compatibility step.

### Core/Workbench

Owns workspace context, patch proposals, diffs, command policy, approvals, and
agent execution contracts. It is available only to Advanced mode.

The first physical Workbench refactor moved existing workspace utilities,
external-agent adapters, Advanced stores, and related models under
`Core/Workbench`. Their public namespaces remain unchanged so this folder move
does not alter callers or runtime behavior.

### Core/System

Owns configuration, persistence, secrets, local facts, reminders, logs, and
notifications.

The first physical System refactor moved configuration, DPAPI/security helpers,
local facts, meal parsing, and reminders under `Core/System`. The Windows tray
adapter moved to `UI/DesktopShell` because it directly depends on WPF and
Windows Forms. Public namespaces remain unchanged during this compatibility
step.

## Dependency Rules

- Core domain services must not depend on WPF controls or windows.
- Roleplay must not depend on Workbench or Agents.
- Providers must implement contracts; they must not own character or mode
  policy.
- Character presentation must receive a visual state rather than reading raw LLM
  output.
- UI must not parse persistence files or execute commands directly.
- Mode checks must occur before reaching workbench execution, not only in the UI.

## Known Repository Debt

### Legacy Duplicate Tree Cleanup

The historical `src/LivingMetalGhost/Ghost` source snapshot was inventoried and
removed after the architecture checkpoint. Exact duplicate runtime files and
superseded code were deleted. Character source-art files with no active
equivalent were preserved under `authoring/Characters`.

The main project no longer needs special `Ghost/**` exclusion rules.

### Runtime and Authoring Asset Separation

`src/LivingMetalGhost/Assets/Characters` contains shipped manifests, runtime
metadata, and sprite files. Source art, references, intermediate files, offline
tools, and future rigging drafts are stored under `authoring/Characters`.

MSBuild exclusion guards prevent `_work`, `_original`, `References`, `Rigging`,
and Python cache directories from entering build or publish output if they are
accidentally added below runtime assets.

### Remaining Coordination Debt

`MainWindow` and `CharacterHost` are the remaining large coordination points.
`MainWindow` owns desktop window/timer behavior. `CharacterHost` still combines
asset loading, render strategy selection, animation scheduling, and WPF
drawing.

Workbench approval handling still resolves services from
`App.Services`. Replace that service location only in a dedicated,
approval-focused change with command-policy regression tests.

## Safe Refactoring Sequence

1. [Completed] Consolidate canonical documentation.
2. [Completed] Add characterization tests around mode boundaries and stores.
3. [Completed] Extract mode and session coordination from `MainViewModel`.
4. [Completed] Separate request, response, history, Roleplay, and Advanced
   conversation responsibilities behind explicit services.
5. [Deferred] Split `CharacterHost` into renderer strategies. Do not implement
   rigging or sprite composition as part of this refactor.
6. [Deferred] Remove service location from Advanced approval/workbench flows.
7. [Completed] Inventory and remove the legacy duplicate tree.
8. [Completed] Group source files by responsibility while retaining compatible
   namespaces and runtime behavior.

Every step must leave the WPF project buildable.
