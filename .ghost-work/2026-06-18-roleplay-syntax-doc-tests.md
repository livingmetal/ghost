# Roleplay syntax documentation guard tests

## Task

Add a regression test that keeps roleplay syntax documentation and prompt wording aligned.

## Changed files

- `tests/LivingMetalGhost.Tests/Core/Services/RoleplaySyntaxDocumentationTests.cs`
- `.ghost-work/2026-06-18-roleplay-syntax-doc-tests.md`

## Changes

- Read `PromptAssembler.cs`, `README.md`, and `plans/roleplay-input-syntax.md` from the repository root during tests.
- Assert that all three describe double-asterisk action/narration syntax.
- Assert that old single-asterisk action syntax wording does not reappear in key prompt/documentation locations.

## Verification

Not run locally in this session.

Expected CI/local verification:

```powershell
.\scripts\verify.ps1 -Configuration Release
```
