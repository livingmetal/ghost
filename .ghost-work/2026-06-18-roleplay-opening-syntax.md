# Roleplay opening syntax guide

## Task

Align the roleplay opening text guide with the double-asterisk action syntax used by the formatter, prompt, README, and syntax guide.

## Changed files

- `src/LivingMetalGhost/Core/Services/StoryStateStore.cs`
- `tests/LivingMetalGhost.Tests/Core/Services/StoryStateStoreTests.cs`
- `.ghost-work/2026-06-18-roleplay-opening-syntax.md`

## Changes

- Updated `StoryStateStore.BuildOpeningText` so the visible opening guide shows double-asterisk action/narration syntax.
- Clarified that single-asterisk text is ordinary italic emphasis.
- Added a regression test to prevent the old single-asterisk action guide from returning.

## Verification

Not run locally in this session.

Expected CI/local verification:

```powershell
.\scripts\verify.ps1 -Configuration Release
```
