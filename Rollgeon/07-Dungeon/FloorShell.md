---
title: FloorShell
type: struct
domain: 07-Dungeon
status: done
tags: [dungeon, camera, floor-view]
---

# FloorShell

> Lightweight metadata for one procedural shell — the transparent cube
> that represents a non-active room when the camera is in floor view
> (TECHNICAL §17.E.9).

## Overview

Co-declared in `IDungeonService.cs` and exposed via
[[IDungeonService.GetFloorShells]]. The struct only carries the data
needed to materialise a shell GameObject; the actual cubes are spawned
lazily by [[FloorShellVisibilityController]] the first time floor view
turns on, so EditMode tests that never touch the camera don't pay for
shell GameObjects.

## API / Shape

```csharp
public struct FloorShell {
    public Guid    InstanceId;     // matches RoomInstance.InstanceId
    public Vector3 WorldPosition;  // shell centre in world space
    public Vector3 Size;           // localScale for the cube primitive
}
```

## Dependencies

**Uses:** `System.Guid`, `UnityEngine.Vector3`.

**Used by:** [[IDungeonService]], [[DungeonManager]],
[[FloorShellVisibilityController]].

## Code

`Assets/Scripts/Rollgeon/Dungeon/IDungeonService.cs`

## External references

- TECHNICAL.md: §17.E.9 Floor view shells.
