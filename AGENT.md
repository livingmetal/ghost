# LivingMetalGhost Agent Guide

This document is the handoff note for future AI agents, Codex sessions, Claude Code sessions, or human maintainers working on this repository.

The project is a Windows desktop character assistant called **LivingMetalGhost**. It is intended to feel like a modern Ukagaka/Nanika-style companion: a lightweight character on the desktop in daily mode, a small visual-novel/ORPG partner in roleplay mode, and a practical workbench/agent manager in advanced mode.

Use this file as the first thing to read before making changes.

---

## Repository and environment

- Repository: `livingmetal/ghost`
- Typical local path: `C:\workspace\livingmetal\temporory\ghost`
- Main app: `src/LivingMetalGhost`
- Target runtime currently tested by the user: `win-arm64`
- User primary device: Surface Pro 11, Windows on ARM
- The user often builds with:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish.ps1 -RuntimeIdentifier win-arm64
```

The repository is actively edited both through GitHub and local pulls. Avoid broad rewrites unless necessary. Prefer small, compiling changes.

---

## Current product direction

The app should not become a Git client or a build tool first. Git and command execution were used as a testbed for the approval/workbench system. The broader goal is this:

1. **Daily mode**
   - Casual character conversation.
   - Reality-based conversation.
   - No local command execution.
   - No file/Git/build operations.
   - If the user asks for real work, guide them to advanced mode.

2. **Roleplay mode**
   - Fictional visual-novel/ORPG style.
   - User becomes the player.
   - Character stays inside the fictional scene.
   - No real app, Git, command, settings, log, prompt, or local file references unless the user explicitly exits fiction.
   - Must feel like a small scene engine, not just a different tone.

3. **Advanced mode**
   - Practical, factual workbench mode.
   - Character voice remains, but accuracy, risk checks, and assumptions come first.
   - Local commands and external agents must be gated by approval cards.
   - Future Codex/Claude Code integration belongs here.

---

## Core mode model

The app currently uses `ConversationMode` with these conceptual states:

- `Daily`
- `Story`
- `Advanced`

The user prefers the wording **롤플레잉 모드** for `Story` mode. Code still uses `Story` in places. When changing UI text, prefer “롤플레잉”. Avoid reintroducing “스토리모드” in user-facing text unless compatibility requires it.

Important existing flags in `MainViewModel`:

- `IsAdvancedMode`
- `IsStoryMode`
- `CurrentMode`

`CurrentMode` is determined as:

```csharp
public ConversationMode CurrentMode => IsAdvancedMode
    ? ConversationMode.Advanced
    : IsStoryMode ? ConversationMode.Story : ConversationMode.Daily;
```

Advanced mode should override roleplay mode.

---

## Roleplay mode design

Roleplay mode should be lightweight, immersive, and syntax-driven.

### Player input syntax

The user requested this simple grammar:

```text
plain text    -> spoken dialogue
*text*        -> visible action / narration / situation description
(text)        -> inner thought
```

Example:

```text
오르키아, 저 콘솔 보여?

*천천히 콘솔 앞으로 다가간다.*

(이건 켜져 있으면 안 되는 장비인데...)
```

The LLM should receive this as structured roleplay input:

- Spoken dialogue: heard by characters.
- Visible action/narration: seen by characters.
- Inner thought: not directly knowable by characters.

Characters may infer thoughts only from visible hesitation, expression, or behavior. They must not read the player’s mind.

### Roleplay opening scene

Roleplay mode should start with an actual situation, not a blank chat. The current default scene is:

```text
밤의 데이터센터
늦은 밤의 폐쇄망 데이터센터.
팬 소리는 낮게 깔리고,
사용되지 않아야 할 콘솔 하나가 푸른빛으로 깨어나 있다.
```

First objective:

```text
콘솔이 왜 깨어났는지 확인하고, 오르키아와 함께 안전하게 접근한다.
```

The opening is produced through `StoryStateStore.BuildOpeningText()` and shown when roleplay mode is enabled.

### Roleplay response rules

The model must:

- Keep the fiction intact.
- Not say “roleplay mode”, “prompt”, “system”, “app”, “Git”, “settings”, or “logs” inside fiction.
- Not decide the user’s next action.
- Progress only a small beat at a time.
- Use scene narration when useful.
- End with a hook or 2 to 3 choices if the scene would otherwise stall.
- Avoid printing labels like `[Scene]`, `[Character]`, `[Choices]` in the final visible response.

Good behavior:

```text
*콘솔의 푸른빛이 네 손등 위로 얇게 번진다.*

