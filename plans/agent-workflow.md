# Plan: Agent Workflow Discipline

## Goal

Adopt a lightweight LazyCodex + Fablize style workflow for Ghost development without taking a hard dependency on either external project.

In this repository:

- LazyCodex-style means plan-first work with explicit tasks.
- Fablize-style means evidence-first completion with build and verification notes.

The concrete fablize procedures (per-task router, investigation protocol,
verification grounding, multi-story gate, early-stop prevention) are written out
in [`fablize-discipline.md`](./fablize-discipline.md). This plan covers the
overall workflow and completion gate; that file covers the per-task discipline.

## Scope

This plan covers agent and maintainer workflow only.

Included:

- Plan-first development under `plans/`.
- Verification through `scripts/verify.ps1`.
- Evidence notes under `.ghost-work/` when useful.
- Completion rules for Codex, Claude Code, and future agents.

Excluded:

- Installing LazyCodex itself.
- Installing Fablize itself.
- Adding unrestricted shell execution to the app.
- Auto-running Codex or Claude Code from the app.
- Auto-applying patches without explicit approval.

## Workflow

1. Read `AGENTS.md` first.
2. Pick or create a plan under `plans/`.
3. Convert the request into small checkable tasks.
4. Make the smallest safe code or documentation change.
5. Run verification.
6. Report evidence, not vibes.

## Completion Gate

A task is not complete until the agent can provide:

- Changed file list.
- Summary of what changed.
- Command used for verification.
- Verification result.
- Manual test notes if UI, sprite, mode, or prompt behavior changed.
- Known remaining risk.

If verification was not possible, the agent must say why and avoid claiming the change is fully verified.

## Standard Verification

Use:

```powershell
.\scripts\verify.ps1
```

Fallback:

```powershell
dotnet build .\src\LivingMetalGhost\LivingMetalGhost.csproj -c Release
```

Optional publish verification:

```powershell
.\publish.ps1 -RuntimeIdentifier win-arm64
```

## Evidence Template

```text
Date:
Task:
Changed files:
Commands run:
Result:
Manual check:
Remaining risk:
```

## Tasks

- [x] Add this workflow plan.
- [ ] Keep this plan compatible with the canonical rules in `AGENTS.md`.
- [ ] Use `scripts/verify.ps1` for local verification.
- [ ] Add feature-specific plans before larger work.
- [ ] Add UI/manual smoke-test notes for sprite and mode behavior.

## Risks

- Too much process can slow down small changes.
- Verification may fail on machines without .NET 10 SDK or Windows desktop build support.
- Evidence logs must not contain secrets, private chat transcripts, or API keys.

## Decision

Use this as a repository convention, not as a heavy framework. Small verified changes are preferred over large unverified edits.
