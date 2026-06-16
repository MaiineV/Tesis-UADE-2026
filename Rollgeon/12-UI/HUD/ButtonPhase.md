---
title: ButtonPhase
type: enum
domain: 12-UI/HUD
status: done
tags: [ui, hud, enum, combat]
---

# ButtonPhase

> Phase enum used internally by [[PlayerActionButtonsView]] to gate
> action-button interactability across the combat sub-states (idle →
> waiting-for-action → rolled).

## Shape

```csharp
public enum ButtonPhase {
    Idle,              // not the player's turn
    WaitingForAction,  // player turn started, no roll yet (or post-execute)
    Rolled,            // dice resolved, awaiting Confirm
}
```

## Transitions

- `OnTurnStarted(player)` → `WaitingForAction`.
- `OnTurnFinished(player)` → `Idle`.
- `OnRollResolved(player)` → `Rolled`.
- `OnDiceRolled(player)` → `WaitingForAction` (re-rolls collapse back).

## Dependencies

- **Used by:** [[PlayerActionButtonsView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/PlayerActionButtonsView.cs`
  (declared as nested `public enum`).
