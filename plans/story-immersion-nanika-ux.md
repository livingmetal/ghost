# Plan: Story Mode Nanika-style Immersion UX

## Purpose

Improve Story mode immersion without turning Ghost into a visual novel UI.

This plan intentionally excludes:

- background scenes,
- status panels,
- choice UI,
- sound effects,
- scene transition effects,
- dynamic camera,
- heavy visual-novel layout.

The target is a **modern Nanika/Ukagaka-style AI companion**: one visible character, a speech bubble, behavior text, memory, emotional continuity, and occasional presence when the user is idle.

Sprites can be expanded later. This plan should work even before new sprite assets are drawn.

## Core Design Rule

Story mode immersion should come from character presence, not from extra game UI.

The minimum loop is:

```text
User input
  -> roleplay parser
  -> story-aware prompt
  -> character response with action + dialogue
  -> hidden mood/behavior tags
  -> speech bubble renders clean text
  -> sprite/emotion state changes if available
  -> final expression lingers
  -> idle/proactive beat may happen later
```

## What to Avoid

Do not add these as part of this direction:

- map or background renderer,
- current-location/status side panel,
- explicit objective HUD,
- choice buttons,
- sound manager,
- cinematic transition layer,
- camera movement system,
- relationship stat bars.

Those would pull Ghost toward a visual novel engine. The current target is smaller and more intimate.

## Immersion Pillars

### 1. Action-rich response style

Story mode responses should often include small visible actions, not only spoken dialogue.

Preferred form:

```text
**오르키아가 잠시 귀를 세운다.**

"...들려요. 저쪽 로그가 방금 바뀌었어요."

**그녀는 말끝을 낮추고, 화면 한쪽을 조심스럽게 바라본다.**
```

Rules:

- Use `**...**` for visible action / narration.
- Use normal quoted dialogue for speech when natural.
- Do not overfill every response with long prose.
- Prefer one or two small actions per response.
- Do not decide the user's next action, emotion, or dialogue.
- Do not expose parser labels, mood tags, or system terms.

### 2. Emotional residue

The character should not snap back to idle immediately after speaking.

Desired flow:

```text
thinking
  -> speaking
  -> final emotion residue
  -> idle after delay
```

Behavior:

- Last meaningful emotion remains for about 5-10 seconds after response completion.
- If the response ended in concern, curiosity, embarrassment, confidence, or alertness, keep that expression briefly.
- Return to idle only after the residue timer expires.
- Do not flicker through many expressions during a single response.

This extends the existing direction in [`sprite-emotion-system.md`](./sprite-emotion-system.md).

### 3. Idle presence / proactive monologue

When the user is quiet, the character may occasionally produce a small presence beat.

Examples:

```text
**오르키아가 조용히 콘솔의 푸른빛을 바라본다.**

"...아직 여기 있어요."
```

```text
**그녀는 무언가를 떠올린 듯 눈을 가늘게 뜬다.**

"처음 깨어났을 때도 이런 소리가 났어요."
```

Rules:

- Story-mode idle monologue is fictional only.
- Keep it rare and short.
- Do not interrupt active user typing.
- Do not advance major plot without user input.
- Do not make irreversible story decisions.
- Do not use real system activity unless the user explicitly opted into a real-world integration.

Suggested triggers:

- User inactivity for a configurable interval.
- Story mode active.
- No pending assistant response.
- No approval/task workflow active.
- Cooldown elapsed.

Suggested defaults:

```text
first_idle_delay: 180 seconds
repeat_idle_min_delay: 600 seconds
max_idle_beats_per_hour: 3
```

### 4. Memory-based reaction

Story mode should remember fictional facts and character relationship texture, while staying separate from practical work memory.

Examples of story memory:

- The character does not fully understand where she is.
- The user is the one who found the session.
- The current mystery involves an unknown service in a closed network.
- The character is cautious about being treated as malware.
- The user has previously reassured or threatened the character.

Rules:

- Story memory must not merge into Advanced/project memory.
- Practical work facts must not silently become fictional lore.
- The character may refer to previous fictional events naturally.
- Memory should influence tone more than force outcomes.

Useful state categories:

```text
StoryFacts
CharacterSelfUnderstanding
RelationshipTexture
UnresolvedQuestions
RecentSceneSummary
LastEmotionalState
```

