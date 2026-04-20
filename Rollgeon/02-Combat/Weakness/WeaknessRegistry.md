---
title: WeaknessRegistry
type: service
domain: 02-Combat/Weakness
status: done
tags: [combat, weakness, registry]
---

# WeaknessRegistry

> In-memory registry of each entity's weakness (`comboId`, `multOverride`).
> Populated at enemy spawn time; read by [[WeaknessChecker]].

## Shape

```csharp
public sealed class WeaknessRegistry : IWeaknessRegistry {
    public void SetWeakness(Guid entityId, string comboId, float multiplierOverride);
    public bool TryGet(Guid entityId, out (string comboId, float mult) data);
    public void Unregister(Guid entityId);
}
```

## Lifecycle

- Registered globally by `WeaknessServiceBootstrap` ([[IPreloadableService]]).
- Entities set their own entry in their spawn handler (read from
  `EnemyDataSO.Weakness`).
- Entries are cleared on `Unregister` when the entity dies.

## Dependencies

- **Uses:** `IWeaknessRegistry` interface.
- **Used by:** [[WeaknessChecker]], enemy spawn pipeline, Auditor /
  boss spawn helpers.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Weakness/WeaknessRegistry.cs`
- Interface: `.../IWeaknessRegistry.cs`
- Bootstrap: `.../WeaknessServiceBootstrap.cs`

## External references

- Setup: `docs/setup/Content#0097b_WarriorContractAndWeakness.md`
- TECHNICAL.md: §7.2 Weakness registry
