# Branch note

The branch name is `executor-approval-tests`, but this slice intentionally contains only roleplay formatter tests.

Reason:

- PR #5 already merged the first safety-core tests.
- The executor/approval implementation was not immediately discoverable through the available repository search during this session.
- `RoleplayInputFormatter` was directly inspected and is a stable, high-value regression target.

This branch should be opened with a roleplay-focused PR title.
