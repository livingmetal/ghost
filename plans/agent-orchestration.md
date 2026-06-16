# Plan: Agent Orchestration

## Goal

Make the main Ghost character the user-facing orchestrator.

The user should talk to one main character. That character decides whether to answer directly or summon specialized helper agents, coordinates their work, asks for approval before risky actions, and returns one coherent result.

This is the long-term direction for making Ghost more than a mascot chat window: the main character becomes the conductor, and helper agents become summoned specialists.

## Core Principle

The user talks to the main character, not to a pile of tools.

Sub-agents are implementation details unless the UI intentionally shows them in the Agent Dock.

```text
User
  -> Main Character / Orchestrator
      -> Intent Router
      -> Task Planner
      -> Agent Selector
      -> Approval Gate
      -> Result Synthesizer
          -> Research Agent
          -> Coding Agent
          -> Infra Architect Agent
          -> Writer Agent
          -> QA / Reviewer Agent
          -> Memory Agent
          -> Tool Runner
  -> Final response / patch / report
```

## Non-goals

- Do not turn Ghost into a generic shell runner.
- Do not auto-run external coding agents.
- Do not auto-apply patches.
- Do not auto-approve workspace-changing actions.
- Do not merge roleplay memory with work memory.
- Do not expose internal prompts or raw sub-agent chatter as normal chat output.

## Orchestrator Responsibilities

The main character should eventually be responsible for:

- Classifying the request by mode: `Daily`, `Story`, or `Advanced`.
- Deciding whether the task is simple enough to answer directly.
- Decomposing complex tasks into small, explicit work orders.
- Choosing sub-agents by capability, cost, and risk.
- Keeping sub-agent context isolated.
- Asking for user approval before workspace-changing operations.
- Tracking evidence: files read, commands requested, patches proposed, verification result.
- Synthesizing outputs into one grounded final answer.
- Preserving character presence without sacrificing correctness.

## First-class Sub-agents

| Agent | Purpose | Allowed modes | Notes |
| --- | --- | --- | --- |
| Daily Assistant | Direct chat, translation, small summaries | Daily | No local commands |
| Story Director | Scene pacing, NPC reaction, roleplay state updates | Story | Cannot control the player |
| Research Agent | Web, docs, uploaded file, or repository investigation | Daily / Advanced | Must cite or report evidence |
| Infra Architect Agent | Data center, OpenStack, Ceph, HPC, network design review | Advanced | Checks assumptions and risks |
| Coding Agent | Code analysis and patch proposals | Advanced | Patch preview required before writes |
| QA Reviewer | Contradiction check, safety check, verification plan | Advanced | Should run after complex outputs |
| Writer Agent | Reports, README, guides, proposal text | Daily / Advanced | Style depends on task |
| Memory Agent | Project context, preferences, character state | All, scoped | Must respect memory boundaries |
| Tool Runner | Commands, builds, tests, file operations | Advanced only | Approval-gated |

## Execution Model

1. **Triage**
   - Identify mode and risk.
   - Decide whether orchestration is needed.

2. **Plan**
   - Split the request into small work orders.
   - Define expected output for each work order.

3. **Dispatch**
   - Assign work orders to sub-agents.
   - Use the lightest adequate model or tool.

4. **Isolate**
   - Give each sub-agent only the context it needs.
   - Keep noisy intermediate reasoning out of the main conversation.

5. **Review**
   - Run QA for complex or high-risk work.
   - Detect contradictions, missing evidence, and unsafe actions.

6. **Approve**
   - Ask the user before file writes, builds, tests, external agents, or workspace changes.
   - Allow always-approve only for safe repeat discovery.

7. **Synthesize**
   - Merge the useful results.
   - Hide internal chatter unless the user asks for details.

8. **Verify**
   - Run or request the appropriate verification step.
   - If verification cannot be performed, say so explicitly.

## Mode Boundaries

### Daily mode

Daily mode is for normal assistant conversation.

- The orchestrator may answer directly.
- Low-risk utility skills are acceptable.
- Coding, shell, build, Git, and workspace agents must not run.

### Story mode

Story mode is fictional and scene-bound.

- The main character stays in fiction.
- The Story Director may manage fictional scene state.
- No real app, Git, shell, build, settings, log, or file operations.
- Story memory must not become practical project memory.

### Advanced mode

Advanced mode is the full workbench.

- Full orchestration is allowed.
- Workspace changes require approval cards.
- External coding agents are summoned helpers, not replacements for the main character.
- The main character remains the user-facing manager and final synthesizer.

## UI Concepts

### Agent Dock

The Agent Dock should show summoned helpers without making the user manage every detail.

Useful fields:

- Agent name
- Short task label
- Status: `idle`, `planning`, `running`, `blocked`, `waiting_for_approval`, `done`, `failed`
- Risk level
- Evidence or last meaningful activity

### Approval Card

Approval cards should include:

- Requested action
- Why it is needed
- Risk level
- Files, commands, or external agents affected
- Expected result
- Options: approve, reject, always approve only when safe

## Data Model Sketch

```csharp
public sealed record AgentTask(
    string Id,
    string AgentId,
    string Title,
    string Goal,
    AgentRiskLevel RiskLevel,
    ConversationMode Mode,
    IReadOnlyList<string> Inputs,
    AgentTaskStatus Status);

public enum AgentRiskLevel
{
    Low,
    Medium,
    High
}

public enum AgentTaskStatus
{
    Idle,
    Planning,
    Running,
    WaitingForApproval,
    Done,
    Failed,
    Blocked
}
```

Potential future services:

```text
Core/Agents/AgentOrchestrator.cs
Core/Agents/AgentTask.cs
Core/Agents/AgentRegistry.cs
Core/Agents/AgentRouter.cs
Core/Agents/AgentApprovalService.cs
Core/Agents/SubAgents/*.cs
UI/ViewModels/AgentDockViewModel.cs
```

## Prompt Contract

### Orchestrator prompt

The orchestrator prompt should say:

- You are the main Ghost character.
- You may summon helper agents when useful.
- You must not expose raw internal chatter by default.
- You must ask for approval before risky actions.
- You must return one coherent result to the user.

### Sub-agent prompt

Sub-agent prompts should receive narrow work orders.

Sub-agents should return structured output:

```text
Findings:
Evidence:
Risks:
Proposed next action:
```

The final answer should not dump raw sub-agent logs unless the user asks.

## Roadmap

### Phase 0: Documentation skeleton

- Define orchestration model in markdown.
- Keep current behavior unchanged.

### Phase 1: Virtual sub-agents

- Use the same LLM provider.
- Separate prompts by role.
- Keep all execution in memory.
- No external commands.

### Phase 2: Internal orchestration service

- Add `AgentOrchestrator` and `AgentTask` model.
- Add Agent Dock state.
- Add status reporting.

### Phase 3: Approval-gated local work

- Route build/test/file operations through approval cards.
- Track evidence and verification results.

### Phase 4: External coding agent adapters

- Add Codex / Claude Code style adapters only in Advanced mode.
- Treat them as helper agents under the main character.
- Never auto-run them without approval.

### Phase 5: Model routing

- Use lightweight models for simple classification and formatting.
- Use stronger models for architecture, code review, and synthesis.
- Keep token savings as a goal, not a guarantee.

## Design Rule

Ghost should feel like one capable character with a team behind them.

The user should not feel forced to manage a committee. The main character handles the coordination, the helper agents do focused work, and the final answer comes back clean.