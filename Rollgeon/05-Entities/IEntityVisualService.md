---
title: IEntityVisualService
type: interface
domain: 05-Entities
status: done
tags: [entities, visuals, service, interface]
---

# IEntityVisualService

> Visual layer contract: spawns / despawns the GameObject pawns of
> entities and keeps them in sync with the logical grid. Run-scope
> service. TECHNICAL.md §17.§I + §13.3.

## Overview

Mirrors the logical entity layer with [[EntityPawn]] instances in the
scene. Also implements `IEntityPositionResolver` so the
`FloatingDamageSpawner` (and any other feedback consumer — see
`[[IFeedbackService]]`) can anchor world-space numbers to the pawn of
each entity. Pawn lookup is keyed by `Guid` to keep visuals decoupled
from the logical [[Entity]] reference.

## API / Shape

```csharp
public interface IEntityVisualService : IEntityPositionResolver {
    EntityPawn SpawnHero(Guid guid, ClassHeroSO hero, GridCoord coord);
    EntityPawn SpawnEnemy(Guid guid, EnemyDataSO data, GridCoord coord);
    void Despawn(Guid guid);
    void DespawnAll();
    bool TryGetPawn(Guid guid, out EntityPawn pawn);
}
```

`IEntityPositionResolver.TryGetWorldPosition(Guid)` returns the
current world position of the pawn (or `null` if not spawned).

## Dependencies

- **Uses:** [[EntityPawn]], `Guid`, `ClassHeroSO`, `EnemyDataSO`,
  `GridCoord`, [[Entity]] (logical mirror).
- **Used by:** [[IFeedbackService]] (resolves anchor world positions),
  [[IPawnRegistry]] (per-pawn binding), HUD tooltips, combat camera.
- **Default impl:** [[EntityVisualService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Visuals/IEntityVisualService.cs`

## External references

- TECHNICAL.md: §17.I Visual entity layer / §13.3 Movement sync
