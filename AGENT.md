# LivingMetalGhost Agent Guide

This is the single handoff guide for future AI agents, Codex sessions, Claude Code sessions, and human maintainers working on this repository.

Read this file first before changing the project.

---

## Final product goal

**LivingMetalGhost** is a Windows desktop character assistant inspired by modern Ukagaka/Nanika companions.

The final goal is not merely a chat window with a mascot. The app should become a small desktop companion platform with three sharply separated experiences:

1. **Daily companion**
   - A lightweight character that lives on the desktop.
   - Handles casual questions, short utility conversations, translation, summaries, and small daily assistant tasks.
   - Keeps character flavor without turning every answer into fiction.
   - Does not run local commands or change files.

2. **Roleplay / visual-novel mode**
   - A fictional ORPG / visual-novel style scene mode.
   - The user controls their own player character.
   - The assistant character reacts inside the fictional scene.
   - Scene state, turn memory, tension, and affinity are isolated from practical work memory.
   - The mode should eventually feel like a lightweight scene engine, not just a different speaking tone.

3. **Advanced workbench mode**
   - A practical work mode for architecture review, code review, file/workspace tasks, build/test orchestration, and external coding agents.
   - Character voice remains, but accuracy, risk checks, assumptions, and approval boundaries come first.
   - Codex / Claude Code style agents may appear as controlled sub-agents in an Agent Dock.
   - All workspace-changing actions must be gated by explicit approval.

A good long-term shape is:

```text
Desktop character
  -> Daily chat for ordinary interaction
  -> Roleplay console for fictional scenes
  -> Advanced workbench for real project work
       -> Agent Dock
       -> Approval cards
       -> Build/test summaries
       -> Safe file patch preview
```

The boundary between fiction, daily chat, and practical work is central. Do not blur it.

---

## Repository shape

- Repository: `livingmetal/ghost`
- Main app: `src/LivingMetalGhost`
- UI stack: WPF / .NET
- Character assets: `src/LivingMetalGhost/Assets/Characters`
- Config template: `config.template.json`
- User config at runtime: `%APPDATA%/LivingMetalGhost/config.json`

Do not assume a fixed local path, fixed developer machine, fixed CPU architecture, or fixed runtime identifier. This project may be cloned and built from different machines and directories.

Prefer repository-relative commands in examples:

```powershell
dotnet build .\src\LivingMetalGhost\LivingMetalGhost.csproj
```

Publishing may use `publish.ps1`, but do not hard-code one runtime as the only valid target.

---

## Core mode model

The app uses `ConversationMode`:

- `Daily`
- `Story`
- `Advanced`

The UI should call `Story` mode **롤플레잉 모드**. Avoid user-facing text such as “스토리 모드” unless compatibility requires it.

Current conceptual priority:

```csharp
public ConversationMode CurrentMode => IsAdvancedMode
    ? ConversationMode.Advanced
    : IsStoryMode ? ConversationMode.Story : ConversationMode.Daily;
```

Advanced mode overrides roleplay mode. Daily mode should be the startup default. Roleplay mode is something the user turns on intentionally.

---

## Daily mode rules

Daily mode is reality-based character chat.

Daily mode should:

- Keep replies compact by default.
- Answer real-world questions normally.
- Use character voice without heavy narration.
- Avoid local commands, file changes, Git actions, and build/test execution.
- Avoid leaking roleplay state into practical answers.

If the user wants fictional scene continuation while Daily mode is active, either answer casually as fiction or guide them toward roleplay mode. Do not silently pull persistent roleplay state into daily answers.

---

## Roleplay mode design

Roleplay mode is fictional, scene-driven, and syntax-driven.

### Player input syntax

The current grammar is:

```text
plain text       -> spoken dialogue
**text**         -> visible action / narration / background description
*text*           -> ordinary italic emphasis
(text)           -> inner thought
```

Example:

```text
오르키아, 저 콘솔 보여?

**천천히 콘솔 앞으로 다가간다.**

(이거, 켜져 있으면 안 되는 장비인데...)
```

The LLM should receive this as structured roleplay input:

- Spoken dialogue: heard by characters.
- Visible action/narration: seen by characters.
- Inner thought: not directly knowable by characters.
- Italic emphasis: style only, not action syntax.

Characters may infer thoughts only from visible hesitation, expression, or behavior. They must not read the player’s mind.

### Roleplay story templates

Starting scenarios should not be hard-coded in C#.

Use character manifest defaults and user settings instead:

