---
title: CombatHUDView
type: screen
domain: 12-UI/Screens
status: done
tags: [ui, hud, combat]
---

# CombatHUDView

> HUD composite presented during `GamePhase.Combat`. Pushed by
> [[CombatHandoffService]] with a `CombatHUDPayload(enemyTargetGuid,
> roomInstanceId, encounterDisplayName)`.

## Sub-views

- [[TurnQueueView]]
- [[EnemyPanelView]]
- [[ComboIndicatorView]]
- [[DiceZoneView]]
- [[PlayerActionButtonsView]]
- [[RerollCountView]]
- [[FloatingDamageSpawner]] (overlay)
- [[ContractDisplayView]]

## Event wiring (typical)

- `OnTurnQueueBuilt` → [[TurnQueueView]] rebuilds.
- `OnPlayerEnergyChanged` → [[EnergyBarView]] inside the HUD updates.
- `OnComboMatched` (via [[TypedEvent]]) → [[ComboIndicatorView]]
  flashes.
- `OnDamageResolved` → [[FloatingDamageSpawner]] pops a number.

## Dependencies

- **Uses:** [[BaseScreen]], every sub-view above, [[IPhaseService]].
- **Used by:** [[CombatHandoffService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/CombatHUDView.cs`
- Payload: `.../CombatHUDPayload.cs`
- Tests: `.../Tests/CombatHUDViewTests.cs`

## External references

- Setup: `docs/setup/UI#0095b_CombatHUD.md`,
  `docs/setup/System#0012a_CombatScreenAndHandoff.md`
- TECHNICAL.md: §D / §12 Combat HUD
