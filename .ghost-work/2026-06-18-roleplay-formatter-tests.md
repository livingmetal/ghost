# Roleplay formatter tests

## Task

Add regression tests for `RoleplayInputFormatter` after the initial safety test project was merged.

## Changed files

- `tests/LivingMetalGhost.Tests/Core/Services/RoleplayInputFormatterTests.cs`

## Changes

- Covered plain text pass-through behavior.
- Covered dialogue/action/thought separation.
- Covered private-thought guard instructions.
- Covered whitespace normalization across CRLF and extra spaces.
- Covered current double-asterisk parsing behavior so future syntax changes are explicit.

## Verification

Not run locally in this session.

Expected CI/local verification:

```powershell
.\scripts\verify.ps1 -Configuration Release
```

## Remaining risk

- Current production parser still documents `*...*` as action/narration syntax.
- If the project decides to switch fully to `**...**`, this test should be updated in the same PR as the parser change.
