---
title: CameraInputRouter
type: class
domain: 23-Camera
status: done
tags: [camera, input, monobehaviour]
---

# CameraInputRouter

> `MonoBehaviour` que lee el action map `Camera` del `InputActionAsset`
> del proyecto y delega los inputs (rotate drag, pan drag, zoom, recenter)
> al [[CameraService]] adjunto.

## Overview

Tres formas de proveer el `InputActionAsset`, por orden de precedencia:
`Configure(asset, mapName)` antes de `OnEnable`; campo serializado del
componente; `CameraInputConfig` resuelto desde `ServiceLocator` (lo
publica el [[CameraServiceBootstrap]]). Sin ninguno, el router queda
inerte y la cámara sólo responde a comandos directos al [[ICameraService]].
Convierte el modifier-held + drag delta en `AccumulateRotationDrag` /
`PanBy`; el scroll de zoom se signa para `ZoomBy(±1)`.

## API / Shape

```csharp
[RequireComponent(typeof(CameraService))]
public sealed class CameraInputRouter : MonoBehaviour {
    public void Configure(InputActionAsset actions, string mapName);
}
```

## Action map esperado

`Camera` map con: `RotateModifier`, `RotateDrag`, `PanModifier`,
`PanDrag`, `Zoom`, `Recenter`. Cada lookup tolera ausencia (no throw).

## Dependencies

**Uses:** [[CameraService]], `CameraInputConfig` (de [[CameraServiceBootstrap]]),
`UnityEngine.InputSystem`, `Patterns.ServiceLocator`.
**Used by:** Componente colocado en el rig de `Main Camera` por el
diseñador.

## Code

`Assets/Scripts/Rollgeon/Camera/CameraInputRouter.cs`

## External references

- TECHNICAL.md §17.E.4 — Input bindings.
