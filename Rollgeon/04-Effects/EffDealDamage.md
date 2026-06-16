---
title: EffDealDamage
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, damage]
---

# EffDealDamage

> Concrete [[BaseEffect]] that resolves damage via [[IDamagePipeline]]
> and writes the final amount under [[BehaviorValueKey]]
> `FloatingDamage` so a downstream `EffPlayFeedback` can show the
> floating number.

## Overview

Source can be a constant `_baseAmount` or a multiplier of the matched
combo's `BaseDamage` (see [[DamageSource]]). When the source is
`ComboValue` and there is no matched combo (e.g. the combo was blocked
by [[IComboBlockService]]), the effect deals `0` damage — fixing the
boss-immunity bypass where a blocked combo still hit for the constant
fallback.

Resolves multiple targets from `EffectContext.SelectionResult`
(grid-occupied lookup) or falls back to `TargetGuid`.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class EffDealDamage : BaseEffect<DamageArgs, int>,
    IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior {
    private DamageSource _damageSource = DamageSource.Constant;
    private int   _baseAmount = 10;
    private float _comboMultiplier = 1f;
    private AttackKind _attackKind = AttackKind.BasicAttack;
}
```

## Dependencies
**Uses:** [[BaseEffect]], [[DamageSource]], [[AttackKind]],
[[IDamagePipeline]], [[DamageContext]], [[BehaviorValueKey]],
[[ISelectionController]], `IGridManager`.
**Used by:** [[EffectData]] inside attack [[ActionDefinitionSO]] / boss
behavior pipelines, [[BasePreCondition]] gating combos.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffDealDamage.cs`
