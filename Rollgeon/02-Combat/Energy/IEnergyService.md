---
title: IEnergyService
type: interface
domain: 02-Combat
status: done
tags: [combat, energy, interface]
---

# IEnergyService

> Public contract for the energy service — initialize, spend, regen at
> turn end, and read current/max. Path canonical para restar energía;
> los callers no deben tocar `AttributesManager.Modify<Energy>` directo.

## Overview

Consumed by [[TurnManager]] (charges energy via `SpendEnergy`),
[[CombatTurnFSM]] (turn-end regen), and HUD views (read current/max,
subscribe to `OnEnergyChanged` / `OnPlayerEnergyChanged`).

## API / Shape

```csharp
public interface IEnergyService {
    void InitializeForEntity(Guid entityId);
    bool SpendEnergy(Guid entityId, int cost);
    void RegenerateAtTurnEnd(Guid entityId);
    int  GetCurrent(Guid entityId);
    int  GetMax(Guid entityId);
}
```

## Dependencies
**Used by:** [[TurnManager]], [[EnergyService]], [[EnergyBarView]].

## Code
`Assets/Scripts/Rollgeon/Combat/Energy/IEnergyService.cs`
