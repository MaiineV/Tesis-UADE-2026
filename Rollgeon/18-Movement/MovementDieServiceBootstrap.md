---
title: MovementDieServiceBootstrap
type: so
domain: 18-Movement
status: done
tags: [movement, dice, bootstrap, so]
---

# MovementDieServiceBootstrap

> ScriptableObject that registers [[MovementDieService]] as
> [[IMovementDieService]] in `ServiceLocator`.

## Overview
`IPreloadableService` with `Priority = 79` — right after
[[MovementServiceBootstrap]] (78) to keep the Movement group contiguous; it
does not depend on it. Reads `IPlayerService` (Global) to resolve the class
die; warns and falls back to D4 if missing. Scope is `ServiceScope.Run`.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Movement/Movement Die Service Bootstrap")]
public sealed class MovementDieServiceBootstrap : ScriptableObject, IPreloadableService {
    public int  Priority => 79;
    public void Register();
}
```

## Dependencies
**Uses:** [[MovementDieService]], [[IMovementDieService]], `IPlayerService`,
`ServiceLocator`, `IPreloadableService`
**Used by:** Service preload pipeline ([[Bootstrap]]) —
`Assets/Rollgeon/ServiceBootstrap.asset → ExtraServices`.

## Code
`Assets/Scripts/Rollgeon/Movement/Die/MovementDieServiceBootstrap.cs`
Asset: `Assets/Rollgeon/Services/MovementDieServiceBootstrap.asset`

## External references
- TECHNICAL.md: §6.6
