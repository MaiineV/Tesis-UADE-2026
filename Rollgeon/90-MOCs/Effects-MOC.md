---
title: Effects-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, effects]
---

# 04-Effects — Map of Content

> Pipeline of polymorphic effects + preconditions + selection / target
> query. Consumed by actions, combos, behaviors.

## Relationships

```
 EffectData
   ├─ PreConditions: List<BasePreCondition>   (AND semantic)
   └─ Effects:       List<IEffect>            (ordered, short-circuit)

 IEffect ← BaseEffect (sealed Apply) ← generic BaseEffect<TArgs,TValue>
     └─ concretes: EffDamage, EffHeal, …

 EffectContext carries state across stages
                  (lastResult, SourceGuid, TargetGuid, SourceBehavior,
                   SelectionResult, …)

 SelectionSettings ─ embedded per effect
     └─ DefaultQuery : BaseTargetQuery
             └─ TQ_AllEnemies, TQ_Self, …
```

## Notes

- **Core:** [[IEffect]] · [[BaseEffect]] · [[EffectData]] ·
  [[EffectContext]]
- **Concretes:** [[EffDamage]] · [[EffHeal]]
- **Target queries:** [[BaseTargetQuery]] · [[TargetQueries]] ·
  [[SelectionSettings]]
- **Conditions:** [[BasePreCondition]]

## Cross-domain edges

- [[ActionDefinitionSO]]`.Effect` fires effects during
  [[TurnManager]]`.TryExecute`.
- [[BaseComboSO]]`.ExtraEffects` runs post-match combo-specific effects.
- Boss / support behaviors in [[Entities-MOC]] compose effects too.
- [[EffDamage]] calls [[DamagePipeline]]; [[EffHeal]] calls
  [[HealPipeline]] (see [[Combat-MOC]]).
