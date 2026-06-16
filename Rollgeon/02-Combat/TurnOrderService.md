---
title: TurnOrderService
type: service
domain: 02-Combat
status: done
tags: [combat, turn-order]
---

# TurnOrderService

> Builds and maintains the per-round turn queue. Role-agnostic — it
> deals in `Guid`s only; [[CombatTurnFSM]] decides which state to enter
> based on whether the current slot is the player or an enemy.

## API

```csharp
public sealed class TurnOrderService {
    public IReadOnlyList<Guid> OrderForRound { get; }
    public Guid Current     { get; } // throws if empty
    public int  RoundIndex  { get; }
    public int  ParticipantCount { get; }

    public void BuildForCombat(IEnumerable<Guid> participants);
    public Guid Advance();   // circular; increments RoundIndex on wrap
    public void Reset();
}
```

## Build and advance

- `BuildForCombat` asks [[IInitiativeProvider]] for each participant's
  initiative, sorts descending with deterministic GUID tiebreak
  (`InitiativeFallbacks`), and fires
  [[EventName]] `OnTurnQueueBuilt(snapshot, roundIndex)`.
- `Advance` wraps the cursor; on wrap-around it increments
  `RoundIndex` and re-fires `OnTurnQueueBuilt`.

## Snapshot safety

`OnTurnQueueBuilt` publishes a `ReadOnlyCollection<Guid>` built on a
copy of the live list — listeners cannot mutate the service's state.

## Dependencies

- **Uses:** [[DefaultInitiativeProvider]] (via `IInitiativeProvider`),
  `InitiativeFallbacks`, [[ServiceLocator]], [[EventManager]],
  [[EventName]].
- **Used by:** [[CombatContext]], [[CombatEnterState]],
  [[PlayerTurnState]], [[EnemyTurnState]], turn queue HUD view.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/TurnOrderService.cs`
- Bootstrap: `.../TurnOrderServiceBootstrap.cs`
- Tests: `.../Tests/TurnOrderServiceTests.cs`

## External references

- Setup: `docs/setup/System#0100c_TurnOrderHiddenSpeed.md`
- TECHNICAL.md: §12.7 Turn order
