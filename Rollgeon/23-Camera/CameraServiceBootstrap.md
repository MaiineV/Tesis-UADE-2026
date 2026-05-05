---
title: CameraServiceBootstrap
type: bootstrap
domain: 23-Camera
status: done
tags: [camera, bootstrap, so]
---

# CameraServiceBootstrap

> `IPreloadableService` opcional que registra el [[CameraConfigSO]] en
> `ServiceLocator` (scope `Global`) y, si recibe un `InputActionAsset`,
> publica un `CameraInputConfig` consumible por el [[CameraInputRouter]].

## Overview

Alternativa al drop directo del `CameraConfig.asset` en
`ServiceBootstrapSO.SettingsAssets`. Sólo es necesario si además querés
bootstrap-wirear el `InputActionAsset` que el router debe leer en la
scene de gameplay. Priority 45 — antes de los Run-scope services
(75+) porque el config vive en scope Global.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Camera/Camera Service Bootstrap")]
public sealed class CameraServiceBootstrap : ScriptableObject, IPreloadableService {
    public int Priority => 45;
    public void Register();
}

public sealed class CameraInputConfig {
    public InputActionAsset Actions { get; }
    public string MapName { get; }
    public CameraInputConfig(InputActionAsset actions, string mapName);
}
```

## Dependencies

**Uses:** [[CameraConfigSO]], [[CameraService]] (target del registro
`Run`), `UnityEngine.InputSystem.InputActionAsset`,
`Patterns.ServiceLocator`, `Rollgeon.Patterns.Bootstrap.IPreloadableService`.
**Used by:** `ServiceBootstrapSO.ExtraServices`, [[CameraInputRouter]]
(consume `CameraInputConfig`).

## Code

`Assets/Scripts/Rollgeon/Camera/CameraServiceBootstrap.cs`

## External references

- TECHNICAL.md §17.E — Camera bootstrap.
