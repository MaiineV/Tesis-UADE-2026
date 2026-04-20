---
title: UI-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, ui]
---

# 12-UI — Map of Content

> Screens + HUDs. Screens live in a stack driven by `ScreenManager`;
> every HUD view is event-driven and read-only with respect to gameplay.

## Relationships

```
 ScreenHost (scene)
     │ (canvas parent)
 ScreenManager (Global) — Push / Pop / Replace + OnShow/OnHide
     │
 BaseScreen (abstract)
  ├─ MainMenuScreen → ClassSelectionScreen → BuildSelectionScreen
  ├─ ExplorationHUDView  (+ HealthBarView, EnergyBarView, GoldCounterView,
  │                         ActiveItemsView, MinimapView,
  │                         RoomNavigationView)
  ├─ CombatHUDView       (+ TurnQueueView, EnemyPanelView,
  │                         ComboIndicatorView, DiceZoneView,
  │                         PlayerActionButtonsView, RerollCountView,
  │                         FloatingDamageSpawner, ContractDisplayView)
  ├─ FloorTransitionScreen
  ├─ VictoryScreen / DefeatScreen
  └─ PauseMenuOverlay (pushes PhaseOverlay.Pause)
```

## Notes

- **Core:** [[ScreenManager]] · [[BaseScreen]] · [[ScreenHost]]
- **Screens:** [[MainMenuScreen]] · [[ClassSelectionScreen]] ·
  [[BuildSelectionScreen]] · [[ExplorationHUDView]] ·
  [[CombatHUDView]] · [[FloorTransitionScreen]] · [[VictoryScreen]] ·
  [[DefeatScreen]] · [[PauseMenuOverlay]]
- **Exploration HUD views:** [[HealthBarView]] · [[EnergyBarView]] ·
  [[GoldCounterView]] · [[ActiveItemsView]] · [[MinimapView]] ·
  [[RoomNavigationView]]
- **Combat HUD views:** [[TurnQueueView]] · [[EnemyPanelView]] ·
  [[ComboIndicatorView]] · [[DiceZoneView]] ·
  [[PlayerActionButtonsView]] · [[RerollCountView]] ·
  [[FloatingDamageSpawner]] · [[ContractDisplayView]]

## Cross-domain edges

- Views subscribe to events from [[Attributes-MOC]], [[Combat-MOC]],
  [[Combos-MOC]], [[Dungeon-MOC]], [[Dice-MOC]].
- [[CombatHandoffService]] pushes [[CombatHUDView]]; [[CombatReturnService]]
  replaces with [[VictoryScreen]] / [[DefeatScreen]].
