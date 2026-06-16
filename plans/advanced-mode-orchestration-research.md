# Research: Advanced Mode Orchestration Path

## Purpose

This note explains what Ghost needs for Advanced mode to evolve into a real orchestrator workbench.

The conclusion: Advanced mode should not become a free-form shell, a generic Git client, or a raw multi-agent chat room. It should become a **main-character-led workbench** where the main Ghost character keeps user-facing control, summons specialized helper agents when useful, gates risky actions, tracks evidence, and synthesizes the final result.

## External patterns reviewed

### OpenAI Agents SDK

Relevant pattern: **manager / agents-as-tools**.

OpenAI documents two broad multi-agent patterns:

1. A manager/orchestrator invokes specialized sub-agents as tools and keeps conversation control.
2. Handoffs transfer control to another specialist agent.

For Ghost, the manager pattern is the better default because the main character should remain the user's stable interface. Handoffs may be useful internally, but user-facing control should return to the main character.

OpenAI's guardrail model is also relevant: input guardrails, output guardrails, and tool guardrails run at different workflow points. Tool guardrails matter most for Ghost because file, build, Git, and shell-like actions must be checked at the operation boundary, not only at the final answer boundary.

Sources:

- https://openai.github.io/openai-agents-python/agents/
- https://openai.github.io/openai-agents-python/guardrails/

### Claude Code subagents

Relevant pattern: **context-isolated subagents with tool scopes**.

Claude Code's subagent model is useful for Ghost because it separates noisy exploration from the main conversation. A subagent can have its own context window, its own system prompt, specific tool access, independent permissions, and a narrow job. It then returns only a summary to the main conversation.

Useful ideas for Ghost:

- Use subagents when exploration would flood the main conversation with logs or file contents.
- Give each subagent a clear description so the orchestrator knows when to invoke it.
- Use tool allowlists/denylists per agent.
- Prefer read-only research agents before any write-capable worker.
- Allow project-level agent definitions that can be versioned with the repo.
- Support parallel research only when subtasks are independent.
- Avoid letting every subagent see all context by default.

Sources:

- https://code.claude.com/docs/en/sub-agents

### Microsoft AutoGen teams

Relevant pattern: **team workflows need explicit control and termination**.

AutoGen's team model shows that multi-agent teams are useful for complex tasks requiring diverse expertise, but should not be the starting point for simple tasks. It also highlights that team runs need termination conditions, reset/resume behavior, streaming/observability, and cancellation.

Useful ideas for Ghost:

- Start with a single orchestrator and only escalate to subagents when needed.
- Add explicit termination conditions for multi-step workflows.
- Track team state so the UI can show whether an agent is running, waiting, blocked, done, or cancelled.
- Add cancellation as a first-class action in the Agent Dock.

Sources:

- https://microsoft.github.io/autogen/stable/user-guide/agentchat-user-guide/tutorial/teams.html

### Recent safety findings for coding agents

Recent research on coding agents warns that agents with shell, file, and network access can perform out-of-scope actions even on benign tasks. This is not only a model-quality issue; it is an authorization and permission-boundary issue. Another evaluation of Claude Code auto mode found that permission classifiers can miss ambiguous state-changing actions, especially when equivalent effects happen through file edits rather than shell commands.

Design implication for Ghost:

- Do not rely on an LLM permission classifier alone.
- Treat file writes, patch application, command execution, external coding agents, build/test runs, and Git state changes as explicit action types with deterministic policy checks.
- Ask for approval based on action type and scope, not just generated natural-language intent.

Sources:

- https://arxiv.org/abs/2605.18583
- https://arxiv.org/abs/2604.04978

## What Advanced mode needs

### 1. Strong mode boundary

Advanced mode must be the only mode allowed to invoke workbench capabilities.

Daily mode:

- No file writes.
- No Git operations.
- No builds/tests.
- No command execution.
- No external coding agents.

Roleplay mode:

- No real tools.
- No project memory changes.
- No accidental file/project references inside fiction.

Advanced mode:

- May read project context.
- May propose plans.
- May ask for approval to run tools.
- May dispatch subagents.
- May synthesize and verify results.

### 2. Orchestrator service

Add an internal service that is not tied directly to WPF UI code.

Suggested files:

```text
src/LivingMetalGhost/Core/Agents/AgentOrchestrator.cs
src/LivingMetalGhost/Core/Agents/AgentRegistry.cs
src/LivingMetalGhost/Core/Agents/AgentRouter.cs
src/LivingMetalGhost/Core/Agents/AgentTask.cs
src/LivingMetalGhost/Core/Agents/AgentRun.cs
src/LivingMetalGhost/Core/Agents/AgentEvent.cs
src/LivingMetalGhost/Core/Agents/AgentRiskPolicy.cs
src/LivingMetalGhost/Core/Agents/AgentApprovalService.cs
src/LivingMetalGhost/Core/Agents/AgentEvidenceStore.cs
src/LivingMetalGhost/UI/ViewModels/AgentDockViewModel.cs
```

