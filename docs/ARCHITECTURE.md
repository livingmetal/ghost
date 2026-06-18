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
- `UI/DesktopShell/ViewModels/MainViewModel.ChatPlacement.cs`
- `UI/Roleplay/ViewModels/MainViewModel.Roleplay.cs`
- `UI/Workbench/ViewModels/MainViewModel.*.cs`
- `UI/Settings/ViewModels/*`

The desktop shell owns window placement, visibility, tray behavior, proactive
timers, and mode-specific companion windows.

Current debt: `MainWindow` and `MainViewModel` both coordinate mode behavior.
`MainViewModel` also owns conversation display, character mood, logging,
approval cards, patch proposals, and app commands.

The first physical UI refactor grouped windows by product experience while
retaining their existing XAML class identities and namespaces. View-model
partials were placed with their owning experience, while the common
`MainViewModel` remains the coordination point. Behavioral decomposition remains
a separate step.

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
- `Core/Conversation/Models/*`
- `Infrastructure/Llm/*`

`ConversationService` selects a provider, builds prompts, manages in-memory
history, parses response mood tags, updates mode-specific persistence, and logs
Advanced turns.

Current debt: Daily, Story, and Advanced workflows share one large service.
Repository context, Story memory digestion, response cleanup, and history
management are coupled to provider invocation.

### Roleplay and Visual-Novel Mode

Primary files:

- `Core/Roleplay/Services/RoleplayInputFormatter.cs`
- `Core/Roleplay/Services/RoleplayStateUpdater.cs`
- `Core/Roleplay/Services/StoryStateStore.cs`
- `Core/Roleplay/Services/StoryTemplateCatalog.cs`
- `Core/Roleplay/Services/StoryMemoryDigestParser.cs`
- `Core/Roleplay/Models/StoryState.cs`
- `Core/Roleplay/Models/StoryTemplate.cs`
- `Core/Roleplay/Models/RoleplayMemoryEntry.cs`

This area owns player-input parsing, Story templates, scene state, fictional
memory, opening text, and post-turn updates.

Current debt: Story prompt rules still live inside the general
`PromptAssembler`, while Story display flow remains in `MainViewModel`.

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

Current debt: approval handling uses service-location calls from
`UI/Workbench/ViewModels/MainViewModel.Approvals.cs`, and command/workbench concerns cross UI,
`Skills`, and the Workbench area.

### Local Facts, Timers, Notifications, and Parsers

Primary files:

- `Core/System/Facts/*`
- `Core/System/Reminders/*`
- `UI/DesktopShell/TrayIconService.cs`
- `Application/Skills/SlashIntentSkill.cs`

These are local-first utilities and should remain independent from LLM provider
implementations where possible.

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

### Large Coordination Classes

`MainViewModel`, `MainWindow`, `ConversationService`, `PromptAssembler`, and
`CharacterHost` are the main concentration points. Split them by extracting
testable behavior before moving files or changing namespaces.

## Safe Refactoring Sequence

1. Consolidate canonical documentation.
2. Add characterization tests around mode boundaries and current stores.
3. Extract mode coordination from `MainViewModel`.
4. Extract Story conversation flow from general conversation orchestration.
5. Introduce a character visual-state/renderer boundary.
6. Consolidate Advanced approval and workbench services.
7. Inventory and remove the legacy duplicate tree.
8. Move namespaces and directories only after behavior is covered.

Every step must leave the WPF project buildable.
