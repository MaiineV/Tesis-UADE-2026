---
title: EconomyService
type: service
domain: 19-Economy
status: done
tags: [economy, service]
---

# EconomyService

> MVP in-memory implementation of [[IEconomyService]] — single integer counter that fires `OnGoldChanged` on construction and on every mutation.

## Overview

Sealed service that holds the current run gold balance. Constructor seeds the balance (clamped to non-negative) and triggers `OnGoldChanged(current, current)` so HUD subscribers can hydrate without an extra call. `Spend` returns `false` and does not mutate when funds are insufficient — callers must branch on the return value.

Will be replaced by an adapter against the player's `Gold` attribute once the attribute system (§1.3) lands. The contract surface stays stable.

## API

```csharp
public sealed class EconomyService : IEconomyService {
    public EconomyService(int startingGold);
    public int  CurrentGold { get; }
    public void Add(int amount);
    public bool Spend(int amount);
    public bool CanAfford(int amount);
}
```

## Dependencies

**Uses:** [[IEconomyService]], [[EventManager]], [[EventName]] (`OnGoldChanged`).
**Used by:** [[EconomyBootstrap]] (constructs and registers), [[EnemyGoldDropService]] (calls `Add`), [[ShopItemPedestalInteractable]] (calls `Spend`), [[GoldCounterView]] (reads `CurrentGold`).

## Code

`Assets/Scripts/Rollgeon/Economy/EconomyService.cs`

## External references

- TECHNICAL.md §17.F (shop spend wiring).