오르키아는 화면에 떠오른 깨진 문자를 보며 목소리를 낮춘다.

"이건 부팅 로그가 아니야. 누군가 안쪽에서 문을 두드리는 신호에 가까워."

1. 콘솔 로그를 살핀다
2. 오르키아에게 먼저 분석을 맡긴다
3. 한 걸음 물러나 주변 소리를 듣는다
```

Bad behavior:

```text
현재 스토리 모드입니다. 사용자의 입력은 행동으로 해석됩니다.
```

or

```text
당신은 문을 열고 안으로 들어갔다.
```

Do not take control of the player.

---

## Recent roleplay implementation files

These files are central to the current roleplay feature:

- `src/LivingMetalGhost/Core/Services/RoleplayInputFormatter.cs`
  - Parses plain text, `*action*`, and `(thought)` into structured prompt text.

- `src/LivingMetalGhost/Core/Services/StoryStateStore.cs`
  - Stores story state at `%APPDATA%\LivingMetalGhost\Stories\default`.
  - Default scene and `BuildOpeningText()` live here.

- `src/LivingMetalGhost/Core/Services/PromptAssembler.cs`
  - Builds mode-specific system prompts.
  - `BuildRoleplayingModeDirective()` contains the roleplay syntax and fiction rules.

- `src/LivingMetalGhost/Core/Services/ConversationService.cs`
  - Uses `RoleplayInputFormatter.FormatForPrompt(text)` when `ConversationMode.Story` is active.
  - Updates story memory through `RoleplayStateUpdater.UpdateAfterTurn(...)`.

- `src/LivingMetalGhost/UI/ViewModels/MainViewModel.cs`
  - `SetStoryMode(...)` shows the opening text when roleplay starts.
  - `ShowRoleplayOpening(...)` writes the opening scene into the chat list.

---

## Daily mode rules

Daily mode is not roleplay. It should be a character-flavored daily assistant mode.

Daily mode should:

- Keep replies compact by default.
- Answer real-world questions normally.
- Use character voice, but not heavy narration.
- Not execute local commands.
- Not invoke Git/build/file operations.
- Not leak roleplay state into practical answers.

If the user asks for fictional scene continuation while Daily mode is active, either answer casually as fiction or suggest using roleplay mode if the UI flow supports it. Do not silently pull roleplay state into daily answers.

---

## Advanced mode and command approval

Advanced mode is the only place where local command-like behavior belongs.

### Existing approval direction

Current work explored these behaviors:

- `git status` / read-only checks can be automatic.
- `git fetch origin` is a `NetworkRead` style command and can be approved.
- `git pull` changes workspace state and must remain approval-gated.
- General/roleplay mode must not execute command actions.

Agent Dock has approval cards and now may show:

```text
[승인] [항상 승인] [거절]
```

The UI may be cramped. UI resizing can wait.

### Always approve policy

Always-approve must remain conservative.

Allowed to remember:

- Network read-only operations such as `git fetch origin`.

Do not remember automatically:

- `git pull`
- merge/rebase/checkout
- reset/clean/delete
- file writes
- `dotnet build/test` for now
- PowerShell/script execution
- Codex/Claude Code execution

The rule of thumb:

```text
Always approve may skip repeated low-risk discovery.
It must not skip operations that change workspace state.
```

---

## Git work status

Git integration is an MVP testbed. Do not over-invest in Git-specific workflow unless the user explicitly asks.

Good enough for now:

- Read local status.
- Ask approval for fetch.
- Allow remembering safe network checks.
- Ask approval for pull.
- Block Git work outside advanced mode.

Avoid spending time on these unless requested:

- rebase/merge strategy automation
- branch management UI
- push/commit/PR automation
- reset/clean helpers

The next practical command-work step, when resumed, should likely be `dotnet build` / `dotnet test` behind approval cards. The user indicated this can wait and may be better handled through Codex later.

---

## Current build command

Use the same command the user runs:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish.ps1 -RuntimeIdentifier win-arm64
```

If a change touches XAML or generated partial classes, expect WPF generated temp project names such as:

```text
LivingMetalGhost_xxxxxxxx_wpftmp
```

These are normal during WPF builds.

---

## Important compatibility pitfalls

### `ConversationLogEntry`

Do not call a multi-argument constructor. It currently uses settable properties. Use object initializer style:

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

