---
title: EnemyTurnState
type: state
domain: 02-Combat/FSM
status: done
tags: [combat, fsm, state, enemy]
---

# EnemyTurnState

> Enemy-active state of [[CombatTurnFSM]]. Invokes the injected enemy AI
> handler and awaits `EnemyDone`.

## Behaviour

- `Enter(input)`:
  1. Fires [[EventName]] `OnTurnStarted(currentEnemyId)`.
  2. Calls `Context.EnemyActionHandler(currentEnemyId)`. The handler may
     drive the AI synchronously or asynchronously and must eventually
     dispatch `CombatInput.EnemyDone` (`SendInput` is reentrancy-safe via
     the FSM's queue).
- `CheckInput`:
  - `EnemyDone` → `TurnOrder.Advance()`; fires `OnTurnFinished`; routes
    to [[PlayerTurnState]] (or loops on the next enemy if there is one).
  - `CombatEnded` → [[CombatExitState]].

## Dependencies

- **Uses:** [[CombatContext]], [[BasicEnemyAI]] (via
  `EnemyActionHandler`), [[TurnOrderService]], [[EventManager]],
  [[PlayerTurnState]], [[CombatExitState]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/States/EnemyTurnState.cs`

## External references

- TECHNICAL.md: §12.1 Enemy turn
