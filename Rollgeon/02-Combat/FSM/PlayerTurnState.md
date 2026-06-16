---
title: PlayerTurnState
type: state
domain: 02-Combat/FSM
status: done
tags: [combat, fsm, state, player]
---

# PlayerTurnState

> Player-active state of [[CombatTurnFSM]]. Awaits player actions and
> self-loops on `PlayerActionDone`; advances turn on `PlayerEndTurn`.

## Behaviour

- `Enter(input)`:
  1. Fires [[EventName]] `OnTurnStarted(playerId)`.
  2. Enables the player action HUD via the screen manager.
- `CheckInput`:
  - `PlayerActionDone` → re-enter self (cheap self-loop that keeps the
    HUD state fresh).
  - `PlayerEndTurn` → `TurnOrder.Advance()`; fires `OnTurnFinished`;
    routes to [[EnemyTurnState]].
  - `CombatEnded` → [[CombatExitState]].
- `Exit` clears the player's action-used set on [[TurnManager]] (via the
  `OnTurnStarted` event that fires from the next state's `Enter`).

## Dependencies

- **Uses:** [[CombatContext]], [[TurnOrderService]], [[TurnManager]],
  [[EventManager]], [[EnemyTurnState]], [[CombatExitState]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/States/PlayerTurnState.cs`

## External references

- TECHNICAL.md: §12.1 Player turn
