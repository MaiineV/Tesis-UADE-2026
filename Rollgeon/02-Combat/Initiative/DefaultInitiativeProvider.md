---
title: DefaultInitiativeProvider
type: service
domain: 02-Combat/Initiative
status: done
tags: [combat, initiative, turn-order]
---

# DefaultInitiativeProvider

> Rolls initiative per entity by combining the hidden [[Speed]] stat with
> a speed-die via [[IInitiativeRng]]. Used by [[TurnOrderService]] to
> build the round's turn queue.

## API

```csharp
public interface IInitiativeProvider {
    int RollInitiative(Guid entityId);
}

public sealed class DefaultInitiativeProvider : IInitiativeProvider { ... }
```

## Algorithm

1. Read `Speed.ModifiedValue` via [[AttributesManager]].
2. Add a roll from [[IInitiativeRng]] (`DefaultInitiativeRng` in prod,
   `FixedInitiativeRng` in tests).
3. Ties are broken deterministically by GUID ordering in
   `InitiativeFallbacks.DescByInitiativeThenByGuid`.

## Dependencies

- **Uses:** [[AttributesManager]], [[Speed]], [[IInitiativeRng]],
  [[TurnOrderConfig]].
- **Used by:** [[TurnOrderService]]`.BuildForCombat`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Initiative/DefaultInitiativeProvider.cs`
- Interface: `.../IInitiativeProvider.cs`
- Fallbacks: `.../InitiativeFallbacks.cs`
- RNG: `../Random/DefaultInitiativeRng.cs`, `IInitiativeRng.cs`

## External references

- Setup: `docs/setup/System#0100c_TurnOrderHiddenSpeed.md`
- TECHNICAL.md: §12.7 Initiative
