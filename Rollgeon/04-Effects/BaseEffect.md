---
title: BaseEffect
type: system
domain: 04-Effects
status: done
tags: [effects, base, abstract]
---

# BaseEffect

> Abstract base for every effect. Holds [[SelectionSettings]], the
> sealed `Apply` pipeline step, and the virtual `ApplyEffect` hook
> concretes override.

## Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public abstract class BaseEffect : IEffect {
    public SelectionSettings Selection = new();

    public virtual string GetEffectName() => GetType().Name;
    public bool HasSelectionRequirement();
    public bool RequiresSelectionAt(SelectionTiming timing);

    public bool Apply(EffectContext context);          // sealed — handles §8.8 short-circuit
    public abstract bool ApplyEffect(EffectContext c); // concretes implement

    public virtual bool ValidateSelection(TargetSelectionResult, Guid owner, out string error);
}
```

A generic variant `BaseEffect<TArgs, TValue>` adds `ResolveArgs(ctx)` /
`ResolveValue(ctx)` for effects that want typed args and a stored value
written to the source behavior ([[BehaviorValueKey]]).

## Short-circuit contract (§8.8)

The sealed `Apply` returns immediately when
`ctx.lastResult == false`, then delegates to `ApplyEffect` and writes
its return to `ctx.lastResult`. Subsequent effects in an
[[EffectData]] chain are therefore skipped.

## Dependencies

- **Uses:** [[IEffect]], [[EffectContext]], [[SelectionSettings]],
  [[TargetSelectionResult]].
- **Used by:** every concrete effect ([[EffDamage]], [[EffHeal]], …),
  [[EffectData]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/BaseEffect.cs`,
  `BaseEffect.Generic.cs`

## External references

- Setup: `docs/setup/Foundation#0004_EffectsPreConditions.md`
- TECHNICAL.md: §8.3 / §8.8 BaseEffect