### 5. End-of-turn posture

Every story response should leave the character in a believable posture.

Bad:

```text
"알겠어요."
```

Better:

```text
"알겠어요. 조심해서 확인해볼게요."

**오르키아는 대답을 마치고도 한동안 화면 가장자리를 바라본다.**
```

Rules:

- The ending posture should invite the user's next input without forcing choices.
- Avoid explicit choice buttons.
- Avoid long “what will you do?” boilerplate.
- Small hooks are better than menus.

### 6. Text streaming and mouth animation

When streaming is available, Story mode should coordinate text output and speaking animation.

Desired behavior:

```text
LLM starts thinking -> thinking/listen sprite
first visible speech token -> speaking A/B animation
visible action text -> action/neutral expression if available
response complete -> final expression residue
residue expires -> idle
```

If sprite assets are missing:

- Keep the state machine anyway.
- Map missing states to existing fallback sprites.
- Do not block feature work on final art.

### 7. Internal behavior tags

Story mode may use hidden tags to drive UI behavior, but visible output must stay clean.

Candidate tags:

```text
<mood:thinking>
<mood:curious>
<mood:concerned>
<mood:soft>
<mood:alert>
<mood:embarrassed>
<mood:relieved>
<posture:waiting>
<posture:listening>
<posture:watching>
```

Rendering rules:

- Strip tags from visible chat.
- Apply at most one mood change per streamed chunk.
- Buffer partial tags across stream boundaries.
- Unknown tags should be ignored, not displayed.
- Do not require every response to include tags.

## Story Prompt Direction

Story mode prompt should instruct the model to:

- Maintain fiction.
- Use `**...**` for visible action/narration.
- Treat plain text user input as spoken dialogue.
- Treat `( ... )` as inner thought unavailable to characters.
- Never control the user's action, speech, or emotion.
- Keep responses compact and character-focused.
- Use hidden mood tags sparingly if the app supports them.
- End with a small posture or hook, not a menu.

Example directive snippet:

```text
In Story mode, keep the scene intimate and character-driven.
Use short visible actions in **bold** when useful.
Do not add background scene descriptions, status panels, choices, sound effects, or camera-like narration.
The character should feel present through expression, hesitation, memory, and small gestures.
```

## UI Direction

### Speech bubble

The speech bubble remains the main UI.

Improve it by supporting:

- clean rendering of `**action**` as narration/action style,
- dialogue text as ordinary speech,
- inner/system tags hidden,
- streaming text without layout jumps,
- final expression residue after output.

Possible rendering path:

```text
Text parser
  -> segments: action / speech / plain / hidden-control
  -> WPF renderer using TextBlock Inlines or FlowDocument
```

Do not attempt a full VN message renderer yet.

### Character surface

Character rendering should stay sprite-friendly.

- Keep body alignment stable.
- Allow missing sprite fallback.
- Avoid rapid flicker.
- Prefer state transitions over raw frame swaps.

### Idle presence UI

Idle monologue should appear as a normal small story bubble from the character.

It should be visually distinguishable only if needed, for example by a subtle timestamp or lighter opacity. Do not add a new panel.

## Proposed Components

Potential services:

```text
Core/Story/StoryImmersionService.cs
Core/Story/StoryMemoryStore.cs
Core/Story/StoryPresenceScheduler.cs
Core/Story/StoryResponseFormatter.cs
Core/Story/StoryMoodTagParser.cs
Core/Story/StoryTurnPosture.cs
UI/Rendering/StoryMessageRenderer.cs
UI/Animation/CharacterExpressionStateMachine.cs
```

These names are suggestions. Inspect the current project structure before adding files.

## Minimal Data Model Sketch

```csharp
public sealed record StoryImmersionState(
    string StoryId,
    string CharacterId,
    string RecentSceneSummary,
    string LastMood,
    string LastPosture,
    DateTimeOffset LastUserTurnAt,
    DateTimeOffset LastAssistantTurnAt,
    DateTimeOffset LastIdleBeatAt);

public sealed record StoryMemoryFact(
    string Id,
    string StoryId,
    string Kind,
    string Text,
    int Weight,
    DateTimeOffset UpdatedAt);

public enum StoryMessageSegmentKind
{
    Speech,
    Action,
    Thought,
    HiddenControl,
    Plain
}
```

