# Roadmap: Coding Agent App Parity for Advanced Mode

## Purpose

This roadmap defines what Ghost Advanced mode needs before it can credibly approach the usefulness of Codex-style and Claude-Code-style coding agent apps.

The target is not to clone either product. Ghost has a different interface: the main character remains the user-facing orchestrator. The goal is functional parity in the important engineering workflows:

- understand a repository,
- plan work,
- edit safely,
- run verification,
- preserve sessions,
- delegate to subagents,
- integrate tools,
- surface evidence,
- and keep the user in control.

## Product Positioning

Ghost Advanced mode should become:

```text
Character-led coding workbench
  = repository-aware assistant
  + agent task runner
  + approval and safety layer
  + diff and verification UI
  + session/evidence memory
  + optional external agent adapters
```

It should not become:

```text
free-form terminal wrapper
raw multi-agent chat room
uncontrolled auto-fixer
Git client clone
IDE replacement
```

## Parity Feature Matrix

| Capability | Codex / Claude Code class behavior | Ghost target |
| --- | --- | --- |
| Repository awareness | Reads project files, structure, instructions | Workspace index + file map + AGENT/README awareness |
| Persistent project instructions | AGENTS.md / CLAUDE.md style context | `AGENT.md`, `AGENTS.md`, `plans/*.md`, optional `agents/*.agent.md` |
| Agent loop | Plan, use tools, inspect results, continue | `AgentOrchestrator` controls loop with explicit states |
| Tool execution | Shell, file, Git, tests, web/MCP | Tools behind deterministic risk policy |
| Permission model | Ask/auto/deny modes | Approval cards, no silent state-changing actions |
| Patch workflow | Edit files, show diff, review | Proposal first, diff preview, apply only after approval |
| Verification | Run tests/build/lint | Approval-gated verify actions and evidence records |
| Subagents | Context-isolated specialists | `AgentRegistry` + virtual agents + later external adapters |
| Context compaction | Summarize old context and tool results | Task summaries + evidence store + memory domain separation |
| Session persistence | Resume long tasks | Append-only `AgentRun` log under app data or `.ghost-work` |
| Worktree/sandbox | Isolated task environment | Later: Git worktree or copy-on-write workspace per agent |
| Extensibility | MCP, hooks, plugins, slash commands | Later: MCP client, command palette, hooks, project agents |
| Human review | User approves changes | Main character presents plan, risk, diff, verification result |

## Completeness Definition

Advanced mode is not "complete" when it can answer coding questions. It is complete enough when it can safely complete this workflow:

```text
User: "Fix this bug / add this feature"

Ghost:
1. Reads project instructions.
2. Builds a task plan.
3. Finds relevant files.
4. Explains proposed change.
5. Generates a patch preview.
6. Asks for approval.
7. Applies patch only after approval.
8. Runs approved verification.
9. Summarizes what changed and what passed/failed.
10. Stores evidence for future sessions.
```

## Architecture Layers

### 1. UI Layer

Main pieces:

- Advanced Workbench panel
- Agent Dock
- Approval Card panel
- Diff Preview panel
- Evidence / Run Log panel
- Workspace selector
- Command palette

The main character remains visible as the manager. The UI should feel like the character has opened a workbench, not like the character was replaced by an IDE.

### 2. Orchestration Layer

Core services:

```text
AgentOrchestrator
AgentRouter
AgentPlanner
AgentRegistry
AgentRunStore
AgentEvidenceStore
AgentApprovalService
AgentRiskPolicy
AgentContextBuilder
AgentResultSynthesizer
```

Responsibilities:

- classify tasks,
- decide direct answer vs workflow,
- create task graph,
- select agents,
- enforce mode/risk policy,
- request approvals,
- collect evidence,
- resume/cancel runs,
- produce final response.

### 3. Workspace Layer

Core services:

```text
WorkspaceService
WorkspaceIndexService
ProjectInstructionLoader
FileReadService
FileSearchService
PatchService
DiffService
GitStatusService
```

Responsibilities:

- select repository root,
- read allowed files,
- ignore build outputs and binary files,
- load `README.md`, `AGENT.md`, `AGENTS.md`, and `plans/*.md`,
- search files by name/content,
- generate patch previews,
- apply approved changes.

### 4. Execution Layer

Core services:

```text
CommandRunner
BuildRunner
TestRunner
ExternalAgentRunner
McpClientHost
```

Rules:

- Advanced mode only.
- Explicit approval required for state-changing or long-running actions.
- Capture stdout/stderr, exit code, duration, and working directory.
- Prefer allowlisted commands before general shell.
- Never pass secrets to prompts or logs.

### 5. Memory and Evidence Layer

Core services:

```text
AgentRunStore
AgentEvidenceStore
TaskSummaryStore
WorkspaceMemoryStore
```

Must persist:

- user request,
- plan,
- task graph,
- approvals,
- files inspected,
- diffs proposed,
- diffs applied,
- commands run,
- verification result,
- final summary.

Must not persist:

- secrets,
- raw huge logs by default,
- unrelated chat noise,
- roleplay state in work memory.

