# Plans Index

This directory contains future proposals, research notes, and staged roadmaps.
It is not the source of truth for current application behavior.

For current rules, read:

- [`../AGENTS.md`](../AGENTS.md)
- [`../docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md)
- [`../docs/ROLEPLAY.md`](../docs/ROLEPLAY.md)
- [`../docs/SPRITE_ARCHITECTURE.md`](../docs/SPRITE_ARCHITECTURE.md)

## Development Workflow

- [`architecture-refactor-20260619.md`](./architecture-refactor-20260619.md)
- [`agent-workflow.md`](./agent-workflow.md)
- [`fablize-discipline.md`](./fablize-discipline.md)

These documents provide optional workflow detail. `AGENTS.md` owns the canonical
completion and safety rules.

## Advanced Workbench and Orchestration

- [`agent-orchestration.md`](./agent-orchestration.md)
- [`advanced-mode-orchestration-research.md`](./advanced-mode-orchestration-research.md)
- [`coding-agent-parity-roadmap.md`](./coding-agent-parity-roadmap.md)
- [`local-resource-offload-policy.md`](./local-resource-offload-policy.md)

These are long-term design documents. They do not authorize automatic command
execution, external-agent startup, patch application, or approval bypass.

## Story and Character Experience

- [`roleplay-input-syntax.md`](./roleplay-input-syntax.md)
- [`structured-narrative.md`](./structured-narrative.md)
- [`story-window.md`](./story-window.md)
- [`story-immersion-nanika-ux.md`](./story-immersion-nanika-ux.md)
- [`sprite-emotion-system.md`](./sprite-emotion-system.md)

Current Story syntax and sprite boundaries are documented under `docs/`. Treat
these plan files as historical rationale or future UX proposals when they differ
from current implementation.

## Plan Maintenance

- Mark completed work instead of silently rewriting history.
- Move stable current behavior into `docs/`.
- Keep speculative designs out of `AGENTS.md` and `README.md`.
- Archive or delete obsolete plans only after confirming that no unique decision
  record would be lost.
