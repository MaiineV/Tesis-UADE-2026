---
title: FloorShellVisibilityController
type: service
domain: 07-Dungeon
status: done
tags: [dungeon, camera, floor-view]
---

# FloorShellVisibilityController

> Run-scope toggle that swaps between "current room visible" and "all
> shells visible" when the camera enters / leaves floor view
> (TECHNICAL §17.E.9).

## Overview

Listens on [[EventName|OnCameraFloorViewToggled]] to flip mode and on
[[EventName|OnRoomEntered]] to refresh which shell is "current" (and
therefore hidden) as the player moves between rooms. Reads metadata
from [[IDungeonService.GetFloorShells]]; the player-facing room prefab
is left untouched so combat / exploration keep rendering normally.

Shell GameObjects are materialised lazily the first time floor view is
activated. Each shell is a `Cube` primitive parented under a single
`FloorShells` root. Per-state materials are chosen in `ApplyVisibility`
(not at materialisation, since `Visited` changes as the player advances):
visited rooms get [[CameraConfigSO|ShellVisitedMaterial]] (lighter),
discovered-but-unvisited neighbours get [[CameraConfigSO|ShellAdjacentMaterial]]
(darker) — each falling back to a URP Unlit / Standard material tinted
with the matching `Shell*Color` when no override is supplied. Colliders
are stripped on creation. `Dispose` tears down every spawned shell and
both materials it generated.

Special-room icons (boss/shop sprites floating over the shell) are sized
from [[CameraConfigSO|ShellIconWorldSize]] / `ShellIconHeightOffset` and
carry a [[BillboardToCamera]] component so they keep facing the camera
every frame, even while it rotates (`RotateBy45`).

## API / Shape

```csharp
public sealed class FloorShellVisibilityController : IDisposable {
    public FloorShellVisibilityController(IDungeonService dungeon, CameraConfigSO config);
    public static FloorShellVisibilityController CreateAndRegister();
    public void Dispose();
}
```

`CreateAndRegister` resolves [[IDungeonService]] and [[CameraConfigSO]]
via [[ServiceLocator]] and registers itself in [[ServiceScope]] `Run`.

## Dependencies

**Uses:** [[IDungeonService]], [[FloorShell]], [[CameraConfigSO]],
[[EventManager]], [[EventName]], [[ServiceLocator]].

**Used by:** [[CameraService]] (drives the toggle event),
[[DungeonManager]] (typically materialised alongside it).

## Code

`Assets/Scripts/Rollgeon/Dungeon/FloorShellVisibilityController.cs`

## External references

- TECHNICAL.md: §17.E.9 Floor view shells, §17.E.10 Recenter.
