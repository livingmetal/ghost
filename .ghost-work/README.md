# Ghost Work Evidence

This directory is for lightweight evidence notes used by Codex, Claude Code, future agents, and human maintainers.

Use it to record what was changed and how it was verified.

## Do Commit

- Small evidence templates.
- Generic checklists.
- Non-sensitive verification notes.

## Do Not Commit

- API keys.
- Tokens.
- Private chat transcripts.
- Machine-specific secrets.
- Full local logs that include personal paths, environment variables, or credentials.
- Generated build output.

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

## Completion Standard

A task should not be called complete unless the evidence contains either:

1. Successful verification output, or
2. A clear explanation of why verification could not be run.

Prefer a small verified change over a large unverified change.
