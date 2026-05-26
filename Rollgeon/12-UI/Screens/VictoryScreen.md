---
title: VictoryScreen
type: screen
domain: 12-UI/Screens
status: done
tags: [ui, screen, victory]
---

# VictoryScreen

> End-of-run success screen. Shown after the floor-1 boss is defeated
> (Sprint 03 scope).

## Behaviour

- Pushed by [[CombatReturnService]] when
  `CombatOutcome == PlayerWon` and the room's `RoomType == Boss`.
- Displays run summary (hero, floors cleared, run time).
- Offers "Return to main menu" → [[RunBootstrapper]]`.EndRun` + replace
  with [[MainMenuScreen]].

## Dependencies

- **Uses:** [[BaseScreen]], [[ScreenManager]], [[RunBootstrapper]].
- **Used by:** [[CombatReturnService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/VictoryScreen.cs`
- Tests: `.../Tests/VictoryScreenTests.cs`

## External references

- Setup: `docs/setup/UI#0013c_VictoryDefeatScreens.md`
- TECHNICAL.md: §D Victory
