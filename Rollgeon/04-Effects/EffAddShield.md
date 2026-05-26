---
title: EffAddShield
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, shield]
---

# EffAddShield

> Concrete [[BaseEffect]] that adds `Shield` stat points to a target
> entity. Source can be a constant or a multiplier of the matched
> combo's `BaseDamage`.

## Overview

Resolves the target through `SelectionResult.FirstSelectedCoord` (grid
lookup) or falls back to `SourceGuid` for self-shields. Writes the
amount under [[BehaviorValueKey]] `FloatingShield` for floating-number
feedback. Returns `false` (cuts the chain — §8.8) only when the target
can't be resolved or `AttributesManager` isn't registered.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class EffAddShield : BaseEffect<ShieldArgs, int>,
    IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior {
    private DamageSource _shieldSource = DamageSource.Constant;
    private int   _baseAmount = 5;
    private float _comboMultiplier = 1f;
}
```

## Dependencies
**Uses:** [[BaseEffect]], [[DamageSource]], `Shield` stat,
`AttributesManager`, [[BehaviorValueKey]], `IGridManager`.
**Used by:** [[EffectData]], shield-buff combos / abilities.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffAddShield.cs`
