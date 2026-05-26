---
title: CombatInput
type: concept
domain: 02-Combat/FSM
status: done
tags: [combat, fsm, enum]
---

# CombatInput

> Enum listing every legal input the [[CombatTurnFSM]] accepts.

## Shape

```csharp
public enum CombatInput {
    None             = 0,  // sentinel; never triggers a transition
    StartCombat      = 1,  // Enter → PlayerTurn | EnemyTurn
    PlayerActionDone = 2,  // self-loop on PlayerTurn
    PlayerEndTurn    = 3,  // PlayerTurn → EnemyTurn
    EnemyDone        = 4,  // EnemyTurn → PlayerTurn
    CombatEnded      = 5,  // any → CombatExit
}
```

## `None` contract

`default(CombatInput) == None`. The FSM passes `default(TInput)` on
`Start` and `Stop`; every state must treat `None` as no-op in
`CheckInput`.

## Dependencies

- **Used by:** [[CombatTurnFSM]], the 4 combat states,
  [[CombatController]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/CombatInput.cs`

## External references

- TECHNICAL.md: §12.1 Combat inputs
