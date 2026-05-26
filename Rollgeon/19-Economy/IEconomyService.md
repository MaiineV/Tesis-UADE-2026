---
title: IEconomyService
type: interface
domain: 19-Economy
status: done
tags: [economy, interface]
---

# IEconomyService

> Minimal contract for run gold — query, add, spend. Emits `OnGoldChanged` on every balance change.

## Overview

Single-currency economy contract for the run. The MVP backs it with [[EconomyService]] (in-memory counter); when the attribute system lands (TECHNICAL.md §1.3) the same contract will adapt over the player's `Gold` attribute without breaking callers.

`Spend` is all-or-nothing — never debits a partial amount. `Add`/`Spend` of `<= 0` are no-ops (the `Spend(<=0)` case returns `true`).

## API

```csharp
public interface IEconomyService {
    int  CurrentGold      { get; }
    void Add(int amount);            // negative/zero = no-op
    bool Spend(int amount);          // all-or-nothing; returns success
    bool CanAfford(int amount);
}
```

`OnGoldChanged` payload: `[int current, int delta]`.

## Dependencies

**Uses:** [[EventManager]], [[EventName]] (`OnGoldChanged`).
**Used by:** [[EconomyService]], [[EconomyBootstrap]], [[EnemyGoldDropService]], [[ShopItemPedestalInteractable]], [[GoldCounterView]], [[RunController]], [[PlayerService]] (future Gold attribute adapter), [[RunContext]].

## Code

`Assets/Scripts/Rollgeon/Economy/IEconomyService.cs`

## External references

- TECHNICAL.md §1.3 (Gold attribute), §17.F (shop spend path).
