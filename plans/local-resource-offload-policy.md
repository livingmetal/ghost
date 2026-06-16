# Policy: Local Resource Offload

## Purpose

Ghost should not spend LLM tokens on work that can be done deterministically and cheaply on the local machine.

This policy applies especially to:

- Advanced mode,
- Story mode,
- orchestration,
- sprite/emotion rendering,
- memory summarization and retrieval,
- code/workspace analysis.

The LLM should focus on judgment, language, planning, synthesis, and fictional character performance. The local app should handle parsing, indexing, state machines, timers, rendering, deterministic policy checks, and repetitive transformations.

## Core Principle

Use the local machine as the stage crew.

The LLM is not the renderer, scheduler, file indexer, approval engine, diff tool, markdown parser, animation state machine, or command policy engine.

```text
LLM should do:
  reasoning
  planning
  dialogue
  summarization
  code understanding where needed
  final synthesis

Local app should do:
  parsing
  routing
  indexing
  memory retrieval
  markdown segmentation
  mood tag stripping
  sprite state transitions
  timers
  approval/risk policy
  diff generation/rendering
  file search
  command execution wrapper
  evidence storage
```

## Advanced Mode Offload Targets

### 1. Workspace indexing

Do locally:

- enumerate files,
- respect `.gitignore` / configured ignores,
- skip binaries and build outputs,
- calculate file size and modified time,
- detect project markers,
- load `README.md`, `AGENT.md`, `AGENTS.md`, and `plans/*.md`.

Give the LLM only a compact index and selected snippets.

Do not paste entire repositories into prompts.

### 2. Repository search

Do locally:

- filename search,
- extension filtering,
- simple text search,
- symbol-ish search when cheap,
- recently modified file lookup.

Use the LLM after retrieval to interpret results, not to brute-force scan everything.

### 3. Risk and permission policy

Do locally and deterministically:

- classify concrete operations by action type,
- require approval for writes, deletes, patch application, builds/tests, Git state changes, shell commands, and external agents,
- decide whether always-approve is allowed.

The LLM may recommend an action, but it must not be the final permission authority.

### 4. Diff and patch mechanics

Do locally:

- produce file diffs,
- render diff previews,
- apply only approved diffs,
- record changed files,
- reject unexpected patch drift.

The LLM may propose code text or a patch, but the app should validate and apply it.

### 5. Command wrapping and verification

Do locally:

- run approved commands,
- enforce timeout,
- capture stdout/stderr,
- capture exit code,
- summarize logs before sending to LLM,
- redact obvious secrets where possible.

Send the LLM a bounded command summary, not unlimited raw output.

### 6. Evidence storage

Do locally:

- store run IDs,
- task IDs,
- files inspected,
- approvals,
- patches,
- command results,
- verification status.

The LLM should use evidence records to explain what happened, but the evidence store is an app responsibility.

### 7. Context compaction

Do locally first:

- keep recent run summaries,
- preserve evidence by reference,
- retrieve only relevant memory,
- enforce token budgets.

The LLM may generate a concise summary, but local code decides what is retained and rehydrated.

## Story Mode Offload Targets

### 1. Roleplay input parsing

Do locally:

- plain text -> spoken dialogue,
- `**...**` -> visible action / narration,
- `( ... )` -> inner thought,
- `*...*` -> ordinary italic emphasis.

The LLM should receive a structured version. It should not have to guess the grammar every turn.

### 2. Story message segmentation

Do locally:

- split response into action, speech, plain text, and hidden control segments,
- render action style differently if supported,
- strip hidden tags before display.

Do not rely on the LLM to keep UI control tags invisible.

### 3. Mood and posture tag parsing

Do locally:

- parse `<mood:...>` and `<posture:...>` tags,
- buffer partial tags across streaming chunks,
- ignore unknown tags,
- throttle rapid mood changes.

The LLM may suggest a mood tag. The app owns the actual sprite/expression state.

### 4. Expression state machine

Do locally:

- thinking -> speaking -> final residue -> idle,
- speaking A/B mouth animation,
- final expression hold for 5-10 seconds,
- fallback mapping for missing sprites,
- no flicker.

Do not ask the LLM every few seconds which sprite to show.

### 5. Idle presence scheduler

Do locally:

- track user inactivity,
- enforce cooldown,
- enforce hourly cap,
- prevent idle beats during Advanced mode or active work,
- stop when Story mode is off.

The LLM may generate the short monologue only when the local scheduler decides it is allowed.

### 6. Story memory retrieval

Do locally:

- store compact story facts,
- track recent scene summary,
- track unresolved fictional questions,
- retrieve only relevant facts for the current prompt.

The LLM should not receive the full story transcript every turn.

### 7. Proactive beat templates

Prefer local templates for low-value idle beats.

For example, local templates can produce simple presence lines without an LLM call when appropriate:

```text
**오르키아는 말없이 깜박이는 커서를 바라본다.**

"...아직 여기 있어요."
```

Use the LLM only when the idle beat needs story-specific memory or nuanced character reaction.

## What Should Still Use the LLM

### Advanced mode

Use the LLM for:

- explaining code behavior,
- designing changes,
- reviewing architecture,
- summarizing complex logs,
- writing final user-facing conclusions,
- generating patch proposals when deterministic transforms are insufficient.

### Story mode

Use the LLM for:

- character dialogue,
- emotionally appropriate response,
- interpreting user intent within fiction,
- nuanced memory-based reactions,
- compact scene continuation.

## Local-first Decision Checklist

Before adding an LLM call, ask:

1. Can this be parsed deterministically?
2. Can this be retrieved from local state?
3. Can this be decided by policy?
4. Can this be rendered by UI state?
5. Can this be handled by a template?
6. Can this be summarized locally before asking the LLM?

If yes, do it locally first.

## Token Budget Rules

- Never send entire logs unless explicitly needed.
- Never send entire files when a snippet is enough.
- Never send full story history when a compact memory summary is enough.
- Prefer path + line range + short excerpt.
- Prefer evidence IDs over repeated evidence content.
- Use model calls for semantic compression only after local filtering.

## Implementation Order

### Story mode

1. Local roleplay input parser.
2. Local response segment parser.
3. Local hidden tag stripper.
4. Local expression residue state machine.
5. Local idle scheduler.
6. Local story memory retrieval.
7. LLM-generated idle beat only when needed.

### Advanced mode

1. Local workspace index.
2. Local project instruction loader.
3. Local file search and bounded file read.
4. Local risk policy.
5. Local approval request model.
6. Local diff/patch preview.
7. Local command wrapper.
8. LLM synthesis on top of local evidence.

## Anti-patterns

- Asking the LLM to parse every mood tag.
- Asking the LLM what sprite to show every second.
- Asking the LLM to decide whether a shell command is safe.
- Sending raw build logs without truncation/summarization.
- Sending the whole repository into context.
- Sending the whole story transcript into every Story-mode prompt.
- Using an LLM call for idle beats that could be a local template.

## Design Decision

Ghost should feel intelligent because the character responds well, not because every tiny UI behavior calls a large model.

Spend tokens where judgment is needed. Spend local CPU where structure is enough.
