# Safety core tests

## Task

Add the first automated safety regression tests for command risk policy and workspace path guarding.

## Changed files

- `scripts/verify.ps1`
- `tests/LivingMetalGhost.Tests/LivingMetalGhost.Tests.csproj`
- `tests/LivingMetalGhost.Tests/Agents/CommandPolicyServiceTests.cs`
- `tests/LivingMetalGhost.Tests/Agents/WorkspaceGuardTests.cs`

## Changes

- Added a dedicated test project targeting `net10.0-windows`.
- Added command policy tests for:
  - safe read auto-run behavior;
  - network read approval behavior;
  - workspace write approval behavior;
  - dangerous command strong approval behavior;
  - unknown command blocking;
  - empty command blocking.
- Added workspace guard tests for:
  - empty root rejection;
  - existing root resolution;
  - root and child acceptance;
  - `..` parent traversal rejection;
  - sibling-prefix rejection;
  - escaping path detection.
- Updated `scripts/verify.ps1` to restore and run the test project when present.

## Verification

Not run locally in this session.

Expected CI/local verification:

```powershell
.\scripts\verify.ps1 -Configuration Release
```

## Remaining risk

- This is the first test project, so CI may reveal package or Windows-targeting setup issues.
- Tests currently cover policy classification and path guards only. Executor argument parsing and approval-card state tests remain future work.