The orchestrator should have one job: turn a user request into a controlled workflow.

Minimal flow:

```text
User request
  -> Mode check
  -> Intent classification
  -> Risk classification
  -> Direct answer or task decomposition
  -> Agent selection
  -> Approval check
  -> Agent/tool execution
  -> Evidence collection
  -> QA review when needed
  -> Final synthesis by main character
```

### 3. Agent manifest format

Ghost needs a lightweight internal agent manifest. It does not have to copy Claude's exact format, but it should have the same useful ingredients: identity, description, prompt, model preference, tool scope, risk class, and output contract.

Suggested project path:

```text
agents/*.agent.md
```

Suggested format:

```markdown
---
id: infra-architect
name: Infra Architect
mode: advanced
model_preference: strong
risk_level: read_only
allowed_tools:
  - read_project_files
  - search_repository
  - web_search
blocked_tools:
  - write_file
  - run_shell
  - apply_patch
---

You review data center, OpenStack, Ceph, HPC, and network architecture.
Return findings in this structure:

Findings:
Assumptions:
Risks:
Recommended next action:
```

Start with static manifests. Avoid dynamic self-created agents until the permission system is mature.

### 4. Task and evidence model

Every subagent run should produce structured evidence, not just prose.

Suggested model:

```csharp
public sealed record AgentTask(
    string Id,
    string ParentRunId,
    string AgentId,
    string Title,
    string Goal,
    ConversationMode Mode,
    AgentRiskLevel RiskLevel,
    IReadOnlyList<string> Inputs,
    AgentTaskStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record AgentEvidence(
    string TaskId,
    string Kind,
    string Summary,
    string? Path,
    string? Command,
    string? Url,
    DateTimeOffset CapturedAt);
```

Evidence kinds:

- `file_read`
- `web_source`
- `command_requested`
- `command_result`
- `patch_proposed`
- `verification_result`
- `approval_decision`

This allows the final answer to say: what was inspected, what was changed, what was not verified.

### 5. Deterministic risk policy

Do not let the model decide whether an action is safe by itself.

Risk should be computed from the actual operation type:

| Action | Default risk | Default behavior |
| --- | --- | --- |
| classify intent | low | automatic |
| summarize known context | low | automatic |
| read app config | low/medium | automatic only if non-secret |
| read project files | low/medium | allowed in Advanced mode |
| web search | medium | allowed if user asked or task needs freshness |
| git status | low | allowed in Advanced mode |
| git fetch | medium | approval or remembered safe approval |
| build/test | medium | approval required |
| write file | high | patch preview + approval |
| apply patch | high | approval required |
| git pull/merge/rebase/reset/clean | high | explicit approval required |
| delete files | high | explicit approval; no always-approve |
| external coding agent | high | explicit approval; scoped work order |
| shell command | high | explicit approval unless whitelisted read-only |

### 6. Approval cards

Advanced mode needs approval cards that are more precise than a generic yes/no.

Each card should show:

- action type
- target path or command
- reason
- risk level
- expected output
- whether it changes workspace state
- whether it can be remembered as always-approved

Approval options:

```text
Approve once
Reject
Always approve this exact safe pattern
```

Do not offer always-approve for:

- file writes
- deletes
- patch application
- git pull/merge/rebase/reset/clean
- external coding agents
- broad shell commands

### 7. Agent Dock

The Agent Dock should make orchestration visible without making the user manage every small detail.

Minimum display fields:

- agent name
- task title
- status
- risk level
- last event
- pending approval count

Statuses:

```text
idle
planned
running
waiting_for_approval
blocked
done
failed
cancelled
```

Actions:

```text
Open details
Approve / reject pending action
Cancel task
Copy evidence summary
```

### 8. Context isolation and compaction

Ghost should avoid dumping all chat history into every subagent.

Recommended context policy:

- Give each subagent a narrow work order.
- Include only relevant files, snippets, and facts.
- Return summaries, not raw exploration logs.
- Keep the main conversation clean.
- Persist only task summaries and evidence, not every token.

This is especially important because Ghost has three memory domains:

```text
Daily memory
Roleplay state
Advanced/project memory
```

These must stay separate.

### 9. Verification loop

Advanced mode should not claim completion just because a patch was generated.

Completion states:

```text
proposed
approved
applied
verified
unverified
failed_verification
```

The final response should distinguish:

- what was planned
- what was changed
- what was verified
- what could not be verified
- what remains risky

### 10. External agent adapters

Codex / Claude Code / other coding agents should be adapters behind the orchestrator, not the UI itself.

Suggested adapter interface:

```csharp
public interface IExternalAgentAdapter
{
    string Id { get; }
    string DisplayName { get; }
    AgentCapabilitySet Capabilities { get; }
    Task<ExternalAgentPlan> PlanAsync(AgentWorkOrder order, CancellationToken ct);
    Task<ExternalAgentResult> RunAsync(AgentWorkOrder order, AgentApprovalContext approvals, CancellationToken ct);
}
```

Rules:

