---
title: MainMenuScreen
type: screen
domain: 12-UI/Screens
status: done
tags: [ui, screen, menu]
---

# MainMenuScreen

> First interactive screen after [[Bootstrap]]. Buttons: "New Run"
> (→ [[ClassSelectionScreen]]), "Continue" (disabled pending save
> system), "Settings" (stub), "Quit".

## Responsibilities

- Present the game title, build number, and primary flow entry.
- Push [[ClassSelectionScreen]] on "New Run".
- Subscribe to version info updates (build time, commit hash) from
  `BootstrapHooks`.

## Dependencies

- **Uses:** [[ScreenManager]], [[BaseScreen]].
- **Used by:** post-bootstrap flow.

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/MainMenuScreen.cs`

## External references

- Setup: `docs/setup/UI#0102_MainMenu.md`
- TECHNICAL.md: §D Main menu
