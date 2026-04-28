---
title: PlayerTurnSubContext
type: class
domain: 02-Combat
status: done
tags: [combat, fsm, substate, player-turn]
---

# PlayerTurnSubContext

> Mutable context shared by the three player-turn substates
> ([[PlayerIdleSubState]], [[PlayerSelectingSubState]],
> [[PlayerExecutingSubState]]). Carries the parent [[CombatContext]],
> the acting player's `Guid`, and the pending action / behavior context
> / selection result for the in-flight request.

## Shape

```csharp
public sealed class PlayerTurnSubContext {
    public CombatContext CombatContext;
    public Guid ActingGuid;
    public HeroActionBehavior PendingAction;
    public BehaviorContext PendingBehaviorContext;
    public TargetSelectionResult SelectionResult;
}
```

## Overview

Built once by [[PlayerTurnState]]`.Enter` and reused by the inner
sub-FSM. [[PlayerIdleSubState]] clears `PendingAction`,
`PendingBehaviorContext`, and `SelectionResult` on each entry — those
fields are populated by `PlayerTurnState.RequestAction` and consumed
by [[PlayerSelectingSubState]] / [[PlayerExecutingSubState]].

## Dependencies

- **Uses:** [[CombatContext]], `HeroActionBehavior`, `BehaviorContext`,
  `TargetSelectionResult`.
- **Used by:** [[PlayerTurnState]], [[PlayerIdleSubState]],
  [[PlayerSelectingSubState]], [[PlayerExecutingSubState]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/States/PlayerTurn/PlayerTurnSubContext.cs`
