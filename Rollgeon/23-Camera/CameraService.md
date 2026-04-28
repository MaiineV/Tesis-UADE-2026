---
title: CameraService
type: service
domain: 23-Camera
status: done
tags: [camera, service, monobehaviour]
---

# CameraService

> Implementación `MonoBehaviour` de [[ICameraService]] sobre el `Main
> Camera` de `02_Gameplay.unity`. Inicializada por
> [[CameraServiceBootstrap]] con un [[CameraConfigSO]].

## Overview

Mantiene un rig isométrico que sigue al `FollowTarget` con `_panOffset`
acumulativo. PrimeTween cubre rotación (yaw discreto en 45°), zoom
(orto u perspectiva via `IsOrthographic`) y recenter. Pan clamp opcional
contra `IDungeonService.GetFloorBounds`. Wall occlusion: en cada cambio
de `CurrentFacing` cruza el `OcclusionMap` del config con los
[[WallOccluder]]s del room actual y los hace fade.

## Detalles clave

- Pixel snap (`EnablePixelSnap`): proyecta la posición del rig en los
  ejes screen-plane via dot product, redondea al texel del RT y empuja
  el error como `_PixelPanOffset` al shader `SharpUpscale` para
  recuperar movimiento suave.
- `AccumulateRotationDrag`: drag del mouse acumula pixeles; cada
  `DragPixelsPerStep` dispara un `RotateBy45`.
- Shake (§17.E.10 — TODO v8): scaffold con `Tween.ShakeLocalPosition`.
- Eventos: `FacingChanged`, `FloorViewToggled`, además publica
  `EventName.OnCameraFacingChanged`, `OnCameraFloorViewToggled`,
  `OnCameraRecentered` en el `EventManager` global.

## API / Shape

```csharp
public sealed class CameraService : MonoBehaviour, ICameraService {
    public void Initialize(CameraConfigSO config);
    public void AccumulateRotationDrag(float deltaPixels);
    public void ResetRotationDrag();
    // + ICameraService surface
}
```

## Dependencies

**Uses:** [[ICameraService]], [[CameraConfigSO]], [[CameraFacing]],
[[WallDirection]], [[WallOccluder]], `IDungeonService` (07-Dungeon —
floor bounds y current-room occluders), `Patterns.ServiceLocator`,
`Patterns.EventManager`, `PrimeTween`.
**Used by:** [[CameraInputRouter]] (drag/zoom/recenter), [[CameraServiceBootstrap]] (config registration), [[FeedbackManager]] (`Shake`), [[ExplorationController]] (07-Dungeon — `SetFollowTarget`).

## Code

`Assets/Scripts/Rollgeon/Camera/CameraService.cs`

## External references

- TECHNICAL.md §17.E — Camera service.
- Tests: `Assets/Scripts/Rollgeon/Camera/Tests/CameraServiceTests.cs`.
