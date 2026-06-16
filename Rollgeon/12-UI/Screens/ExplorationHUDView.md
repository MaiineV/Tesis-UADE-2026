---
title: ExplorationHUDView
type: screen
domain: 12-UI/Screens
status: done
tags: [ui, hud, exploration]
---

# ExplorationHUDView

> HUD composite that wraps the player's dungeon navigation view. Active
> whenever [[IPhaseService]]`.CurrentBase == Exploration`.

## Sub-views (wired in the scene hierarchy)

- [[HealthBarView]]
- [[EnergyBarView]]
- [[GoldCounterView]]
- [[ActiveItemsView]]
- [[MinimapView]]
- [[RoomNavigationView]] — arrows / waypoints for entering adjacent
  rooms.

## Behaviour

- `OnShow`: subscribes each sub-view to the relevant event channel.
- `OnHide`: unsubscribes all.
- Phase-gated: pushes itself via [[ExplorationController]]
  `.BeginExploration`.

## Dependencies

- **Uses:** [[BaseScreen]], the 6 sub-views.
- **Used by:** [[ExplorationController]], [[CombatReturnService]]
  (re-push on victory).

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/ExplorationHUDView.cs`
- Tests: `.../Tests/ExplorationHUDViewTests.cs`

## External references

- Setup: `docs/setup/UI#0095a_ExplorationHUD.md`,
  `docs/setup/UI#0011d_ExplorationScreen.md`
- TECHNICAL.md: §D / §13.5 Exploration HUD
