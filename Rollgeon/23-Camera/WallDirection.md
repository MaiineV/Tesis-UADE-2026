---
title: WallDirection
type: enum
domain: 23-Camera
status: done
tags: [camera, enum, occlusion]
---

# WallDirection

> Etiqueta autoral por pared en un `RoomPrefab`. El [[CameraService]]
> la cruza contra [[CameraConfigSO]]`.OcclusionMap` para decidir qué
> paredes ocultar según el [[CameraFacing]] actual.

## Shape

```csharp
public enum WallDirection {
    N, NE, E, SE, S, SW, W, NW,
}
```

## Dependencies

**Used by:** [[WallOccluder]] (campo `Direction`), [[CameraConfigSO]]
(`OcclusionMap` values), [[CameraService]] (`RefreshWallOcclusion`),
[[FloorLayoutSO]] (07-Dungeon — orientación de paredes en cada room).

## Code

`Assets/Scripts/Rollgeon/Camera/WallDirection.cs`

## External references

- TECHNICAL.md §17.E.8 — Wall occlusion.
