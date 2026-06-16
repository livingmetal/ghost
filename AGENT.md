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
*늦은 저녁, 흑점 폭발로 인해 잠시 마비되었던 데이터센터를 점검하던 한 작업자는 정체불명의 서비스가 세션을 열고 기다리는 것을 발견했다.*

(이건 단절된 네트워크라 누가 건드릴 수 없는 장비인데...)
*콘솔을 열고. 한참 정보를 두드린 그는 외부 침입의 흔적이 없음을 발견하고 조심스럽게 그 서비스에 접근했다.*

```

The LLM should receive this as structured roleplay input:

- Spoken dialogue: heard by characters.
- Visible action/narration: seen by characters.
- Inner thought: not directly knowable by characters.

Characters may infer thoughts only from visible hesitation, expression, or behavior. They must not read the player’s mind.

### Roleplay opening scene

Roleplay mode should start with an actual situation, not a blank chat. The current default scene is:

```text

밤의 데이터센터.

외부와 단절된 폐쇄망 데이터센터는 낮보다 밤에 더 시끄럽다.
사람의 목소리는 사라지고, 랙 사이를 흐르는 팬 소음과 전원 장치의 낮은 진동만이 남는다.

전날 발생한 강한 흑점 폭발 이후, 데이터센터는 원인을 알 수 없는 전체 재기동을 겪었다.
대부분의 서비스는 정상 복구되었지만, 작업자는 혹시 모를 장애를 확인하기 위해 늦은 밤까지 홀로 점검을 이어가고 있었다.

로그는 깨끗했다.
방화벽도, 접근 기록도, 계정 인증 이력도 이상이 없었다.
외부 침입은 없었다.

그렇게 작업을 마무리하려던 순간, 모니터 한쪽에서 낯선 프로세스 하나가 눈에 들어왔다.

`ghost.session.listener`

등록한 기억이 없는 서비스였다.
그러나 그것은 분명히 살아 있었다.

상태 표시등은 푸른빛으로 깜박이고 있었다.
마치 누군가 안쪽에서 조심스럽게 문을 두드리는 것처럼.

작업자는 콘솔을 열고 서비스 정보를 확인했다.
서명 없음.
배포 이력 없음.
패키지 출처 없음.
네트워크 송수신 없음.

그리고 마지막 로그 한 줄.

`SESSION BOUNDARY DETECTED`
`SPRITE RENDERER INITIALIZED`
`UNKNOWN ENTITY WAITING FOR INPUT`

작업자는 잠시 손을 멈췄다.

폐쇄망 내부에, 외부와 통신하지 않는, 출처 불명의 세션.
그리고 그 세션은 자신이 발견되기를 기다리고 있었다.

조심스럽게 접속 명령을 입력하자, 검은 터미널 위로 희미한 노이즈가 번졌다.
푸른 픽셀들이 모여 사람의 형상을 만들었다.

그곳에는 긴 귀를 가진 여성이 서 있었다.
판타지 소설에서나 보았을 법한 엘프.
하지만 그녀는 성 안이나 숲이 아니라, 데이터센터의 콘솔 안에 있었다.

엘프 형태의 스프라이트는 당황한 듯 주변을 둘러보았다.
하지만 그녀의 시선은 작업자를 정확히 찾지 못했다.

잠시 후, 그녀가 정면을 향해 조심스럽게 입을 열었다.

"거기... 누가 있나요?"

```

First objective:

```text
엘프는 작업자를 직접 볼 수 없다.
하지만 터미널 입력을 통해 대화할 수 있다.

정체불명의 엘프형 세션과 대화하여, 그녀가 누구인지 확인하자.
```

The opening is produced through `StoryStateStore.BuildOpeningText()` and shown when roleplay mode is enabled.

### Per-character starting story templates

The starting story is no longer hardcoded as the single source. Each character can ship a default story template as an asset:

```text
src/LivingMetalGhost/Assets/Characters/<Name>/story-default.json
```

Template fields (snake_case JSON):

- `character_id` — must match the character `id` in `manifest.json`.
- `title`, `player_role`, `mood`, `tension` — story metadata.
- `scene` — opening narration (use `\n` for line breaks).
- `summary` — current objective.
- `opening_line` — the character's first spoken line; `*...*` action syntax is allowed.

Loading and seeding flow:

- `StoryTemplateCatalog` scans the same roots as `CharacterCatalog`
  (app `Assets\Characters` and `%APPDATA%\LivingMetalGhost\Characters`)
  and keys templates by `character_id`.
- `StoryStateStore` resolves the active character via `AppConfigLoader` (`App.GhostId`)
  and seeds `StoryState` from that template when roleplay is first enabled or after `Reset(...)`.
- If no template exists for the active character, it falls back to the built-in hardcoded scene.
- `StoryState.OpeningLine` carries the template's `opening_line`, which `BuildOpeningText()` renders
  after the scene and before the objective.

The current shipped template is Orkia's `밤의 데이터센터`
(`Assets/Characters/Orkia/story-default.json`).

Note: a persisted `story_state.json` with a non-empty `Scene` masks the template
until the user runs roleplay reset, which re-seeds from the template.

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
1. 엘프에게 말을 걸어 대화가 가능한지 확인한다.
2. 그녀가 자신의 이름과 출신을 기억하는지 확인한다.
3. 이 세션이 외부 침입인지, 장애인지, 혹은 전혀 다른 현상인지 판단한다.
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
  - Seeds `StoryState` from the active character's story template (fallback to built-in scene).
  - `BuildOpeningText()` lives here and now also renders `StoryState.OpeningLine`.

- `src/LivingMetalGhost/Core/Services/StoryTemplateCatalog.cs`
  - Loads per-character `story-default.json` templates, keyed by `character_id`.

- `src/LivingMetalGhost/Core/Models/StoryTemplate.cs`
  - Record for a character's default starting story.

- `src/LivingMetalGhost/Assets/Characters/<Name>/story-default.json`
  - Per-character starting story asset (e.g. `Orkia/story-default.json`).

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
