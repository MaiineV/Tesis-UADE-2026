---
title: SpawnPoint
type: behavior
domain: 07-Dungeon
status: done
tags: [dungeon, spawn, mono-behaviour]
---

# SpawnPoint

> Optional `MonoBehaviour` marker for spawn points not covered by the
> typed lists on a `RoomLayout` (player / enemies / rewards /
> obstacles). TECHNICAL §13.3.

## Overview

The main spawn flow uses authored `Transform`s on `RoomLayout`. This
component is a fallback for custom markers (NPCs, props) where the
designer wants an explicit grid-coord resolution.

## API / Shape

```csharp
public sealed class SpawnPoint : MonoBehaviour {
    public SpawnKind Kind = SpawnKind.Enemy;
    public GridCoord Coord = GridCoord.Zero;
}
```

## Dependencies
**Uses:** [[SpawnKind]], `GridCoord`.

## Code
`Assets/Scripts/Rollgeon/Dungeon/Components/SpawnPoint.cs`
