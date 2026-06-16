---
title: IEnemySpawnResolver
type: interface
domain: 02-Combat
status: done
tags: [combat, handoff, dungeon, interface]
---

# IEnemySpawnResolver

> Resolves enemy spawns for an encounter. Returns
> `(runtime Guid, source EnemyDataSO)` pairs for the participant list,
> registering each spawn into [[RoomInstance]]`.SpawnedEnemies` and
> seeding an `EnemySpawnState` for HP persistence between visits.

## Overview

Prefers the room's `PossibleSetups` (pre-designed sets — picked at
random) and falls back to `EnemyPool` procedural rolls. Determinism is
controlled via the injected `System.Random`.

## API / Shape

```csharp
public interface IEnemySpawnResolver {
    List<(Guid id, EnemyDataSO data)> Resolve(RoomInstance instance, System.Random rng);
}
```

## Dependencies
**Uses:** [[RoomSO]], [[RoomInstance]], [[EnemyPoolSO]],
[[EnemySetupSO]], `EnemyDataSO`.
**Used by:** [[ICombatHandoffService]], dungeon manager.
**Implemented by:** [[DefaultEnemySpawnResolver]].

## Code
`Assets/Scripts/Rollgeon/Combat/Handoff/IEnemySpawnResolver.cs`