## Data Model Draft

```csharp
public sealed record WorkspaceContext(
    string RootPath,
    string DisplayName,
    IReadOnlyList<ProjectInstruction> Instructions,
    IReadOnlyList<WorkspaceFileSummary> FileMap,
    DateTimeOffset IndexedAt);

public sealed record AgentRun(
    string Id,
    string UserRequest,
    ConversationMode Mode,
    AgentRunStatus Status,
    IReadOnlyList<AgentTask> Tasks,
    IReadOnlyList<AgentEvidence> Evidence,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record AgentTask(
    string Id,
    string AgentId,
    string Title,
    string Goal,
    AgentTaskStatus Status,
    AgentRiskLevel RiskLevel,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs);

public sealed record ApprovalRequest(
    string Id,
    string RunId,
    string ActionType,
    string Target,
    string Reason,
    AgentRiskLevel RiskLevel,
    bool ChangesWorkspace,
    bool CanAlwaysApprove,
    DateTimeOffset CreatedAt);

public sealed record PatchProposal(
    string Id,
    string RunId,
    IReadOnlyList<PatchFileChange> Files,
    string Rationale,
    PatchProposalStatus Status);
```

## Permission Model

Permission should be deterministic. The model can recommend an action, but the application decides whether approval is required.

### Risk classes

```text
Low
  read project instructions
  classify intent
  summarize loaded context
  search indexed file names

Medium
  read arbitrary project files
  web search
  git status
  run build/test after approval
  git fetch after approval

High
  write file
  apply patch
  delete file
  run arbitrary shell
  git pull/merge/rebase/reset/clean
  launch external coding agent
```

### Permission rules

- Low risk may run automatically in Advanced mode.
- Medium risk may run automatically only when read-only and expected by the task; otherwise ask.
- High risk always requires explicit approval.
- Always-approve may only apply to exact low/medium read-only patterns.
- Always-approve must never apply to file writes, deletes, patch application, arbitrary shell, or external coding agents.

## Required Workflows

### Workflow 1: Repository read and explain

Goal: answer repository questions without changing files.

Required pieces:

- workspace root selection,
- project instruction loader,
- file map,
- file search,
- bounded file reads,
- evidence summary.

Acceptance test:

```text
User asks: "Where is roleplay mode implemented?"
Ghost identifies relevant files, cites inspected paths, and does not request file write approval.
```

### Workflow 2: Plan-only coding task

Goal: produce an implementation plan without editing.

Required pieces:

- coding analyst agent,
- repo researcher agent,
- QA reviewer,
- final synthesizer.

Acceptance test:

```text
User asks: "Plan how to add Agent Dock cancellation."
Ghost returns phased plan, affected files, risks, and verification steps.
```

### Workflow 3: Patch proposal

Goal: propose changes without applying them.

Required pieces:

- patch proposal model,
- diff renderer,
- approval card,
- no direct file writes.

Acceptance test:

```text
User asks: "Implement it."
Ghost shows patch preview and asks approval before applying.
```

### Workflow 4: Approved patch apply

Goal: safely apply approved patches.

Required pieces:

- patch application service,
- rollback/milestone marker,
- evidence record,
- post-apply summary.

Acceptance test:

```text
User approves patch.
Ghost applies only the displayed diff and records changed files.
```

### Workflow 5: Verification

Goal: run build/test/lint only after approval.

Required pieces:

- command approval,
- command runner,
- output summarizer,
- evidence store.

Acceptance test:

```text
Ghost asks before running .\scripts\verify.ps1.
After approval, it captures exit code and summary.
```

### Workflow 6: External agent adapter

Goal: use Codex/Claude Code style tools as helpers.

Required pieces:

- adapter interface,
- scoped work order,
- approval gate,
- result parser,
- diff/evidence import.

Acceptance test:

```text
User asks Ghost to summon Codex for a focused task.
Ghost explains scope, asks approval, runs adapter only after approval, and imports results into evidence.
```

## External Agent Adapter Contract

```csharp
public interface IExternalCodingAgentAdapter
{
    string Id { get; }
    string DisplayName { get; }
    ExternalAgentCapabilities Capabilities { get; }

    Task<ExternalAgentAvailability> CheckAvailabilityAsync(CancellationToken ct);
    Task<ExternalAgentPlan> CreatePlanAsync(ExternalAgentWorkOrder order, CancellationToken ct);
    Task<ExternalAgentResult> RunAsync(
        ExternalAgentWorkOrder order,
        ExternalAgentApprovalContext approval,
        CancellationToken ct);
}
```

Adapter rules:

- Work order must include allowed scope.
- Work order must include blocked actions.
- External agent output must be treated as untrusted until reviewed.
- Any proposed file change must return as a diff, not an auto-applied mutation unless explicitly approved.
- Ghost must own the final user summary.

## MCP and Tool Extensibility

MCP support is a later milestone, but the architecture should leave room for it.

Potential services:

```text
McpServerRegistry
McpToolCatalog
McpPermissionPolicy
McpInvocationService
```

Rules:

