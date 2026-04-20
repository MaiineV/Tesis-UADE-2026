---
title: BasePreCondition
type: system
domain: 04-Effects
status: done
tags: [effects, preconditions, predicate]
---

# BasePreCondition

> Abstract predicate attached to an [[EffectData]] group. All the
> preconditions in a group must evaluate `true` for the effects to run
> (AND-semantic). Boolean composition is done via a `PCComposite`
> subclass.

## Shape

```csharp
public abstract class BasePreCondition {
    public abstract bool Evaluate(PreConditionContext ctx);
    public static bool EvaluateAll(List<BasePreCondition> list,
                                   PreConditionContext ctx); // AND fold
}
```

## Context

`PreConditionContext` carries:

- `OwnerGuid` — entity that owns the effect.
- `OpponentGuid` — currently-targeted counterparty.
- `Entity` — runtime entity snapshot if available.
- Additional fields added as new preconditions require them.

## Capability markers

Effects declare what they need from the context via marker interfaces
(`IUsesSelection`, `IUsesValue`, `ICanBeConstantValue`,
`IShouldStoreValuesOnBehavior`). Preconditions work similarly — e.g.
`PCHasEnergy`, `PCInPhase`, `PCRuleset.AllowsAction`.

## Dependencies

- **Uses:** `PreConditionContext`.
- **Used by:** [[EffectData]].

## Code

- Runtime: (folder under `Assets/Scripts/Rollgeon/...PreConditions/` —
  stubs + concretes live alongside the effect concretes).

## External references

- Setup: `docs/setup/Foundation#0004_EffectsPreConditions.md`
- TECHNICAL.md: §8.4 PreConditions