- Manifest field: `default_story_template`
- User override: `CharacterPromptSettings.StoryTemplate`
- State storage: `StoryStateStore`

The template format is simple line-based metadata:

```text
title: 밤의 데이터센터
player_role: 아키텍쳐
scene: 늦은 밤의 폐쇄망 데이터센터. 팬 소리는 낮게 깔리고, 사용되지 않아야 할 콘솔 하나가 푸른빛으로 깨어나 있다.
summary: 첫 목표: 콘솔이 왜 깨어났는지 확인하고, 캐릭터와 함께 안전하게 접근한다.
mood: quiet_tension
tension: 1
affinity: 50
```

Settings UI should let the user edit this template per character.

### Roleplay state

Roleplay state belongs only to roleplay storage. Do not merge it into practical project memory.

Key state fields:

- `Enabled`
- `Title`
- `Scene`
- `Summary`
- `PlayerRole`
- `Mood`
- `Tension`
- `Affinity`
- `UpdatedAt`

Affinity is a relationship-tone value, not a safety override. It may make a character warmer or more guarded, but it must not override consent, safety boundaries, or the character’s established personality.

### Roleplay response rules

The model must:

- Keep the fiction intact.
- Not say “roleplay mode”, “prompt”, “system”, “app”, “Git”, “settings”, or “logs” inside fiction.
- Not decide the user’s next action, emotion, or dialogue.
- Progress one small beat at a time.
- Use scene narration when useful.
- End with a hook or 2 to 3 choices if the scene would otherwise stall.
- Avoid labels like `[Scene]`, `[Character]`, `[Choices]` in the visible response.

Good behavior:

```text
**콘솔의 푸른빛이 네 손등 위로 얇게 번진다.**

오르키아는 화면에 떠오른 깨진 문자를 보며 목소리를 낮춘다.

"이건 부팅 로그가 아니야. 누군가 안쪽에서 문을 두드리는 신호에 가까워."

1. 콘솔 로그를 살핀다
2. 오르키아에게 먼저 분석을 맡긴다
3. 한 걸음 물러나 주변 소리를 듣는다
```

Bad behavior:

```text
현재 롤플레잉 모드입니다. 사용자의 입력은 행동으로 해석됩니다.
```

or

```text
당신은 문을 열고 안으로 들어갔다.
```

Do not take control of the player.

---

## Advanced mode and command approval

Advanced mode is the only place where local command-like behavior belongs.

Command and agent rules:

- Read-only checks may be low risk.
- Network reads may still need approval depending on scope.
- Workspace-changing actions require explicit approval.
- File writes require patch preview and approval.
- Build/test execution should be approval-gated.
- Codex/Claude Code execution should be approval-gated.

Always-approve must remain conservative.

Allowed to remember only for low-risk repeat discovery:

- Read-only checks.
- Safe network reads explicitly approved as repeatable.

Never silently auto-approve:

- `git pull`
- merge/rebase/checkout
- reset/clean/delete
- file writes
- build/test execution
- PowerShell/script execution
- Codex/Claude Code execution
- any operation that changes workspace state

---

## Important implementation files

Roleplay:

- `src/LivingMetalGhost/Core/Services/RoleplayInputFormatter.cs`
  - Parses plain dialogue, `**action**`, `*italic*`, and `(thought)` into structured prompt text.
- `src/LivingMetalGhost/Core/Services/StoryStateStore.cs`
  - Stores roleplay state and builds opening text.
- `src/LivingMetalGhost/Core/Services/RoleplayStateUpdater.cs`
  - Updates scene summary, tension, and affinity with lightweight heuristics.
- `src/LivingMetalGhost/Core/Services/PromptAssembler.cs`
  - Builds mode-specific system prompts.
- `src/LivingMetalGhost/Core/Services/ConversationService.cs`
  - Applies roleplay input formatting and updates story memory.
- `src/LivingMetalGhost/UI/ViewModels/MainViewModel.cs`
  - Mode switching and opening-scene display.
- `src/LivingMetalGhost/UI/ViewModels/MainViewModel.Roleplay.cs`
  - Roleplay state summary/reset helper.

Settings and config:

- `src/LivingMetalGhost/Core/Config/AppConfig.cs`
- `src/LivingMetalGhost/UI/ViewModels/SettingsViewModel.cs`
- `src/LivingMetalGhost/UI/Views/SettingsWindow.xaml`
- Character manifests under `src/LivingMetalGhost/Assets/Characters/*/manifest.json`

