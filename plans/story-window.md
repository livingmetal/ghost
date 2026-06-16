# Plan: Dedicated Story (VN) Window

## Goal

Give story mode its own visual-novel surface (sprite stage + dialogue), instead
of sharing the daily ChatWindow. Approach A from the proposal: a dedicated window
that reuses existing pieces. Keep view (WPF XAML) thin and logic in the
ViewModel/parsers so a later Droid/MAUI/Avalonia port stays feasible.

## Decisions

- Approach A: a separate `StoryWindow`, bound to the same `MainViewModel`.
- When the story window opens, hide the desktop sprite window (`MainWindow`) to
  avoid two sprites and the CharacterHost dual-instance question; restore it when
  story mode ends. `Hide()` never closes a window, so the app keeps running.
- Closing the story window (X or its close button) exits story mode, which
  restores the desktop companion.

## Stories

### V1 — Story window shell (this slice)

- `StoryWindow` with: header (title + close), sprite stage (reuse
  `CharacterHost`), message area (reuse the ChatWindow message rendering with
  `InlineMarkup.Roleplay=true`), input row (reuse `InputText` + `SendCommand`).
- `MainWindow` orchestration mirrors the chat/workbench pattern:
  `EnsureStoryWindow` / `PositionStoryWindow` / `SyncStoryWindowVisibility`,
  driven by `IsStoryMode` / `IsAdvancedMode`. Hide `MainWindow` while open.

### V2 — Narration band

- Route segments by kind (action/narration vs dialogue vs thought) into a
  separate narration strip. Needs `InlineMarkupParser` to expose segment kind.

### V3 — Choices + objective HUD

- Objective indicator from `StoryState.Objectives`; choice buttons from the
  structured-narrative signals.

### V4 — Scene background

- Per-scene background image, joined with multi-scene arcs.

## Completion gate

Each slice builds clean (build to a temp output while the app is running). Window
layout, sprite render, and open/hide behavior are runtime-verified by the user
(cannot be observed headless).

## Risks

- Cannot visually verify headless; layout/behavior is user-verified.
- CharacterHost dual instance: mitigated by hiding MainWindow while story is open.
- Some message-template XAML is duplicated from ChatWindow in V1; extract to a
  shared resource later.
