---
title: CombatOutcome
type: concept
domain: 02-Combat/FSM
status: done
tags: [combat, fsm, enum]
---

# CombatOutcome

> Enum describing how a combat ended. Read by [[CombatExitState]] from
> `CombatContext.PendingOutcome` and forwarded on `OnFinished`.

## Shape

```csharp
public enum CombatOutcome {
    PlayerWon,
    PlayerLost,
    Aborted,
}
```

## Flow

- Set by [[CombatController]]`.NotifyCombatEnded` before dispatching
  `CombatInput.CombatEnded`.
- Read by [[CombatExitState]]`.Enter`.
- Defaults to `Aborted` when the FSM exits with no explicit outcome (see
  [[CombatTurnFSM]]`.OnStateEntered` handler).

## Dependencies

- **Used by:** [[CombatController]], [[CombatExitState]],
  [[CombatTurnFSM]], `CombatReturnService`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/CombatOutcome.cs`

## External references

- TECHNICAL.md: §12.9 Combat end
