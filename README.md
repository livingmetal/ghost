# LivingMetalGhost

LivingMetalGhost is a Windows desktop LLM character assistant inspired by modern Ukagaka/Nanika-style companions.

It is intended to be:

- a lightweight desktop character for daily conversation,
- a visual-novel/ORPG-style partner in roleplay mode,
- and a practical workbench for advanced agent-assisted tasks.

The project targets a WPF single-file deployment model and keeps LLM providers, character assets, prompts, tools, and future agent integrations replaceable.

## Current Direction

Ghost is organized around three user-facing modes:

1. **Daily mode**
   - Normal character-flavored assistant conversation.
   - No local command execution.
   - No Git, build, or file operations.

2. **Roleplay mode**
   - Fictional visual-novel/ORPG-style interaction.
   - User input syntax:

     ```text
     plain text     -> spoken dialogue
     **text**       -> visible action / narration
     (text)         -> inner thought
     ```

   - Single-asterisk text such as `*A*B*` is treated as spoken dialogue, not action syntax.
   - The character must not read inner thoughts directly.
   - User-facing Korean wording should prefer `롤플레잉 모드`.

3. **Advanced mode**
   - Practical workbench mode.
   - Future Codex/Claude Code style agent integration belongs here.
   - Local commands and workspace-changing actions must be approval-gated.

## Requirements

- Windows 10/11
- .NET 10 SDK
- WPF-capable Windows environment

The project is commonly built for:

- `win-x64`
- `win-arm64`

Build scripts resolve `dotnet` from `PATH` first. If `dotnet` is installed in a non-standard location, set `LIVINGMETAL_DOTNET` to the full path of the dotnet executable instead of editing scripts.

Example:

```powershell
$env:LIVINGMETAL_DOTNET = "D:\tools\dotnet\dotnet.exe"
```

## Run

```powershell
dotnet run --project .\src\LivingMetalGhost\LivingMetalGhost.csproj
```

## Verify

Preferred verification command:

```powershell
.\scripts\verify.ps1
```

Release build verification:

```powershell
.\scripts\verify.ps1 -Configuration Release
```

Optional publish verification:

```powershell
.\scripts\verify.ps1 -PublishRuntimeIdentifier win-arm64
```

The verification script restores and builds the WPF project. Optional publish verification delegates to `publish.ps1` for the selected runtime. Do not claim completion without recording the exact verification command and result.

## Publish

Default publish:

```powershell
.\publish.ps1
```

ARM64 publish:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish.ps1 -RuntimeIdentifier win-arm64
```

x64 publish:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish.ps1 -RuntimeIdentifier win-x64
```

`publish-all.ps1` publishes both ARM64 and x64 executables.

## Data Path

Runtime user data is stored under:

```text
%APPDATA%\LivingMetalGhost
```

## Providers

Current and planned provider directions include:

- Mock
- OpenAI-Compatible endpoints
- Gemini
- OpenAI
- Ollama
- Future advanced-mode Codex/Claude Code style integrations

Provider-specific behavior should remain isolated. Character prompts, mode prompts, and tool-routing prompts should not be tangled together.

## Agent Orchestration Direction

Long term, the main Ghost character should become the user-facing orchestrator: it classifies the user's request, decides whether to answer directly or summon helper agents, routes complex work to specialized agents, asks for approval before risky actions, and returns one coherent result.

See [`plans/agent-orchestration.md`](./plans/agent-orchestration.md) for the planned orchestrator skeleton.

For the roadmap toward Codex/Claude-Code-class Advanced mode capability, see [`plans/coding-agent-parity-roadmap.md`](./plans/coding-agent-parity-roadmap.md).

## Character and Sprite Direction

Ghost is sprite-friendly by design.

Current direction:

- Character assets should be modularly addable.
- Mood/emotion tags must not appear in chat bubbles.
- Sprite changes should be possible during speaking.
- Rapid sprite flicker should be avoided.
- Final expression should remain briefly after speech ends.

See [`plans/sprite-emotion-system.md`](./plans/sprite-emotion-system.md) for the planned sprite/mood workflow.

## Agent and AI Contributor Guide

Coding agents may not all automatically read `AGENTS.md`, so human users should explicitly point agents to it when starting a new session.

Important files:

- [`AGENTS.md`](./AGENTS.md): conventional entry point for coding agents.
- [`AGENT.md`](./AGENT.md): canonical repository handoff and project direction.
- [`plans/agent-orchestration.md`](./plans/agent-orchestration.md): long-term main-character orchestrator and sub-agent architecture.
- [`plans/advanced-mode-orchestration-research.md`](./plans/advanced-mode-orchestration-research.md): research note for Advanced-mode orchestration design.
- [`plans/coding-agent-parity-roadmap.md`](./plans/coding-agent-parity-roadmap.md): roadmap toward Codex/Claude-Code-class coding agent app capability.
- [`plans/agent-workflow.md`](./plans/agent-workflow.md): plan-first and evidence-first workflow.
- [`scripts/verify.ps1`](./scripts/verify.ps1): standard verification script.
- [`.ghost-work/README.md`](./.ghost-work/README.md): evidence note guidance.

Workflow rule:

> Plan first, change small, verify before claiming completion.

Do not report a task as complete without verification evidence. If verification cannot be run, say so explicitly.

## Safety Notes

- API keys should be stored securely, not committed.
- Sensitive actions are intentionally out of scope for daily and roleplay modes.
- Do not add unrestricted command execution.
- Do not auto-start external coding agents.
- Do not auto-apply patches.
- Do not auto-approve workspace-changing commands.

The boundary between daily conversation, fictional roleplay, and practical advanced work is central to the app. Keep it sharp.
