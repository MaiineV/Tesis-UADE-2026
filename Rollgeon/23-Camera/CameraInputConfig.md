---
title: CameraInputConfig
type: class
domain: 23-Camera
status: done
tags: [camera, input, poco]
---

# CameraInputConfig

> POCO ligero que transporta el `InputActionAsset` + nombre del map al
> [[CameraInputRouter]] vía `ServiceLocator`. No es un `ScriptableObject`
> porque no necesita editor-authorship — el [[CameraServiceBootstrap]]
> lo construye de campos serializados.

## Shape

```csharp
public sealed class CameraInputConfig {
    public InputActionAsset Actions { get; }
    public string MapName { get; }
    public CameraInputConfig(InputActionAsset actions, string mapName);
}
```

## Dependencies

**Uses:** `UnityEngine.InputSystem.InputActionAsset`.
**Used by:** [[CameraServiceBootstrap]] (publisher),
[[CameraInputRouter]] (consumer en `OnEnable`).

## Code

`Assets/Scripts/Rollgeon/Camera/CameraServiceBootstrap.cs`

## External references

- TECHNICAL.md §17.E.4 — Input bindings.
