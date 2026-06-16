---
title: BasePreCondition
type: system
domain: 26-PreConditions
status: done
tags: [preconditions, predicate, base, abstract]
---

# BasePreCondition

> Abstract predicate attached to an [[EffectData]] group. All the
> preconditions in a group must evaluate `true` for the effects to run
> (AND-semantic). Boolean composition is done via a [[PCComposite]]
> subclass.

## Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public abstract class BasePreCondition {
    public abstract string ConditionName { get; }
    public abstract bool Evaluate(PreConditionContext context);

    [SerializeField] protected bool _isConstantValue = true;

    public static bool EvaluateAll(IEnumerable<BasePreCondition> conditions,
                                   PreConditionContext ctx); // AND fold, null-safe
}
```

`_isConstantValue` flags concretes that resolve to a constant literal in
the inspector (vs. consulting runtime state); editor tooling can opt to
fold them at author-time.

## Context

[[PreConditionContext]] carries:

- `OwnerGuid` — entity that owns the effect.
- `OpponentGuid` — currently-targeted counterparty.
- `Entity` — runtime entity snapshot if available.
- Additional fields added as new preconditions require them.

## Composition

The default group semantic is AND (`EvaluateAll`). For OR / NOT trees,
authors drop a single [[PCComposite]] into the group's list and populate
its `Children` with the desired subtree.

## Capability markers

Effects declare what they need from the context via marker interfaces
(`IUsesSelection`, `IUsesValue`, `ICanBeConstantValue`,
`IShouldStoreValuesOnBehavior`). Preconditions work similarly — e.g.
[[PCHasIntAttribute]], [[PCCurrentPhase]], [[PCComboAvailable]].

## Dependencies

- **Uses:** [[PreConditionContext]].
- **Used by:** [[EffectData]], every concrete `PC*` ([[PCComposite]],
  [[PCComboAvailable]], [[PCCurrentPhase]], [[PCEntityInRange]],
  [[PCFirstRollOfCombat]], [[PCHasIntAttribute]], [[PCHasModifier]],
  [[PCAdjacentToDoor]], [[PCHasInventoryItem]]).

## Code

`Assets/Scripts/Rollgeon/PreConditions/BasePreCondition.cs`

## External references

- Setup: `docs/setup/Foundation#0004_EffectsPreConditions.md`
- TECHNICAL.md: §8.2 PreConditions
