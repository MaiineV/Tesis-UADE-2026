---
title: DefeatScreen
type: screen
domain: 12-UI/Screens
status: done
tags: [ui, screen, defeat]
---

# DefeatScreen

> End-of-run failure screen. Pushed when [[CombatOutcome]] ==
> `PlayerLost`.

## Behaviour

- Displays a "defeated" banner, run summary, and a "Return to main
  menu" CTA.
- Calls [[RunBootstrapper]]`.EndRun` on dismiss, which triggers
  [[ServiceLocator]]`.ClearScope(Run)` — every run-scoped service
  disposes cleanly before returning to the menu.

## Dependencies

- **Uses:** [[BaseScreen]], [[ScreenManager]], [[RunBootstrapper]].
- **Used by:** [[CombatReturnService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/DefeatScreen.cs`
- Tests: `.../Tests/DefeatScreenTests.cs`

## External references

- Setup: `docs/setup/UI#0013c_VictoryDefeatScreens.md`
- TECHNICAL.md: §D Defeat
