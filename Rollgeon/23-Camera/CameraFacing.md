---
title: CameraFacing
type: enum
domain: 23-Camera
status: done
tags: [camera, enum]
---

# CameraFacing

> Yaw discreto de la cámara en pasos de 45°. Valor entero = grados.

## Shape

```csharp
public enum CameraFacing {
    N  = 0,
    NE = 45,
    E  = 90,
    SE = 135,
    S  = 180,
    SW = 225,
    W  = 270,
    NW = 315,
}
```

## Notas

[[CameraService]] usa `(int)facing % 90 != 0` para detectar diagonales
y aplicar `CameraConfigSO.DiagonalYawOffset` solo a NE/SE/SW/NW; los
cardinales quedan en múltiplos exactos de 90°.

## Dependencies

**Used by:** [[ICameraService]] (`CurrentFacing`, `FacingChanged`),
[[CameraService]], [[CameraConfigSO]] (`StartingFacing`, `OcclusionMap`).

## Code

`Assets/Scripts/Rollgeon/Camera/ICameraService.cs`

## External references

- TECHNICAL.md §17.E.2 — Discrete yaw.
