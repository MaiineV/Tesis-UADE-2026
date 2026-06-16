---
title: WallOccluder
type: class
domain: 23-Camera
status: done
tags: [camera, occlusion, monobehaviour]
---

# WallOccluder

> Marker `MonoBehaviour` que el diseñador pega a cada pared del room
> prefab. Lleva la [[WallDirection]] y la lista de `Renderer`s que
> deben fade-out cuando el [[CameraService]] decida ocultarla.

## Overview

`OnValidate` / `Awake` autopueblan el array `_renderers` via
`GetComponentsInChildren<Renderer>(includeInactive: true)` si está vacío.
`SetHidden(hidden, fadeSeconds)`: con `fadeSeconds <= 0` aplica el alpha
inmediato sobre `sharedMaterial.color`; con duración positiva tweenea
con `Tween.MaterialAlpha` (PrimeTween).

### Authoring via Room Editor

Para salas pintadas con el Room Editor (Tools/Rollgeon/Room Editor), la
asignación de `Direction` está automatizada:

- **Auto al pintar:** cada `TileType.Wall` colocado recibe un
  `WallOccluder` si no lo tenía, y su `Direction` se infiere desde la
  posición de la celda relativa al centro semántico de la sala
  (promedio de celdas `TileType.Floor`).
- **Re-bake:** botón **"Bake Wall Occluders"** en la pestaña *Room* —
  itera todas las paredes y recalcula. Mismo flujo que *Bake NavGraph*.
- **Override manual:** flag `ManualOverride` en el componente. Cuando
  está activo el bake no pisa la `Direction` (útil para pillars
  internos o paredes con orientación corregida a mano).

Implementación: `Rollgeon.Editor.Tools.RoomEditor.WallOccluderOps`.

## API / Shape

```csharp
[AddComponentMenu("Rollgeon/Camera/Wall Occluder")]
public sealed class WallOccluder : MonoBehaviour {
    public WallDirection Direction;
    public bool ManualOverride;            // skip auto-bake desde el Room Editor
    public bool IsHidden { get; }
    public void SetHidden(bool hidden, float fadeSeconds);
}
```

## Dependencies

**Uses:** [[WallDirection]], `PrimeTween`, Odin `[EnumToggleButtons]`.
**Used by:** [[CameraService]] (`RefreshWallOcclusion` lo descubre via
`IDungeonService.GetCurrentRoomOccluders`), [[FloorLayoutSO]] (07-Dungeon
— room prefabs lo incluyen).

## Code

`Assets/Scripts/Rollgeon/Camera/WallOccluder.cs`
Tests: `Assets/Scripts/Rollgeon/Camera/Tests/WallOccluderTests.cs`

## External references

- TECHNICAL.md §17.E.8 — Wall occlusion.
