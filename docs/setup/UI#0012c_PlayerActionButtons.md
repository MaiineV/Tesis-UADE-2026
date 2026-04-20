# UI#0012c — PlayerActionButtonsView Setup

## Prerequisites

- **S#0012a** (CombatScreenAndHandoff) merged into `develop`.
- **UI#0095b** (CombatHUD) merged — `CombatHUDView` must already have the
  7 existing sub-views wired.
- **Feature#0104** (EnergyReroll) merged — `IRerollBudgetService` must be
  available for the Reroll button to enable.

## Overview

`PlayerActionButtonsView` is a dice-first sub-view with 4 buttons that
follow the combat flow: **Roll Dice → Reroll / Confirm Attack → End Turn**.
It coexists with the existing `ActionButtonsView` — both can be present
on the CombatHUD prefab simultaneously.

## Prefab setup

1. Open the **CombatHUD** prefab.
2. Create a new child GameObject: `PlayerActionButtonsPanel`.
3. Add the `PlayerActionButtonsView` component to it.
4. Create 4 child `Button` GameObjects:
   - `RollDiceButton`
   - `RerollButton`
   - `ConfirmAttackButton`
   - `EndTurnButton`
5. Create a child `TextMeshPro - Text (UI)` for the reroll label (e.g.
   `RerollLabel`).
6. Wire all 5 references into the `PlayerActionButtonsView` inspector:
   - `_rollDiceButton` → `RollDiceButton`
   - `_rerollButton` → `RerollButton`
   - `_confirmAttackButton` → `ConfirmAttackButton`
   - `_endTurnButton` → `EndTurnButton`
   - `_rerollLabel` → `RerollLabel`

## CombatHUDView wiring

1. Select the root `CombatHUDView` component on the CombatHUD prefab.
2. Drag `PlayerActionButtonsPanel` into the new
   `_playerActionButtons` serialized field.
3. The `CombatHUDView` will automatically:
   - Wire `OnRollDicePressed` → `OnRollDiceRequested` delegate
   - Wire `OnRerollPressed` → `OnEnergyRerollRequested` delegate
   - Wire `OnConfirmAttackPressed` → `OnConfirmAttackRequested` delegate
   - Wire `OnEndTurnPressed` → `OnEndTurnRequested` delegate
   - Call `Bind(playerGuid)` / `Unbind()` alongside other sub-views

## Coexistence with ActionButtonsView

Both `ActionButtonsView` and `PlayerActionButtonsView` can be active at
the same time. They listen to different event combinations:

| Feature | ActionButtonsView | PlayerActionButtonsView |
|---|---|---|
| Attack button | Yes (gated by ActionDef) | No |
| Roll Dice button | No | Yes |
| Reroll button | Yes (energy reroll) | Yes (reroll after roll) |
| Confirm Attack | No | Yes |
| End Turn | Yes | Yes |
| Phase state machine | No (binary isPlayerTurn) | Yes (Idle→WaitingForRoll→Rolled→Resolved) |

If only one panel is desired, leave the other's serialized field as
`null` in `CombatHUDView` — the null-check pattern skips it gracefully.

## CombatController integration

The `CombatController` must set the new delegates on `CombatHUDView`
after pushing the screen:

```csharp
hudView.OnRollDiceRequested = () => { /* trigger roll in FSM */ };
hudView.OnConfirmAttackRequested = () => { /* confirm attack in FSM */ };
```

The `OnEnergyRerollRequested` and `OnEndTurnRequested` delegates are
shared with `ActionButtonsView` — they are already wired.

## Verification

1. Enter Play mode and start a combat encounter.
2. Verify all 4 buttons start **disabled** (Idle phase).
3. When the player's turn starts, **Roll Dice** and **End Turn** should
   enable.
4. Click **Roll Dice** — it should disable; **Confirm Attack** and
   **End Turn** should enable. **Reroll** enables only if
   `IRerollBudgetService` reports availability.
5. Click **Confirm Attack** — all buttons should disable (Resolved phase).
6. When the turn ends, all buttons return to disabled (Idle).
7. Run EditMode tests:
   ```
   Unity → Window → General → Test Runner → EditMode → PlayerActionButtonsViewTests → Run All
   ```
   All 14 tests should pass.
