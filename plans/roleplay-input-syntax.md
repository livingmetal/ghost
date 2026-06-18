# Roleplay input syntax

Roleplay mode uses a small text grammar before sending the player's input to the LLM.

## Current grammar

```text
plain text      -> spoken dialogue
**text**        -> visible action or scene narration
(text)          -> inner thought
```

## Rationale

Single asterisks are intentionally not action syntax. A player may type ambiguous text such as `*A*B*`, and that should remain spoken dialogue rather than being partially parsed as action.

Double asterisks make the action/narration layer explicit and reduce accidental parsing.

## Privacy rule

Text inside parentheses is treated as the player's private thought. The character must not quote, answer, acknowledge, or reveal awareness of that thought. If the whole input is only private thought, the character should continue from ambient scene context as if quietly waiting.

## Implementation notes

- `RoleplayInputFormatter` parses `**...**` as action/narration.
- `RoleplayInputFormatter` leaves single-asterisk text as spoken dialogue.
- Tests cover `*A*B*` as spoken dialogue and `**A*B**` as action text containing a literal single asterisk.
