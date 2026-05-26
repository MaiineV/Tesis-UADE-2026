---
title: BaseBehavior
type: system
domain: 05-Entities
status: done
tags: [entities, behavior, abstract]
---

# BaseBehavior

> Abstract parent of every enemy / entity behavior. Carries a
> [[BehaviorTrigger]], a [[GamePhaseMask]] filter, and the
> `StoredValues` API (§9.3) used by effects to stash feedback data.

## Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public abstract class BaseBehavior {
    public BehaviorTrigger Trigger = BehaviorTrigger.OnTurnStart;
    public GamePhaseMask   AllowedPhases = GamePhaseMask.All;

    public virtual string BehaviorName => GetType().Name;
    public virtual bool CanExecute(BehaviorContext ctx) => true;
    public abstract void Execute(BehaviorContext ctx);

    // StoredValues API (§9.3)
    public void SetBehaviorValue(BehaviorValueKey key, BaseBehaviorStoredValue);
    public bool TryGetBehaviorValues<T>(BehaviorValueKey key, out List<T>);
    public void ClearBehaviorValues();
}
```

## Stored values

Effects like [[EffDealDamage]] / [[EffHeal]] write per-behavior values
(e.g. `FloatingDamage`, `FloatingHeal`) that the feedback layer reads
post-resolve. `ClearBehaviorValues()` is invoked in a `finally` block
by the dispatcher after the effect pipeline runs.

## Dependencies

- **Uses:** [[BehaviorTrigger]], [[GamePhaseMask]], `BehaviorContext`
  (stub), `BehaviorValueKey`, `BaseBehaviorStoredValue`.
- **Used by:** [[EnemyDataSO]]`.Behaviors`, every concrete behavior.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BaseBehavior.cs`

## External references

- Setup: `docs/setup/Content#0099_SupportEnemyAuditor.md`
- TECHNICAL.md: §7.2 BaseBehavior
