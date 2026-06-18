# Plan: Sprite Emotion System

## Goal

Make Ghost respond to LLM mood or emotion tags by changing character sprites without exposing those tags in the visible chat bubble.

This plan exists because sprite behavior is core product behavior for Ghost, not a cosmetic afterthought.

## Scope

Included:

- Parse mood/emotion tags from assistant output.
- Hide control tags from visible chat text.
- Allow sprite changes even when the tag appears in the middle or end of a chunk.
- Limit mood changes to reduce flicker.
- Keep the final expression briefly after the response ends.
- Preserve stable body alignment between sprite frames.

Excluded:

- Full 2D skeletal rigging.
- VRM or 3D character support.
- Automatic image generation.
- Provider-specific hardcoding unless unavoidable.

## Candidate Tags

Keep the exact final syntax flexible, but prefer simple internal tags such as:

```text
<mood:sleep>
<mood:listen>
<mood:thinking>
<mood:speaking>
<mood:working>
<mood:happy>
<mood:sad>
<mood:error>
```

Visible chat output must remove these tags before rendering.

## Desired Behavior

- Idle state may use sleep or relaxed sprite.
- User opens or focuses conversation: listen/awaken state.
- Assistant is thinking: thinking state.
- Assistant speaks: alternate speaking A/B or equivalent frames.
- Assistant works on command-like advanced task: working state.
- After speech ends: retain the last expression for about 5 seconds before returning to idle.
- If multiple tags appear in one response chunk, apply at most one mood change for that chunk.

## Implementation Notes

Search likely areas first:

- `src/LivingMetalGhost/UI/DesktopShell/ViewModels/MainViewModel.cs`
- `src/LivingMetalGhost/Core/Conversation/Services/ConversationService.cs`
- `src/LivingMetalGhost/Core/Conversation/Services/PromptAssembler.cs`
- Character or sprite model files under `src/LivingMetalGhost`
- Asset paths under `src/LivingMetalGhost/Assets/Characters`

Do not assume model properties exist. Inspect current definitions before changing constructors or profile fields.

## Tasks

- [ ] Locate the current sprite selection flow.
- [ ] Locate the chat message rendering flow.
- [ ] Add a mood tag parser or reuse an existing parser if present.
- [ ] Strip mood tags before adding assistant text to visible chat.
- [ ] Apply mood changes independently from visible text position.
- [ ] Add throttling so each chunk triggers at most one mood change.
- [ ] Keep the last expression for about 5 seconds after response completion.
- [ ] Ensure speaking animation does not instantly snap to idle.
- [ ] Verify build with `scripts/verify.ps1`.
- [ ] Manually test at least one response containing a tag at the start, middle, and end.

## Verification

Run:

```powershell
.\scripts\verify.ps1
```

Manual smoke test examples:

```text
<mood:thinking>잠깐 계산해볼게요.
```

```text
잠깐 계산해볼게요. <mood:thinking> 로그를 확인 중입니다.
```

```text
처리 끝났습니다. <mood:happy>
```

Expected:

- The tag is not visible in the chat bubble.
- The sprite changes.
- The app does not crash.
- The final expression remains briefly before idle.

## Risks

- Streaming chunks may split tags across boundaries.
- Over-eager parsing may remove normal user text if syntax is too broad.
- Sprite flicker can become worse if every chunk changes mood.
- Provider prompts may emit inconsistent tag names.

## Decision

Start with a small explicit tag grammar and verified behavior. Do not build a full animation state machine until the simple tag path is stable.
