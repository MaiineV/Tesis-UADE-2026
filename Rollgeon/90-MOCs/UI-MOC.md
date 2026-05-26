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
  │                         ActiveItemsView, ActiveItemSlotView,
  │                         MinimapView, RoomNavigationView,
  │                         ExplorationActionButtonsView)
  ├─ CombatHUDView       (+ TurnQueueView, TurnSlotView,
  │                         ComboIndicatorView, ComboRowView (ComboRow),
  │                         DamageFormulaView, DiceZoneView, DiceSlotView,
  │                         PlayerActionButtonsView, ActionButtonsView,
  │                         EndTurnButtonView, ButtonPhase,
  │                         ChainPhaseIndicatorView, RerollCountView,
  │                         ShieldBarView,
  │                         FloatingDamageSpawner, FloatingDamageInstance,
  │                         FloatingNumberType, ContractDisplayView)
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
- **Screen payloads:** [[BuildSelectionPayload]] ·
  [[ClassSelectionPayload]] · [[CombatHUDPayload]] ·
  [[FloorTransitionPayload]] · [[PoolOfferingRow]]
- **Exploration HUD views:** [[HealthBarView]] · [[EnergyBarView]] ·
  [[GoldCounterView]] · [[ActiveItemsView]] · [[ActiveItemSlotView]] ·
  [[ActiveItemState]] · [[ItemSlotBinding]] ·
  [[ExplorationActionButtonsView]] · [[MinimapView]] ·
  [[RoomNavigationView]]
- **Combat HUD views:** [[TurnQueueView]] · [[TurnSlotView]] ·
  [[ComboIndicatorView]] · [[ComboRow]] · [[ComboRowView]] ·
  [[DamageFormulaView]] · [[DiceZoneView]] · [[DiceSlotView]] ·
  [[PlayerActionButtonsView]] · [[ActionButtonsView]] ·
  [[EndTurnButtonView]] · [[ButtonPhase]] ·
  [[ChainPhaseIndicatorView]] · [[RerollCountView]] ·
  [[ShieldBarView]] · [[FloatingDamageSpawner]] ·
  [[FloatingDamageInstance]] · [[FloatingNumberType]] ·
  [[ContractDisplayView]]

## Cross-domain edges

- Views subscribe to events from [[Attributes-MOC]], [[Combat-MOC]],
  [[Combos-MOC]], [[Dungeon-MOC]], [[Dice-MOC]],
  [[Economy-MOC|Economy]] (gold), [[Items-MOC|Items]] (active item
  slot), [[Shop-MOC|Shop]] (offerings).
- Floating damage / hit pulses are driven by [[Feedback-MOC|Feedback]].
- [[CombatHandoffService]] pushes [[CombatHUDView]]; [[CombatReturnService]]
  replaces with [[VictoryScreen]] / [[DefeatScreen]].