### `CharacterProfile`

Do not assume these properties exist on `CharacterProfile`:

```text
DefaultSizePresetId
DefaultFramingPresetId
DefaultScale
```

Use safe fallbacks currently used in `MainViewModel.RefreshSelectedCharacter()`:

```csharp
SelectedCharacterSizePresetId = string.IsNullOrWhiteSpace(profile?.CharacterSizePresetId)
    ? "normal"
    : profile.CharacterSizePresetId;

SelectedCharacterFramingPresetId = string.IsNullOrWhiteSpace(profile?.CharacterFramingPresetId)
    ? "full-body"
    : profile.CharacterFramingPresetId;

CharacterScale = profile?.CharacterScale is > 0 ? profile.CharacterScale : 1.0;
```

### `AppCommandSkill`

The user wanted chat-triggered “settings/log window” commands removed. Do not re-enable chat text such as `설정 열어` or `로그 보여줘` as app-window commands unless the user asks.

Settings and logs should be opened through explicit UI routes such as buttons, tray menu, or future command palette.

---

## Character and UI direction

The app uses sprites now and should remain sprite-friendly. VRM/3D can be explored later, but current priority is modular character/sprite architecture and mode experience.

Important direction:

- Characters should be modularly addable.
- Sprite swapping can remain coarse for now.
- Later, sprites may become modular parts.
- In advanced mode, Codex/Claude Code may appear as pet/summoned helper agents rather than replacing the main character.
- Agent Dock should show current agent tasks and approval status.
- Hovering agents should eventually show what is running.
- The main character should ask for approval on behalf of background agents.

UI state:

- Daily mode can remain small character + speech bubble.
- Roleplay mode should gradually move toward a visual-novel style panel or richer chat rendering.
- Advanced mode can be a workbench/browser-like panel.
- Agent Dock approval buttons are currently cramped and need later UI polish.

---

## Coding style and response behavior

When modifying code:

- Prefer small commits.
- Avoid full-file rewrites unless necessary.
- Before replacing a file, inspect current model definitions.
- Keep Korean UI text natural and short.
- Do not add verbose assistant-ish messages.
- Do not put internal parser labels into user-visible roleplay output.
- Do not create command execution paths outside advanced mode.
- Treat local command execution as dangerous by default.

When responding to the user:

- The user prefers direct, assumption-checking feedback.
- Explain risks and tradeoffs clearly.
- The user is comfortable with architecture-level discussion and code paths.
- Keep answers actionable.

---

## Suggested next work

### Near-term

1. Rebuild and verify the recent roleplay patch.
2. Test roleplay mode with:

```text
오르키아, 저 콘솔 보여?

*천천히 콘솔 앞으로 다가간다.*

(이거, 켜져 있으면 안 되는 장비인데...)
```

3. Check that:
   - plain text is treated as dialogue,
   - `*...*` is handled as action/narration,
   - `(...)` is handled as inner thought,
   - the character does not directly read the inner thought.

### Next UX work

- Improve roleplay rendering so `*action*` can be shown as italic or a separate narration style.
- Current chat bubble uses text-based rendering, so mixed inline styling may require replacing `TextBox` with `TextBlock` + `Inlines`, `FlowDocument`, or a custom message renderer.

### Later advanced-work work

- Leave Git MVP alone unless the user asks.
- Add `dotnet build` and `dotnet test` through approval cards.
- Add build result summarization.
- Add file read / patch preview before any write operation.
- Then consider Codex/Claude Code orchestration.

---

## Known recent commits to understand context

Recent changes included:

- Roleplay input parser creation.
- Default roleplay opening scene.
- Stronger roleplay prompt rules.
- Roleplay syntax formatting before LLM calls.
- MainViewModel compatibility fix for current `ConversationLogEntry` and `CharacterProfile` models.

If something fails after pulling, first check `MainViewModel.cs`, `ConversationService.cs`, `PromptAssembler.cs`, and `StoryStateStore.cs`.

---

## Do not do without explicit user approval

- Add unrestricted shell execution.
- Execute PowerShell scripts automatically.
- Auto-apply patches from agents.
- Auto-run Codex/Claude Code against the workspace.
- Auto-approve workspace-changing commands.
- Re-enable chat text commands that open settings/log windows.
- Merge roleplay memory into advanced/project memory.

The boundary between fiction, daily chat, and practical work is central to the app. Keep it sharp.
