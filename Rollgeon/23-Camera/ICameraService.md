---
title: ICameraService
type: interface
domain: 23-Camera
status: done
tags: [camera, service]
---

# ICameraService

> Contrato de la cámara isométrica scripteada. Registrada como `IRun`-scope
> en `ServiceLocator` por [[CameraServiceBootstrap]]; la implementación
> viva ([[CameraService]]) corre como `MonoBehaviour` sobre `Main Camera`.

## Overview

Yaw discreto en pasos de 45° via [[CameraFacing]], pan en plano del
rig, zoom (orto u perspectiva), recenter sobre el follow target, y un
hook de `Shake` para feedback. Los listeners reciben `FacingChanged`
(yaw cambió) y `FloorViewToggled` (zoom cruzó el `FloorViewZoomThreshold`).

## API / Shape

```csharp
public interface ICameraService {
    // State (readonly)
    CameraFacing CurrentFacing { get; }
    float CurrentZoom { get; }
    Transform FollowTarget { get; }
    bool IsPanning { get; }
    bool IsFloorView { get; }

    // Commands
    void RotateBy45(bool clockwise);
    void PanBy(Vector2 screenDelta);
    void ZoomBy(float scrollDelta);
    void RecenterOnPlayer(bool instant = false);
    void SetFollowTarget(Transform target);
    void Shake(float amplitude, float durationSeconds);

    // Events
    event Action<CameraFacing> FacingChanged;
    event Action<bool> FloorViewToggled;
}
```

## Dependencies

**Uses:** [[CameraFacing]].
**Used by:** [[CameraService]] (impl), [[CameraInputRouter]], [[FeedbackManager]] (`Shake`), [[ExplorationController]] (07-Dungeon — follow target).

## Code

`Assets/Scripts/Rollgeon/Camera/ICameraService.cs`

## External references

- TECHNICAL.md §17.E — Camera service.
- TECHNICAL.md §17.E.10 — Shake hook (TODO v8).
