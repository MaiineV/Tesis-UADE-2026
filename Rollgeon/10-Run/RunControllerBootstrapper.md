---
title: RunControllerBootstrapper
type: class
domain: 10-Run
status: done
tags: [run, bootstrap, monobehaviour]
---

# RunControllerBootstrapper

> `MonoBehaviour` in `00_Bootstrap` (DontDestroyOnLoad) that creates
> the [[RunController]] and registers it as `IRunController` in
> [[ServiceScope]] `Global` via `RunController.CreateAndRegister`.

## Overview

Owns the inspector reference to `Floor1_Layout.asset` (the default
[[FloorLayoutSO]]) and runs at execution order `-9000` — after
`BootstrapRunner` (`-10000`), before any gameplay component. Because
the holder GO is `DontDestroyOnLoad`, `ServiceScope.Global` survives
across `01_MainMenu` and `02_Gameplay` loads.

## Shape

```csharp
[DefaultExecutionOrder(-9000)]
public sealed class RunControllerBootstrapper : MonoBehaviour {
    [SerializeField] private FloorLayoutSO _defaultLayout;

    private void Awake();   // RunController.CreateAndRegister(_defaultLayout)
}
```

## Dependencies

- **Uses:** [[RunController]], [[FloorLayoutSO]], [[ServiceLocator]].
- **Used by:** scene wiring in `00_Bootstrap`.

## Code

`Assets/Scripts/Rollgeon/Run/RunControllerBootstrapper.cs`

## External references

- Setup: `docs/setup/System#0013d_RunController.md`
- TECHNICAL.md: §1.1.3 Run lifecycle — bootstrap
