---
title: IMovementService
type: interface
domain: 18-Movement
status: done
tags: [movement, service, interface]
---

# IMovementService

> Run-scoped service contract for pathfinding and movement execution
> over the active grid.

## Overview
Sits one layer above [[IGridManager]] — Grid handles "where things are",
Movement handles "how things travel". The default impl runs BFS over
the 4-neighbourhood and respects walkable + occupancy. Movement
execution updates the grid logically and emits `OnEntityMoved` so the
visual layer can animate the corresponding GameObject without coupling
to gameplay logic.

## API / Shape

```csharp
public interface IMovementService {
    List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false);
    List<GridCoord> FindPath(GridCoord from, GridCoord to);
    bool Move(Guid entity, GridCoord destination);

    event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;
    // (entity, from, to, path)
}
```

## Dependencies
**Uses:** [[GridCoord]], [[IGridManager]]
**Used by:** [[MovementService]] (impl), [[MovementServiceBootstrap]],
`SelectionController`, `EffMove`, `EffApplyImpulse`, AI move nodes
(`AINode_Move`, `TreeDrivenEnemyAI`), `EntityVisualService`,
`EntityPawn`, `FeedbackPositionResolver`, range / preview HUD views.

## Code
`Assets/Scripts/Rollgeon/Movement/IMovementService.cs`

## External references
- TECHNICAL.md: §17.§B Movement service
