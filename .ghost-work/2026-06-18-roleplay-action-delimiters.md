# Roleplay action delimiters

## Task

Switch roleplay action parsing and story prompt wording from single-asterisk action syntax to explicit double-asterisk action syntax.

## Why

Single-asterisk input can be ambiguous. For example, `*A*B*` should remain dialogue text instead of being split into action and dialogue pieces. Double asterisks make action/narration explicit.

## Changed files

- `README.md`
- `src/LivingMetalGhost/Core/Services/RoleplayInputFormatter.cs`
- `src/LivingMetalGhost/Core/Services/PromptAssembler.cs`
- `tests/LivingMetalGhost.Tests/Core/Services/RoleplayInputFormatterTests.cs`
- `plans/roleplay-input-syntax.md`

## Changes

- Parse `**...**` as visible action or scene narration.
- Keep single-asterisk text as spoken dialogue.
- Support a literal single asterisk inside double-asterisk action text.
- Update story-mode prompt wording to describe the same delimiter rules.
- Update README and syntax guide.
- Add tests for the ambiguity case and the inner single-asterisk case.

## Verification

Not run locally in this session.

Expected CI/local verification:

```powershell
.\scripts\verify.ps1 -Configuration Release
```
