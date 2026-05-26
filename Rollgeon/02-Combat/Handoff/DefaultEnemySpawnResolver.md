---
title: DefaultEnemySpawnResolver
type: service
domain: 02-Combat/Handoff
status: done
tags: [combat, handoff, spawn]
---

# DefaultEnemySpawnResolver

> Picks which enemies spawn for a combat encounter by sampling the room's
> [[EnemyPoolSO]] and registering them with the run-scoped
> [[InMemoryEntityRegistry]].

## Shape

```csharp
public sealed class DefaultEnemySpawnResolver : IEnemySpawnResolver {
    public IReadOnlyList<(Guid id, EnemyDataSO data)>
        Resolve(RoomSO room, int spawnCount, System.Random rng);
}
```

## Behaviour

1. Read `room.EnemyPool` → `EnemyPoolSO`.
2. Weighted sample `spawnCount` entries from the pool.
3. For each selected `EnemyDataSO`, mint a fresh `Guid`, duplicate the
   entity's [[ModifiableAttributes]], register with
   [[AttributesManager]] + [[InMemoryEntityRegistry]] + (for bosses)
   [[WeaknessRegistry]].
4. Return `(Guid, EnemyDataSO)` tuples for the handoff layer.

## Dependencies

- **Uses:** [[RoomSO]], [[EnemyPoolSO]], [[EnemyDataSO]],
  [[AttributesManager]], [[InMemoryEntityRegistry]],
  [[WeaknessRegistry]].
- **Used by:** [[CombatHandoffService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Handoff/DefaultEnemySpawnResolver.cs`
- Interface: `.../IEnemySpawnResolver.cs`
- Tests: `.../Tests/DefaultEnemySpawnResolverTests.cs`

## External references

- TECHNICAL.md: §13.3 Enemy spawn resolution
