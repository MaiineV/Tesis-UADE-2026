---
title: TileClickHandler
type: class
domain: 17-Grid
status: done
tags: [grid, input, monobehaviour]
---

# TileClickHandler

> `MonoBehaviour` that converts mouse / pointer clicks into tile
> selections by raycasting through the active camera and pushing the
> result into `ISelectionController`.

## Overview
Lives on the camera or a UI rig in the scene. Uses Unity's new Input
System (`InputActionAsset`) — looks up a `Click` and `Point` action
from the named action map (default `"UI"`). On click it:

1. Bails if `ISelectionController` is missing or not currently selecting.
2. Bails if [[IGridManager]] is missing.
3. Re-scales screen coords to camera pixel space (so it works with the
   pixel-art `RenderTexture` pipeline).
4. Raycasts against `_tileLayer` and prefers a [[TileMarker]] on the hit
   collider, falling back to `IGridManager.WorldToGrid(hit.point)`.
5. Calls `controller.OnTargetClicked(TargetRef.At(coord))`.

Heavily logged because click resolution is a frequent source of
selection bugs.

## API / Shape

```csharp
[AddComponentMenu("Rollgeon/Grid/Tile Click Handler")]
public sealed class TileClickHandler : MonoBehaviour {
    // serialized: _camera, _tileLayer, _actions, _mapName
}
```

## Dependencies
**Uses:** [[GridCoord]], [[TileMarker]], [[IGridManager]],
`ISelectionController`, `TargetRef`, `CameraInputConfig`,
`ServiceLocator`, Unity Input System.
**Used by:** Scene wiring (camera rig) — no code consumers.

## Code
`Assets/Scripts/Rollgeon/Grid/TileClickHandler.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
