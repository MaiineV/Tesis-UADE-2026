# UI#0012c — PlayerActionButtonsView + EndTurnButtonView Setup

## Prerequisites

- **S#0012a** (CombatScreenAndHandoff) merged into `develop`.
- **UI#0095b** (CombatHUD) merged — `CombatHUDView` must already have
  sub-views wired.
- **Feature#0104** (EnergyReroll) merged — `IRerollBudgetService` must be
  available for the reroll button in `RerollCountView`.

## Overview

The combat HUD player controls are split into 3 components:

| Component | Responsibility |
|---|---|
| `PlayerActionButtonsView` | 4 behavior buttons (Movement, Attack, Special, Heal) + Confirm |
| `EndTurnButtonView` | End Turn button with its own event subscriptions |
| `RerollCountView` | Reroll counter label + Extra Roll button (see UI#0012b) |

Each follows the `Bind(playerGuid)` / `Unbind()` lifecycle pattern.

## PlayerActionButtonsView — Prefab setup

1. Open the **CombatHUD** prefab.
2. Create (or reuse) a child GameObject: `PlayerActionButtonsPanel`.
3. Add the `PlayerActionButtonsView` component.
4. Create 5 child `Button` GameObjects:
   - `MovementButton`
   - `AttackButton`
   - `SpecialButton`
   - `HealButton`
   - `ConfirmButton`
5. Wire references in the inspector:
   - `_movementButton` → `MovementButton`
   - `_attackButton` → `AttackButton`
   - `_specialButton` → `SpecialButton`
   - `_healButton` → `HealButton`
   - `_confirmButton` → `ConfirmButton`

### Phase state machine

| Phase | Behavior buttons | Confirm |
|---|---|---|
| `Idle` | disabled | disabled |
| `WaitingForAction` | **enabled** | disabled |
| `Rolled` | disabled | **enabled** |

Transitions:
- `OnTurnStarted(player)` → `WaitingForAction`
- `OnDiceRolled(player)` → `Rolled`
- `OnRollResolved(player)` → `WaitingForAction`
- `OnTurnFinished(player)` → `Idle`

## EndTurnButtonView — Prefab setup

1. In the **CombatHUD** prefab, create a child GameObject: `EndTurnButtonPanel`.
2. Add the `EndTurnButtonView` component.
3. Create a child `Button`: `EndTurnBtn`.
4. Wire `_endTurnButton` → `EndTurnBtn`.

### State

| Event | Button |
|---|---|
| `OnTurnStarted(player)` | **enabled** |
| `OnDiceRolled(player)` | disabled (behavior in progress) |
| `OnRollResolved(player)` | **enabled** |
| `OnTurnFinished(player)` | disabled |

## CombatHUDView wiring

1. Select the root `CombatHUDView` component on the CombatHUD prefab.
2. Drag `PlayerActionButtonsPanel` into `_playerActionButtons`.
3. Drag `EndTurnButtonPanel` into `_endTurnButtonView`.
4. The `CombatHUDView` automatically wires:
   - `PlayerActionButtonsView.OnConfirmPressed` → `OnConfirmRequested` delegate
   - `PlayerActionButtonsView.OnBehaviorSelected` → `OnBehaviorSelected` delegate
   - `EndTurnButtonView.OnEndTurnPressed` → `OnEndTurnRequested` delegate
   - `RerollCountView.OnExtraRollPressed` → `OnEnergyRerollRequested` delegate
5. `BindAll` / `UnbindAll` propagates to all sub-views including the new
   `EndTurnButtonView`.

## RerollCountView — optional cost label

If you want the reroll button to show "Free" / "1E" next to it:

1. Create a child `TextMeshPro - Text (UI)`: `CostLabel`.
2. Wire `_costLabel` → `CostLabel` in `RerollCountView`.
3. Leave `null` to skip cost display.

## Verification

1. Enter Play mode and start a combat encounter.
2. Verify all buttons start **disabled** (Idle phase).
3. When the player's turn starts:
   - 4 behavior buttons enable, Confirm stays disabled.
   - End Turn enables.
4. Select a behavior (e.g. Attack) → dice roll:
   - Behavior buttons disable, Confirm enables.
   - End Turn disables during roll.
   - Reroll counter shows "0/2" (or similar per FreeRollCount).
5. Click reroll → counter updates to "1/2", cost label shows "Free" or "1E".
6. Click Confirm → all buttons reset to WaitingForAction.
   - End Turn re-enables.
   - Counter resets to "-/-".
7. Click End Turn → all buttons return to Idle.
8. Run EditMode tests:
   ```
   Unity → Window → General → Test Runner → EditMode → Run All
   ```
   Expected: `PlayerActionButtonsViewTests` (11), `EndTurnButtonViewTests` (10),
   `RerollCountViewTests` (7), `CombatHUDViewTests` (4) — all pass.
