---
title: CombatEnterState
type: state
domain: 02-Combat/FSM
status: done
tags: [combat, fsm, state]
---

# CombatEnterState

> Entry state of [[CombatTurnFSM]]. Builds the turn order and routes to
> the first actor's turn state (player or enemy).

## Behaviour

- `Enter(None)`:
  1. Calls `Context.TurnOrder.BuildForCombat(Context.CachedParticipants)`.
  2. Fires [[EventName]] `OnCombatStart(playerId, roomInstanceId,
     participants)`.
  3. Routes to [[PlayerTurnState]] or [[EnemyTurnState]] based on
     `TurnOrder.Current == Context.PlayerId`.
- Responds to `CombatInput.StartCombat` and `CombatEnded` only.

## Sibling wiring

Siblings (`Player`, `Enemy`, `ExitRef`) are set by [[CombatTurnFSM]]
after construction (set-siblings pattern — avoids circular deps in ctor).

## Dependencies

- **Uses:** [[CombatContext]], [[TurnOrderService]], [[EventManager]],
  [[EventName]], [[PlayerTurnState]], [[EnemyTurnState]],
  [[CombatExitState]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/States/CombatEnterState.cs`

## External references

- TECHNICAL.md: §12.1 Enter state
