---
title: EconomyBootstrap
type: bootstrap
domain: 19-Economy
status: done
tags: [economy, bootstrap, so]
---

# EconomyBootstrap

> Preloadable SO that constructs an [[EconomyService]] with the configured starting gold and registers it as [[IEconomyService]] in `ServiceScope.Global`.

## Overview

Implements `IPreloadableService` with `Priority = 40`. The standalone MVP path until the attribute system (§1.3) replaces it with an attribute-backed adapter — the bootstrap will then construct the adapter instead, with no contract change for callers.

## Serialized fields

- `_startingGold` (`int`, `[MinValue(0)]`) — starting gold for a fresh run. Default `10`.

## API

```csharp
public sealed class EconomyBootstrap : ScriptableObject, IPreloadableService {
    public int  Priority { get; } // 40
    public void Register();        // adds EconomyService as IEconomyService (Global)
}
```

## Dependencies

**Uses:** [[EconomyService]], [[IEconomyService]], [[ServiceLocator]], `IPreloadableService`.
**Used by:** [[Bootstrap]] preload pipeline.

## Code

`Assets/Scripts/Rollgeon/Economy/EconomyBootstrap.cs`

## External references

- TECHNICAL.md §1.3 (Gold attribute migration path).
