---
title: GridManagerBootstrap
type: so
domain: 17-Grid
status: done
tags: [grid, bootstrap, so]
---

# GridManagerBootstrap

> ScriptableObject that registers the run-scoped [[GridManager]] as
> [[IGridManager]] in `ServiceLocator`.

## Overview
`IPreloadableService` with `Priority = 75` — runs after dice/reroll
services (70-72) and before [[MovementService]] (78) and AI (80).
Idempotent: keeps the same `GridManager` instance across calls so the
service identity is stable for a run. Scope is `ServiceScope.Run` so the
grid and its occupancy clear when the run ends.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Grid/Grid Manager Bootstrap")]
public sealed class GridManagerBootstrap : ScriptableObject, IPreloadableService {
    public int  Priority => 75;
    public void Register();
}
```

## Dependencies
**Uses:** [[GridManager]], [[IGridManager]], `ServiceLocator`,
`IPreloadableService`
**Used by:** Service preload pipeline ([[Bootstrap]] / `GameplayBootstrapper`).

## Code
`Assets/Scripts/Rollgeon/Grid/GridManagerBootstrap.cs`

## External references
- TECHNICAL.md: §17.§I Grid coordinates
- Setup: `docs/setup/...` Grid bootstrap
