# Agents Guide

This repository uses `AGENTS.md` as the conventional entry point for coding agents.

The canonical handoff guide is [`AGENT.md`](./AGENT.md). Read it first and follow it for project direction, build commands, mode boundaries, roleplay rules, approval policy, compatibility pitfalls, and current product constraints.

## Agent Workflow

Ghost development follows a lightweight LazyCodex + Fablize style workflow:

- Plan first.
- Change small.
- Verify before claiming completion.
- Report evidence, not guesses.

Use these files:

- [`AGENT.md`](./AGENT.md): canonical project and agent guide.
- [`plans/agent-orchestration.md`](./plans/agent-orchestration.md): main-character orchestrator and sub-agent architecture plan.
- [`plans/agent-workflow.md`](./plans/agent-workflow.md): workflow discipline and completion gate.
- [`plans/fablize-discipline.md`](./plans/fablize-discipline.md): actionable fablize-style procedures (router, investigation, grounding, multi-story gate, early-stop prevention).
- [`plans/sprite-emotion-system.md`](./plans/sprite-emotion-system.md): planned sprite/mood tag behavior.
- [`scripts/verify.ps1`](./scripts/verify.ps1): restore/build verification script.
- [`.ghost-work/README.md`](./.ghost-work/README.md): evidence note guidance.

## Required Completion Rule

Do not report a task as complete unless verification evidence is available.

Preferred command:

```powershell
.\scripts\verify.ps1
```

If verification cannot be run, say so explicitly and explain why.

## Safety Boundary

Do not add unrestricted command execution, auto-start external coding agents, auto-apply patches, or auto-approve workspace-changing commands without explicit user approval.