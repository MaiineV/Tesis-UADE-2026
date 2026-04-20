---
title: CombatContext
type: system
domain: 02-Combat/FSM
status: done
tags: [combat, fsm, context]
---

# CombatContext

> Shared context passed to every state of [[CombatTurnFSM]]. Holds
> references to run-scoped services and combat-specific cached state.

## Shape

```csharp
public sealed class CombatContext {
    public TurnOrderService   TurnOrder   { get; }
    public TurnManager        TurnManager { get; }  // may be null in tests
    public IEnergyService     Energy      { get; }
    public Guid               PlayerId    { get; }
    public Guid               RoomInstanceId { get; }
    public Action<Guid>       EnemyActionHandler { get; }
    public CombatOutcome?     PendingOutcome { get; set; }
    public IReadOnlyList<Guid> CachedParticipants { get; set; }
}
```

## Immutability

Services arrive by reference and are not instantiated here. Only
`PendingOutcome` (set by [[CombatController]] before firing
`CombatInput.CombatEnded`) and `CachedParticipants` (set by
`CombatTurnFSM.SetParticipants` before `Start`) mutate.

## Dependencies

- **Uses:** [[TurnOrderService]], [[TurnManager]], [[EnergyService]]
  (as `IEnergyService`), [[CombatOutcome]].
- **Used by:** all four combat states, [[CombatTurnFSM]],
  [[CombatController]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/CombatContext.cs`

## External references

- TECHNICAL.md: §12.1 Combat context
