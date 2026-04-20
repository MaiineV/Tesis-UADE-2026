---
title: EffDamage
type: system
domain: 04-Effects
status: done
tags: [effects, concrete, damage]
---

# EffDamage

> Example concrete [[BaseEffect]]`<DamageArgs, int>` that pushes a
> base-damage amount through [[DamagePipeline]].

## Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class EffDamage :
    BaseEffect<DamageArgs, int>,
    IUsesSelection, IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior {

    [SerializeField, MinValue(0), MaxValue(999)] int _baseAmount = 10;

    protected override DamageArgs ResolveArgs(EffectContext c) => new() { BaseAmount = _baseAmount };
    protected override int ResolveValue(EffectContext c)       => _baseAmount;

    public override bool ApplyEffect(EffectContext c) {
        // 1. Resolve target (SelectionResult.FirstSelectedGuid or ctx.TargetGuid)
        // 2. If IDamagePipeline registered → pipeline.Resolve(ctx)
        //    else → DamagePipelineStub.Apply (foundation-era fallback)
        // 3. Write BehaviorValueKey.FloatingDamage on SourceBehavior for feedback.
    }
}
```

## Dependencies

- **Uses:** [[BaseEffect]], [[DamagePipeline]] (as `IDamagePipeline`),
  [[DamageContext]], [[AttackKind]], `BehaviorValueKey.FloatingDamage`.
- **Used by:** [[ActionDefinitionSO]], combo `ExtraEffects`, boss
  attack behaviors, test fixtures.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/Concretes/EffDamage.cs`
- Args: `.../Concretes/DamageArgs.cs`

## External references

- TECHNICAL.md: §8.3 EffDamage (example)
