---
title: GridManager
type: service
domain: 17-Grid
status: done
tags: [grid, service]
---

# GridManager

> Default in-memory implementation of [[IGridManager]] — owns occupancy
> dictionaries and translates between grid and world coordinates.

## Overview
Plain C# class (no `MonoBehaviour`), instantiated by
[[GridManagerBootstrap]]. Walkability is delegated to the active
[[NavGraph]] (`graph.HasNode`); occupancy is tracked locally with two
mirrored dictionaries (`Guid → GridCoord` and `GridCoord → Guid`) so
both lookup directions stay O(1). `LoadRoom` clears the previous
occupancy and resets the origin / tile size used by world conversion.

## API / Shape

Implements every member of [[IGridManager]]. Notable behaviour:

- `Move` refuses if the destination is non-walkable or occupied by a
  different entity, and routes through `Register` to keep both maps in
  sync.
- `Register` warns and overwrites if the target tile is already taken.
- `WorldToGrid` rounds local-space `(x, z)` divided by `TileSize`.
- `GridToWorld` returns `GridOrigin + (x*TileSize, 0, y*TileSize)`.

## Dependencies
**Uses:** [[GridCoord]], [[NavGraph]], [[IGridManager]]
**Used by:** [[GridManagerBootstrap]] (instantiation),
service consumers via [[IGridManager]].

## Code
`Assets/Scripts/Rollgeon/Grid/GridManager.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
