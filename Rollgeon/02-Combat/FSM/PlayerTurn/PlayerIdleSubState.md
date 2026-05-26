---
title: PlayerIdleSubState
type: substate
domain: 02-Combat
status: done
tags: [combat, fsm, substate, player-turn]
---

# PlayerIdleSubState

> Default substate of the player-turn sub-FSM. Waits for an action
> request (`ActionRequiresSelection` or `ActionDirect`). On entry,
> clears any stale pending state from the previous action.

## Overview

Extends `BaseState<PlayerTurnSubContext, PlayerTurnSubInput>`.
[[PlayerTurnState]] calls `_subFSM.Start` on this state when the
player's turn begins, and the [[PlayerExecutingSubState]] returns here
on `ActionExecuted` so the player can chain another action within the
same turn (until `PlayerEndTurn` is sent to the outer FSM).

## API / Shape

- **Input:** [[PlayerTurnSubInput]].
- **Context:** [[PlayerTurnSubContext]].
- **Transitions:**
  - `ActionRequiresSelection` → [[PlayerSelectingSubState]].
  - `ActionDirect` → [[PlayerExecutingSubState]].
- **Enter side-effect:** clears `PendingAction`,
  `PendingBehaviorContext`, `SelectionResult` on the shared context.

## Dependencies

- **Uses:** [[PlayerTurnSubContext]], [[PlayerTurnSubInput]],
  [[PlayerSelectingSubState]], [[PlayerExecutingSubState]].
- **Used by:** [[PlayerTurnState]] (parent FSM owner).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/States/PlayerTurn/PlayerIdleSubState.cs`