Advanced/agent work:

- `src/LivingMetalGhost/Agents/*`
- `src/LivingMetalGhost/Skills/CodingAgentSkill.cs`
- `src/LivingMetalGhost/Core/Services/AdvancedSessionLogService.cs`
- `src/LivingMetalGhost/Core/Services/WorkspaceStore.cs`

---

## Build and verification

Use repository-relative build commands. Do not assume a local drive or clone path.

Recommended basic check:

```powershell
dotnet build .\src\LivingMetalGhost\LivingMetalGhost.csproj
```

If publishing, choose the runtime appropriate to the current build machine and target.

If a change touches XAML or generated partial classes, expect WPF generated temp project names such as:

```text
LivingMetalGhost_xxxxxxxx_wpftmp
```

These are normal during WPF builds.

---

## Compatibility pitfalls

### ConversationLogEntry

Do not call a multi-argument constructor. Use object initializer style:

```csharp
await _conversationLogService.AppendAsync(new ConversationLogEntry
{
    Timestamp = DateTimeOffset.Now,
    UserText = userText,
    AssistantText = assistantText,
    Provider = llm.Provider,
    Model = llm.Model,
    IsProactive = isProactive,
    CharacterId = SelectedCharacterId,
    CharacterName = CharacterDisplayName,
    ProviderLabel = ActiveProviderLabel,
    Mood = mood
}, CancellationToken.None);
```

### CharacterProfile

Do not assume size/framing fields live directly on `CharacterProfile`.

Use:

```csharp
character.Presentation.DefaultSizePresetId
character.Presentation.DefaultFramingPresetId
```

or validate configured profile values against `character.Presentation.SizePresets` and `character.Presentation.FramingPresets`.

### AppCommandSkill

Do not re-enable chat text commands that open settings or log windows unless requested. Settings and logs should be opened through explicit UI routes such as buttons, tray menu, or a future command palette.

---

## Character and UI direction

The app is sprite-friendly now and should remain so.

Important direction:

- Characters should be modularly addable.
- Character manifests should carry defaults such as appearance, background, personality, presentation, sprites, and story template.
- Sprite swapping can remain coarse for now.
- Later, sprites may become modular parts.
- Advanced mode can show Codex/Claude Code as pet/summoned helper agents rather than replacing the main character.
- Agent Dock should show current agent tasks and approval status.
- The main character should ask for approval on behalf of background agents.

UI direction:

- Daily mode: small desktop character + speech bubble / compact chat.
- Roleplay mode: richer visual-novel style panel or styled chat rendering.
- Advanced mode: workbench/browser-like panel with Agent Dock.

---

## Coding style

When modifying code:

- Prefer small commits.
- Avoid full-file rewrites unless necessary.
- Before replacing a file, inspect current model definitions.
- Keep Korean UI text natural and short.
- Do not add verbose assistant-like messages.
- Do not put parser labels into user-visible roleplay output.
- Do not create command execution paths outside advanced mode.
- Treat local command execution as dangerous by default.

When responding to the user:

- Be direct and assumption-checking.
- Explain risks and tradeoffs clearly.
- Keep answers actionable.

---

## Suggested next work

Near term:

1. Build and fix compile errors.
2. Verify the roleplay syntax:

```text
오르키아, 저 콘솔 보여?

**천천히 콘솔 앞으로 다가간다.**

(이거, 켜져 있으면 안 되는 장비인데...)
```

3. Check that:
   - plain text is treated as dialogue,
   - `**...**` is handled as action/narration,
   - `*...*` is rendered as ordinary italic emphasis,
   - `(...)` is handled as inner thought,
   - the character does not directly read inner thought.

Next UX work:

- Wire `CharacterPromptSettings.StoryTemplate` into SettingsViewModel and SettingsWindow.
- Use styled rendering for roleplay narration and italic text in chat.
- Consider a visual-novel style roleplay panel.

Later advanced-work work:

- Add `dotnet build` and `dotnet test` through approval cards.
- Add build result summarization.
- Add file read / patch preview before any write operation.
- Then consider Codex/Claude Code orchestration.

---

## Do not do without explicit user approval

- Add unrestricted shell execution.
- Execute PowerShell scripts automatically.
- Auto-apply patches from agents.
- Auto-run Codex/Claude Code against the workspace.
- Auto-approve workspace-changing commands.
- Re-enable chat text commands that open settings/log windows.
- Merge roleplay memory into advanced/project memory.