- External agents are Advanced-mode only.
- They receive scoped work orders.
- They cannot auto-apply changes unless the orchestrator has explicit approval.
- Their output must be converted into Ghost evidence records.
- The main character summarizes the result.

## Recommended implementation phases

### Phase A: Documentation and contracts

Goal: make the shape clear before writing code.

Tasks:

- Keep `plans/agent-orchestration.md` as the top-level architecture.
- Add this research note for rationale and implementation direction.
- Define first internal models in comments or design docs before building UI.

### Phase B: Agent Dock without real agents

Goal: build the UI skeleton first.

Tasks:

- Add `AgentDockViewModel`.
- Show fake or in-memory tasks.
- Add statuses and approval-card placeholder.
- No command execution.

This prevents the project from jumping straight into dangerous shell execution.

### Phase C: Virtual subagents

Goal: simulate orchestration through prompts using the existing LLM provider.

Tasks:

- Add `AgentRegistry` with static in-code agents.
- Add `AgentTask` and `AgentRun` models.
- Route complex Advanced-mode requests to virtual roles:
  - Researcher
  - Infra Architect
  - Coding Analyst
  - QA Reviewer
  - Writer
- Keep all actions read-only.

### Phase D: Read-only tools

Goal: let Advanced mode inspect state safely.

Tasks:

- Add read-only repository search.
- Add file-read capability with size limits.
- Add `git status` only.
- Add evidence records.

### Phase E: Approval-gated build/test

Goal: allow useful verification without writes.

Tasks:

- Add build/test work orders.
- Show exact command in approval card.
- Capture stdout/stderr summary.
- Store verification evidence.

### Phase F: Patch proposal flow

Goal: support file changes safely.

Tasks:

- Generate patch proposals only.
- Show diff preview.
- Apply only after approval.
- Run verification only after approval.

### Phase G: External agent adapters

Goal: summon Codex/Claude Code as helpers.

Tasks:

- Add adapter contracts.
- Start with dry-run / plan-only mode.
- Require explicit approval for actual run.
- Capture results as evidence.

## Recommended first agents

### `advanced-router`

Purpose: classify intent, mode, risk, and whether orchestration is needed.

No tools.

### `repo-researcher`

Purpose: read/search repository files and summarize relevant implementation points.

Read-only tools only.

### `infra-architect`

Purpose: architecture review for data center, OpenStack, Ceph, HPC, network, Windows/Linux operations.

Read-only and web/search tools only.

### `coding-analyst`

Purpose: inspect code and propose changes.

Read-only initially. Later may propose patches but not apply them.

### `qa-reviewer`

Purpose: review proposed answers and patches for contradictions, missing verification, and scope creep.

No write tools.

### `writer`

Purpose: turn reviewed material into README, design docs, reports, or user-facing summaries.

No write tools initially; output text only.

## Key design decisions for Ghost

### Decision 1: Manager pattern first

Use main-character manager orchestration as the default. Do not let subagents become the user-facing conversation unless explicitly opened in details.

### Decision 2: Read-only before write-capable

The first real Advanced-mode agents should only inspect and summarize.

### Decision 3: Permission by operation type

Policy checks should be deterministic and attached to concrete operations.

### Decision 4: Evidence before completion

A task is not complete until Ghost has evidence. For code work, evidence means at least build/test output or an explicit statement that verification was not run.

### Decision 5: Character is UI, orchestrator is core

Do not bury orchestration logic in `MainViewModel`. The main character presents the work, but the orchestration engine should live in `Core/Agents`.

## Anti-patterns to avoid

- One giant prompt that does planning, coding, reviewing, and final writing at once.
- Subagents with all tools by default.
- Letting a model decide its own permission level.
- Running shell commands from Daily mode or Roleplay mode.
- Treating `always approve` as a convenience button for state-changing actions.
- Showing every internal subagent message in the main chat.
- Calling work complete without verification evidence.
- Mixing roleplay state with project work memory.

## Minimal viable target

The smallest useful version of Advanced-mode orchestration is:

```text
Advanced user request
  -> Router classifies task and risk
  -> Orchestrator creates 1-3 read-only AgentTasks
  -> Agent Dock shows running tasks
  -> Subagents return structured summaries
  -> QA checks result
  -> Main character returns final answer with evidence
```

No file writes. No shell. No external agents.

That version already gives Ghost the right skeleton without opening the trapdoor under the floorboards.

## Practical next issue list

1. Add `Core/Agents` models:
   - `AgentTask`
   - `AgentRun`
   - `AgentEvidence`
   - `AgentRiskLevel`
   - `AgentTaskStatus`
2. Add `AgentRegistry` with static built-in agent definitions.
3. Add `AgentDockViewModel` with mock task state.
4. Add a read-only `advanced-router` virtual agent.
5. Add a read-only `repo-researcher` virtual agent.
6. Add approval-card model before adding any command execution.
7. Add evidence store.
8. Add build/test approval flow.
9. Add patch proposal flow.
10. Only then consider Codex/Claude Code adapters.