- MCP tools must be registered with risk metadata.
- MCP tool calls must pass through `AgentRiskPolicy`.
- User-visible approval should show the target server and tool name.
- MCP should be disabled in Daily and Roleplay modes unless the tool is explicitly classified as low-risk utility.

## Context Management

Advanced mode needs compaction from the beginning.

Context classes:

```text
Immediate user request
Project instructions
Relevant file snippets
Recent task summaries
Evidence summaries
Current approval state
```

Do not include by default:

```text
Full chat history
All project files
Raw command logs
Roleplay state
Secrets/config values
```

Compaction rules:

- Summarize completed tasks into concise run notes.
- Keep evidence records separately from chat prose.
- Keep file snippets path-scoped and line-bounded.
- Rehydrate context from evidence only when needed.

## Security Requirements

### Minimum security gate before file writes

Before any file write feature ships:

- deterministic risk policy exists,
- approval card exists,
- diff preview exists,
- applied patch is constrained to previewed diff,
- evidence record is stored,
- delete operations are blocked or separately confirmed.

### Minimum security gate before shell execution

Before any command runner ships:

- command text is visible to user,
- working directory is visible,
- timeout exists,
- stdout/stderr are captured,
- secrets are redacted where possible,
- high-risk commands are blocked or require explicit approval,
- command cannot run from Daily/Roleplay modes.

### Minimum security gate before external agents

Before Codex/Claude Code adapters ship:

- adapter availability check exists,
- scoped work order exists,
- user approval exists,
- output import as evidence exists,
- diff review exists,
- cancellation exists.

## Milestones

### M0: Current documentation baseline

Status: started.

- `plans/agent-orchestration.md`
- `plans/advanced-mode-orchestration-research.md`
- this roadmap

### M1: Advanced Workbench Shell

Deliverables:

- Advanced mode panel layout
- Agent Dock placeholder
- Approval Card placeholder
- Evidence panel placeholder
- No real tools yet

### M2: Read-only Repository Intelligence

Deliverables:

- workspace selector
- project instruction loader
- file map
- bounded file reader
- repo search
- evidence records

### M3: Virtual Agent Orchestrator

Deliverables:

- static `AgentRegistry`
- `AgentRun` and `AgentTask` models
- virtual researcher/coding/QA/writer agents
- result synthesis
- task status in Agent Dock

### M4: Approval System

Deliverables:

- deterministic `AgentRiskPolicy`
- `ApprovalRequest` model
- approval card UI
- remembered approval for safe read-only patterns only
- cancellation support

### M5: Diff and Patch Proposal

Deliverables:

- patch proposal model
- diff viewer
- patch approval
- safe apply service
- change evidence

### M6: Verification Runner

Deliverables:

- build/test command templates
- approval-gated run
- stdout/stderr capture
- timeout
- verification summary

### M7: External Coding Agent Adapters

Deliverables:

- `IExternalCodingAgentAdapter`
- Codex adapter skeleton
- Claude Code adapter skeleton
- plan-only mode
- run-with-approval mode
- result import

### M8: Worktree/Sandbox Isolation

Deliverables:

- create task branch or worktree
- run agent in isolated workspace
- compare diff against base
- discard or merge with approval

### M9: MCP and Hooks

Deliverables:

- MCP registry
- MCP tool risk metadata
- tool invocation approval
- pre/post run hooks
- project-level tool policy

### M10: Long-running Session Quality

Deliverables:

- resume/cancel runs
- append-only run log
- context compaction
- crash recovery
- summary cards

## Recommended Implementation Order

Do not jump directly to M7. External agent integration before M2-M5 would give Ghost power without brakes.

Recommended sequence:

```text
M1 -> M2 -> M3 -> M4 -> M5 -> M6 -> M7 -> M8 -> M9 -> M10
```

## First Concrete Code Sprint

The next sprint should implement the safe skeleton only:

1. Add `Core/Agents` model records/enums.
2. Add `AgentRegistry` with static agent definitions.
3. Add `AgentRunStore` as in-memory first.
4. Add `AgentDockViewModel` with mock and in-memory runs.
5. Add Advanced mode UI area for Agent Dock.
6. Add no file writes, no command execution, no external agents.

Acceptance criteria:

- App builds.
- Advanced mode can show an empty Agent Dock.
- A fake/demo task can move through planned/running/done states.
- No command execution path exists.
- Daily and Roleplay modes cannot access Agent Dock execution controls.

## Definition of Done for Coding-Agent Parity

Ghost reaches practical coding-agent parity when all of these are true:

- It can load and summarize project instructions.
- It can search and read a repository safely.
- It can plan coding tasks with affected files and risks.
- It can propose diffs.
- It can apply approved diffs only.
- It can run approved verification commands.
- It can keep a task/evidence log.
- It can resume or cancel long-running tasks.
- It can delegate scoped work to internal subagents.
- It can optionally delegate scoped work to Codex/Claude Code adapters.
- It can keep the main character as final orchestrator.
- It can clearly say what was verified and what was not.

## Final Design Constraint

Power is not the scarce resource. Control is.

Ghost should become powerful only where the approval, evidence, and recovery systems are already strong enough to hold that power.
