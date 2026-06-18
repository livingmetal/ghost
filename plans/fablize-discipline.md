# Plan: Fablize Discipline for Ghost

## Goal

Apply the working discipline from the `fivetaku/fablize` Claude Code plugin to Ghost
development, without installing the plugin itself.

Core idea behind fablize:

> A harness cannot raise a model's ceiling. It makes the model go all the way to
> its own ceiling — by enforcing verification, completion, and investigation as
> procedure, not as luck.

For Ghost, that means: when working on this repository, a coding agent (Codex,
Claude Code, or future agents) should follow these procedures manually, because
the plugin's hooks are not running here.

This file is the actionable version of the "LazyCodex + Fablize style workflow"
already referenced in [`AGENTS.md`](../AGENTS.md) and
[`agent-workflow.md`](./agent-workflow.md).

## The five disciplines

### 1. Per-task router — pick the matching discipline first

Before starting, classify the request and apply the matching discipline. Use the
smallest matching one; overlap only when the task is genuinely multi-category.

| Signal in the request | Apply |
| --- | --- |
| debug, bug, error, crash, "not working", failing, exception, stack trace | Investigation protocol (2) |
| sprite/mood rendering, UI/XAML, window, bubble, visual-novel panel, anything you can see run | Verification grounding (3) |
| 2+ sequential steps, larger feature, long autonomous work | Multi-story gate (4) |
| any task that produces a code change | Early-stop prevention (5) + the completion gate |

### 2. Investigation protocol (debugging / root cause)

Do not patch the first guess. Follow this order:

1. Reproduce the issue first (build, run, or the exact failing command).
2. Form 3 or more competing hypotheses about the cause.
3. Gather evidence per hypothesis — read the actual code path, logs under
   `%APPDATA%\LivingMetalGhost\Logs`, or crash logs.
4. Trace the full causal chain, not just the symptom.
5. Verify before and after the fix with the same reproduction.
6. Report which hypotheses you rejected and why.

Relevant first files when something breaks after a pull (from `AGENTS.md`):
`MainViewModel.cs`, `ConversationService.cs`, `PromptAssembler.cs`,
`StoryStateStore.cs`.

### 3. Verification grounding (anything that renders or runs)

A static read is not observation. For UI, sprite/mood, mode behavior, or prompt
output:

1. Run it in the real renderer — `dotnet run`, `scripts\verify.ps1`, or a publish
   build on `win-arm64`.
2. Observe the actual output (window, sprite swap, bubble text, mode boundary),
   not what the code "should" do.
3. Fix what the observation reveals.
4. Re-run until the observation matches the goal.

Ghost cannot always be run headless. If you cannot observe the running app, say
so explicitly and downgrade the claim from "verified" to "compiles only".

### 4. Multi-story verification gate (decompose larger work)

For anything beyond a one-line change:

1. Break the request into small, checkable stories.
2. Each story gets concrete completion evidence before moving on.
3. The **final** story is always a verification story — it is not done until you
   have a verify command and its result.

A lightweight manual ledger (mirrors fablize `goals.py`):

```text
G001  <title>  status: pending|in_progress|complete|failed|blocked  evidence: ...
G002  <title>  ...
G00N  Verify    verify-cmd: .\scripts\verify.ps1   verify-result: ...
```

Do not mark a story `complete` without non-empty evidence. Do not mark the final
verify story complete without a real command result.

### 5. Early-stop prevention (no promises without action)

Do not end a turn by *announcing* work you did not do. If you say "I'll implement
X" or "let me update Y", actually make the tool calls in the same turn.

Legitimate stop points that do **not** count as early-stop:

- Asking the user a real question before a risky or ambiguous change.
- Stopping at an approval boundary (see Safety boundary below).
- Explicitly reporting that verification could not be run, with the reason.

## Completion gate

Reuse the gate from [`agent-workflow.md`](./agent-workflow.md). A task is not
complete until you can provide:

- Changed file list.
- Summary of what changed.
- Command used for verification.
- Verification result.
- Manual test notes if UI, sprite, mode, or prompt behavior changed.
- Known remaining risk.

Standard verification:

```powershell
.\scripts\verify.ps1
```

If verification was not possible, say why and do not claim the change is fully
verified.

## Safety boundary (unchanged, overrides everything)

The fablize "finish the work" reflex must never override Ghost's approval model.
Do not, without explicit user approval:

- Add unrestricted shell execution.
- Auto-run Codex or Claude Code against the workspace.
- Auto-apply patches.
- Auto-approve workspace-changing commands.
- Cross the daily / roleplay / advanced mode boundary.

"See it through" means finish the *verification*, not bypass the *approval*.

## What this is not

- Not an install of the fablize plugin. Ghost has no `.fablize/` runtime and no
  hooks; these procedures are followed by hand.
- Not a license to add process to trivial changes. Small verified changes beat
  large unverified ones.
