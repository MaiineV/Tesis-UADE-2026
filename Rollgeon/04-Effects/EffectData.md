---
title: EffectData
type: system
domain: 04-Effects
status: done
tags: [effects, pipeline]
---

# EffectData

> Atomic unit of the effect pipeline: a list of [[BasePreCondition]]s +
> a list of [[IEffect]]s evaluated in order.

## Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class EffectData {
    public string Label = "Effect Group";
    [OdinSerialize, SerializeReference] public List<BasePreCondition> PreConditions = new();
    [OdinSerialize, SerializeReference] public List<IEffect>          Effects       = new();

    public bool CanBeExecuted(PreConditionContext preCtx); // AND-semantic
    public void Execute(EffectContext ctx);                 // short-circuit on ctx.lastResult
    public bool TryExecute(EffectContext ctx, PreConditionContext preCtx);
    public bool ValidateAllSelections(TargetSelectionResult, Guid owner, out string firstError);
}
```

## Polymorphic serialization (§13.6.1)

Both lists use `[OdinSerialize]` + `[SerializeReference]` for double
coverage (Odin + Unity native) of round-trip.

## Execution model

`TryExecute` = `CanBeExecuted` (AND-semantic over preconditions) →
`Execute` (short-circuits on `ctx.lastResult`). The return reflects the
final `lastResult`. Rich boolean composition (OR, NOT, nested) is done
via a `PCComposite` precondition.

## Dependencies

- **Uses:** [[BasePreCondition]], [[IEffect]], [[EffectContext]].
- **Used by:** [[ActionDefinitionSO]], [[BaseComboSO]]`.ExtraEffects`,
  behaviors, items, unlocks, rewards.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/EffectData.cs`
- Tests: `.../Tests/EffectsPipelineTests.cs`

## External references

- Setup: `docs/setup/Foundation#0004_EffectsPreConditions.md`
- TECHNICAL.md: §8.1 EffectData
