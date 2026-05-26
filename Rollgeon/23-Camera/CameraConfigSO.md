---
title: CameraConfigSO
type: so
domain: 23-Camera
status: done
tags: [camera, so, config]
---

# CameraConfigSO

> `SerializedScriptableObject` con todo el tuning de la cámara
> isométrica — placement inicial, rotation/pan/zoom, recenter, wall
> occlusion, pixel snap y floor view. Ningún valor vive hardcoded en
> [[CameraService]].

## Overview

Registrado por [[CameraServiceBootstrap]] (o dropeado directo en
`SettingsAssets`) y consumido por [[CameraService]] al despertar. La
sección Wall Occlusion expone un `Dictionary<CameraFacing, List<WallDirection>>`
default-simétrico (1 pared en cardinales, 2 en diagonales), editable
por el diseñador con Odin. Uso de `OdinSerialize` para persistir el dict.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Camera/Camera Config")]
public class CameraConfigSO : SerializedScriptableObject {
    // Placement (editor-only)
    public float DistanceFromTarget = 12f;
    public float PitchDegrees = 45f;
    public CameraFacing StartingFacing = CameraFacing.NE;
    public float DiagonalYawOffset = 0f;

    // Rotation
    public bool EnableRotation; public float RotationStepDegrees;
    public float DragPixelsPerStep; public float RotationTweenSeconds;
    public Ease RotationEase;

    // Pan
    public bool EnablePan; public float PanSpeed;
    public bool PanClampToFloorBounds; public float PanLerpSeconds;

    // Zoom
    public bool EnableZoom; public float ZoomMin, ZoomMax, ZoomStep;
    public float ZoomTweenSeconds; public Ease ZoomEase;
    public bool IsOrthographic;

    // Recenter
    public bool EnableRecenterInput;
    public float RecenterTweenSeconds; public Ease RecenterEase;

    // Wall occlusion
    public bool EnableWallOcclusion; public float WallFadeSeconds;
    public Dictionary<CameraFacing, List<WallDirection>> OcclusionMap;

    // Pixel snap
    public bool EnablePixelSnap; public int PixelRenderHeight;

    // Floor view
    public bool EnableFloorView; public float FloorViewZoomThreshold;
    public float FloorViewTweenSeconds; public Color ShellColor;
    public Material ShellMaterial;

    public static Dictionary<CameraFacing, List<WallDirection>> DefaultOcclusionMap();
}
```

## Dependencies

**Uses:** [[CameraFacing]], [[WallDirection]], `PrimeTween.Ease`, Odin
`SerializedScriptableObject`.
**Used by:** [[CameraService]], [[CameraServiceBootstrap]].

## Code

`Assets/Scripts/Rollgeon/Camera/CameraConfigSO.cs`
Tests: `Assets/Scripts/Rollgeon/Camera/Tests/CameraConfigSOTests.cs`

## External references

- TECHNICAL.md §17.E.3 — Camera config.
- TECHNICAL.md §17.E.8 — Wall occlusion map.
