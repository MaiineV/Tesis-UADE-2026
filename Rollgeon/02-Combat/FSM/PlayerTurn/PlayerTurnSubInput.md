---
title: PlayerTurnSubInput
type: enum
domain: 02-Combat
status: done
tags: [combat, fsm, substate, enum, player-turn]
---

# PlayerTurnSubInput

> Input enum for the player-turn sub-FSM owned by [[PlayerTurnState]].
> Drives transitions between [[PlayerIdleSubState]],
> [[PlayerSelectingSubState]], and [[PlayerExecutingSubState]].

## Shape

```csharp
public enum PlayerTurnSubInput {
    None = 0,
    ActionRequiresSelection,  // Idle → Selecting
    ActionDirect,             // Idle → Executing
    SelectionCompleted,       // Selecting → Executing
    ActionExecuted,           // Executing → Idle
}
```

## Transition table

| From | Input | To |
|------|-------|----|
| `Idle` | `ActionRequiresSelection` | `Selecting` |
| `Idle` | `ActionDirect` | `Executing` |
| `Selecting` | `SelectionCompleted` | `Executing` |
| `Executing` | `ActionExecuted` | `Idle` |

## Dependencies

- **Used by:** [[PlayerTurnState]], [[PlayerIdleSubState]],
  [[PlayerSelectingSubState]], [[PlayerExecutingSubState]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/States/PlayerTurn/PlayerTurnSubInput.cs`
