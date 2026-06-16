---
title: IWeaknessRegistry
type: interface
domain: 02-Combat
status: done
tags: [combat, weakness, registry, interface]
---

# IWeaknessRegistry

> Runtime registry of each spawned entity's weakness (combo id +
> optional multiplier override). Populated by the enemy spawn pipeline
> from `EnemyDataSO.WeaknessComboId`.

## Overview

Lookup abstraction so [[IWeaknessChecker]] doesn't know about the
entity pipeline. Tests can plug an in-memory registry. A
`multiplierOverride` of `0` means "use the default from
[[RulesetSO]]"; any positive value overrides per-entity.

## API / Shape

```csharp
public interface IWeaknessRegistry {
    void SetWeakness(Guid entityId, string comboId, float multiplierOverride);
    bool TryGet(Guid entityId, out (string comboId, float mult) data);
    void Unregister(Guid entityId);
}
```

## Dependencies
**Used by:** [[IWeaknessChecker]], enemy spawn pipeline.
**Implemented by:** [[WeaknessRegistry]].

## Code
`Assets/Scripts/Rollgeon/Combat/Weakness/IWeaknessRegistry.cs`
