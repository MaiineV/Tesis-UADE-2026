---
title: MovementService
type: service
domain: 18-Movement
status: done
tags: [movement, service, pathfinding]
---

# MovementService

> Default [[IMovementService]] — BFS pathfinding over the active
> [[NavGraph]], respecting walkability and occupancy from
> [[IGridManager]].

## Overview
Plain C# service. `GetReachableTiles` runs a bounded BFS that excludes
walls and tiles occupied by other entities, returning every visited
coord up to `range`. `FindPath` is a standard BFS with a `cameFrom`
dictionary for parent reconstruction; the destination is allowed to be
"occupied" (so an entity can be commanded to its own tile or land on a
queued slot). `Move` looks up the entity's current coord, computes the
path, asks the grid manager to commit the move, and fires
`OnEntityMoved` with the full path so animation and feedback systems
can play the trip.

## API / Shape

Implements [[IMovementService]]. Constructor:

```csharp
public MovementService(IGridManager grid);
```

Throws `ArgumentNullException` if `grid` is null. `Move` returns `true`
also when `from == destination` (no-op success).

## Dependencies
**Uses:** [[GridCoord]], [[IGridManager]], [[NavGraph]],
[[IMovementService]]
**Used by:** [[MovementServiceBootstrap]] (instantiation),
service consumers via [[IMovementService]].

## Code
`Assets/Scripts/Rollgeon/Movement/MovementService.cs`

## External references
- TECHNICAL.md: §17.§B Movement service
