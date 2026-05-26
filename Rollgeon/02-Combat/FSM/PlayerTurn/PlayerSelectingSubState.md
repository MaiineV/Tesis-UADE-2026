---
title: PlayerSelectingSubState
type: substate
domain: 02-Combat
status: done
tags: [combat, fsm, substate, selection, player-turn]
---

# PlayerSelectingSubState

> Resolves the target selection required by an action's effects before
> execution. Owns the bridge between the FSM and `ISelectionController`.

## Overview

Extends `BaseState<PlayerTurnSubContext, PlayerTurnSubInput>`. Entered
when [[PlayerIdleSubState]] receives `ActionRequiresSelection`. On
`Enter`:

1. Walks the action's effect groups looking for the first effect with
   `RequiresSelectionAt(SelectionTiming.BeforeRoll)`; aborts to
   `Executing` (via `SelectionCompleted`) if none is found.
2. Reads the selection settings (`SlotState`, `IsGlobal`, `Range`,
   `AutoAccept`, `AutoResolve`).
3. Resolves the owner position via [[IGridManager]]. Self-target and
   auto-resolve cases write a `TargetSelectionResult` directly and
   transition immediately.
4. Otherwise, computes valid tiles via `targetSettings.ResolveValidTiles`,
   subscribes to `ISelectionController.OnSelectionCompleted`, and calls
   `BeginSelection` with `HighlightStyle = "move"`.

On `Exit`, unsubscribes from the selection controller.

## API / Shape

- **Input:** [[PlayerTurnSubInput]].
- **Context:** [[PlayerTurnSubContext]].
- **Transitions:**
  - `SelectionCompleted` → [[PlayerExecutingSubState]].
- Stores resolved targets in `Context.SelectionResult`.

## Dependencies

- **Uses:** [[PlayerTurnSubContext]], [[PlayerTurnSubInput]],
  `ISelectionController`, `SelectionRequest`, `SelectionSettings`,
  `TargetSelectionResult`, [[IGridManager]], [[ServiceLocator]].
- **Used by:** [[PlayerTurnState]], [[PlayerIdleSubState]] (incoming),
  [[PlayerExecutingSubState]] (outgoing).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/States/PlayerTurn/PlayerSelectingSubState.cs`
