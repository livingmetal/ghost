# Roleplay action delimiters

## Task

Switch roleplay input action parsing from single asterisks to double asterisks.

## Why

Single-asterisk input can be ambiguous. For example, `*A*B*` should remain dialogue text instead of being split into action and dialogue pieces. Double asterisks make action/narration explicit.

## Changed files

- `src/LivingMetalGhost/Core/Services/RoleplayInputFormatter.cs`
- `tests/LivingMetalGhost.Tests/Core/Services/RoleplayInputFormatterTests.cs`
- `plans/roleplay-input-syntax.md`

## Changes

- Parse `**...**` as visible action or scene narration.
- Keep single-asterisk text as spoken dialogue.
- Support a literal single asterisk inside double-asterisk action text.
- Add tests for the ambiguity case and the inner single-asterisk case.
- Add a syntax reference document.

## Verification

Not run locally in this session.

Expected CI/local verification:

```powershell
.\scripts\verify.ps1 -Configuration Release
```

## Follow-up

Update roleplay prompt wording in `PromptAssembler.cs` to describe the same delimiter rules. The parser and tests are updated in this branch, but that long prompt file still needs a separate small edit path.
