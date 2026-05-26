---
title: MovementServiceBootstrap
type: so
domain: 18-Movement
status: done
tags: [movement, bootstrap, so]
---

# MovementServiceBootstrap

> ScriptableObject that registers [[MovementService]] as
> [[IMovementService]] in `ServiceLocator`.

## Overview
`IPreloadableService` with `Priority = 78` — must run after
[[GridManagerBootstrap]] (75) so [[IGridManager]] is resolvable. Logs an
error and aborts if the grid manager is missing instead of constructing
a half-wired service. Scope is `ServiceScope.Run`.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Movement/Movement Service Bootstrap")]
public sealed class MovementServiceBootstrap : ScriptableObject, IPreloadableService {
    public int  Priority => 78;
    public void Register();
}
```

## Dependencies
**Uses:** [[MovementService]], [[IMovementService]], [[IGridManager]],
`ServiceLocator`, `IPreloadableService`
**Used by:** Service preload pipeline ([[Bootstrap]]).

## Code
`Assets/Scripts/Rollgeon/Movement/MovementServiceBootstrap.cs`

## External references
- TECHNICAL.md: §17.§B Movement service
