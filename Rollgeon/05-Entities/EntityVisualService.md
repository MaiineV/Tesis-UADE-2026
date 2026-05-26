---
title: EntityVisualService
type: service
domain: 05-Entities
status: done
tags: [entities, visuals, service, run-scope]
---

# EntityVisualService

> Default implementation of [[IEntityVisualService]]. Pure C# class
> wired to the [[IGridManager]] and `IMovementService`; instantiates
> pawn prefabs, mirrors logical movement onto pawn transforms, and
> falls back to colored primitives when prefabs are missing.

## Overview

Subscribes to `IMovementService.OnEntityMoved` in the constructor and
calls `EntityPawn.SnapToGrid(grid, to)` on the matching pawn. The FP
uses teleport — tweens can layer in later by replacing
`EntityPawn.SetWorldPosition`. `Dispose` unsubscribes and despawns
everything (run-scope cleanup).

`SpawnEnemy` distinguishes a boss heuristically (`BaseHP >= 80`) so
the floor-1 boss gets its own prefab if provided; otherwise it falls
back to the regular enemy prefab. Prefabs missing entirely produce
hidden primitives (`HideFlags.DontSave`) so the FP stays bootable
with default content.

After spawning, the service finds a `Rollgeon.Feedback.PawnRegistryBinding`
component on the instantiated GameObject and feeds it the entity's
`Guid` — that wires the pawn into the feedback / `IPawnRegistry` lookups.

## API / Shape

```csharp
public sealed class EntityVisualService : IEntityVisualService, IDisposable {
    public EntityVisualService(
        IGridManager grid,
        IMovementService movement,
        GameObject heroPrefab,
        GameObject enemyPrefab,
        GameObject bossPrefab,
        Transform parent = null);

    // IEntityVisualService
    EntityPawn SpawnHero(Guid, ClassHeroSO, GridCoord);
    EntityPawn SpawnEnemy(Guid, EnemyDataSO, GridCoord);
    void       Despawn(Guid);
    void       DespawnAll();
    bool       TryGetPawn(Guid, out EntityPawn);

    // IEntityPositionResolver
    Vector3?   TryGetWorldPosition(Guid);

    void Dispose();
}
```

## Dependencies

- **Uses:** [[IEntityVisualService]] (implements), [[EntityPawn]],
  [[PawnKind]], [[IGridManager]], `IMovementService`,
  `Rollgeon.Feedback.PawnRegistryBinding`, `ClassHeroSO`,
  `EnemyDataSO`.
- **Used by:** [[IFeedbackService]] (anchor resolution),
  [[IPawnRegistry]] (binding pipeline), [[Entity]] mirror layer,
  `EntityVisualServiceBootstrap`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Visuals/EntityVisualService.cs`
- Bootstrap: `.../EntityVisualServiceBootstrap.cs`
- Tests: `.../Tests/EntityVisualServiceTests.cs`

## External references

- TECHNICAL.md: §17.I Visual entity layer / §13.3 Movement sync