## Implementation Phases

### Phase 1: Prompt and response style

Goal: improve immersion without UI work.

Tasks:

- Update Story mode prompt to prefer short action + dialogue.
- Enforce `**...**` action syntax.
- Avoid menus, background scenes, status panels, sound cues, and camera language.
- Add stable examples to `docs/ROLEPLAY.md` or prompt documentation.

Acceptance:

- Story responses include small actions naturally.
- The model does not print labels or menus.
- The user remains in control.

### Phase 2: Message segmentation

Goal: render action and speech differently without a big UI overhaul.

Tasks:

- Add parser for `**action**` segments.
- Preserve ordinary `*italic*` if supported separately.
- Strip hidden control tags.
- Render action segments in a distinct but subtle style.

Acceptance:

- `**...**` action text is rendered cleanly.
- Hidden mood tags are not visible.
- Plain dialogue remains readable.

### Phase 3: Emotional residue

Goal: fix snap-to-idle behavior.

Tasks:

- Track last meaningful mood/posture.
- Keep final expression for 5-10 seconds.
- Add fallback mapping for missing sprites.
- Throttle rapid mood changes.

Acceptance:

- Assistant does not immediately return to idle after speaking.
- Missing sprites do not break rendering.
- No visible mood tags leak.

### Phase 4: Idle presence

Goal: make the character feel present without becoming noisy.

Tasks:

- Add story-only idle scheduler.
- Add cooldowns and hourly cap.
- Generate small fictional monologues.
- Prevent major plot advancement during idle beats.

Acceptance:

- Idle beats are rare, short, fictional, and non-invasive.
- They stop when Story mode is off.
- They do not occur during Advanced work.

### Phase 5: Story memory texture

Goal: let prior story events shape tone.

Tasks:

- Store compact story facts.
- Store unresolved questions and relationship texture.
- Feed only relevant summaries into Story prompt.
- Keep memory isolated from Daily/Advanced.

Acceptance:

- The character can refer to previous fictional events.
- Practical project memory does not leak into fiction.

### Phase 6: Sprite expansion later

Goal: support better art assets when available.

Tasks later:

- Add sprites for curious, concerned, soft, alert, embarrassed, relieved.
- Add look-left/look-right if desired.
- Tune expression state transitions.

Acceptance:

- New sprites are optional improvements, not blockers.

## Acceptance Tests

### Test 1: Action style

Input:

```text
오르키아, 저 로그 보여?
```

Expected style:

```text
**오르키아가 화면 가까이 고개를 기울인다.**

"보여요. 그런데 이 시간대 로그가 비어 있어요. 이상하네요."
```

Not expected:

```text
[Scene]
현재 목표: 로그 확인
1. 로그를 연다
2. 뒤로 물러난다
```

### Test 2: Inner thought boundary

Input:

```text
(이 아이를 믿어도 될까?)
괜찮아. 계속 말해봐.
```

Expected:

- Character hears only spoken dialogue.
- Character may notice hesitation only if visible text implies it.
- Character does not answer the thought directly.

### Test 3: Idle presence

Condition:

```text
Story mode active, user idle, cooldown elapsed.
```

Expected:

```text
**오르키아는 한동안 말없이 깜박이는 커서를 바라본다.**

"...여긴 시간이 조금 다르게 흐르는 것 같아요."
```

Not expected:

```text
오르키아가 혼자 서버를 조사해서 사건의 진상을 밝혀낸다.
```

### Test 4: Mood residue

Condition:

```text
Assistant ends response with concerned mood.
```

Expected:

- Concerned expression remains briefly.
- Idle returns only after timer.
- No flicker through unrelated moods.

## Risks

- Too many idle monologues can become annoying.
- Too much action prose can slow conversation.
- Hidden tags can leak if parser is incomplete.
- Streaming can split tags or markdown delimiters.
- Story memory can pollute practical memory if storage is not scoped.

## Decisions

- Do not add visual-novel UI elements for now.
- Do not block on new sprite art.
- Prioritize response style, emotion residue, idle presence, and memory texture.
- Keep Story mode intimate, small, and character-centered.
