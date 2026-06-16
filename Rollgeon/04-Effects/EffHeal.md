---
title: EffHeal
type: system
domain: 04-Effects
status: done
tags: [effects, concrete, heal]
---

# EffHeal

> Heal counterpart of [[EffDealDamage]]. Pushes a base amount through
> [[HealPipeline]] and writes a `FloatingHeal` behavior value for the
> feedback layer.

## Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class EffHeal : BaseEffect<HealArgs, int>,
    IUsesSelection, IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior { ... }
```

`HealArgs` mirrors `DamageArgs` — a single `BaseAmount` field for now,
extendable to per-cap rules / over-heal policy later.

## Dependencies

- **Uses:** [[BaseEffect]], [[HealPipeline]], `BehaviorValueKey.FloatingHeal`.
- **Used by:** potion items, support enemy heal behaviors,
  [[ContractSheet]] restorative combos.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/Concretes/EffHeal.cs`
- Args: `.../Concretes/HealArgs.cs`

## External references

- TECHNICAL.md: §8.3 EffHeal
