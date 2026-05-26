---
title: PlayerExecutingSubState
type: substate
domain: 02-Combat
status: done
tags: [combat, fsm, substate, execution, player-turn]
---

# PlayerExecutingSubState

> Runs the pending player action through the [[TurnManager]] (or, as
> fallback, calls `action.Execute` directly) and transitions back to
> [[PlayerIdleSubState]] on completion.

## Overview

Extends `BaseState<PlayerTurnSubContext, PlayerTurnSubInput>`. Entered
either directly from [[PlayerIdleSubState]] (`ActionDirect`) or from
[[PlayerSelectingSubState]] (`SelectionCompleted`). On `Enter`:

1. Reads `PendingAction`, `PendingBehaviorContext`, and
   `SelectionResult` from the shared [[PlayerTurnSubContext]]; aborts to
   Idle (via `ActionExecuted`) if `PendingAction` is null.
2. Lazily creates a `SourceEntity` on the behavior context if missing.
3. If the context is a `HeroBehaviorContext`, propagates the
   `SelectionResult` into it.
4. Branches on `EnergyPrepaid`: calls `TurnManager.TryExecuteEnergyPrepaid`
   when `true` (energy already deducted by the action panel) or
   `TurnManager.TryExecute` otherwise. Falls back to `action.Execute`
   directly when [[TurnManager]] is not registered.
5. Sends `ActionExecuted` to the sub-FSM, returning to
   [[PlayerIdleSubState]].

## API / Shape

- **Input:** [[PlayerTurnSubInput]].
- **Context:** [[PlayerTurnSubContext]].
- **Transitions:**
  - `ActionExecuted` → [[PlayerIdleSubState]].

## Dependencies

- **Uses:** [[PlayerTurnSubContext]], [[PlayerTurnSubInput]],
  [[TurnManager]], [[ActionDefinitionSO]], `HeroBehaviorContext`,
  `Entity`, [[ServiceLocator]].
- **Used by:** [[PlayerTurnState]], [[PlayerIdleSubState]] (incoming
  via `ActionDirect`), [[PlayerSelectingSubState]] (incoming via
  `SelectionCompleted`).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/States/PlayerTurn/PlayerExecutingSubState.cs`
