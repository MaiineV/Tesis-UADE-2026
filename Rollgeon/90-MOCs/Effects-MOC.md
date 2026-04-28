---
title: Effects-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, effects]
---

# 04-Effects — Map of Content

> Pipeline of polymorphic effects + preconditions + selection / target
> resolution. Consumed by actions, combos, behaviors.

## Relationships

```
 EffectData
   ├─ PreConditions: List<BasePreCondition>   (AND semantic)
   └─ Effects:       List<IEffect>            (ordered, short-circuit)

 IEffect ← BaseEffect (sealed Apply) ← generic BaseEffect<TArgs,TValue>
     └─ concretes: EffDealDamage, EffHeal, EffAddShield,
                   EffApplyImpulse, EffPlayFeedback, EffChain,
                   EffMove, EffForceDoor, EffPassDoor,
                   EffAddItemToInventory, EffRemoveInventoryItem

 EffectContext carries state across stages
                  (lastResult, SourceGuid, TargetGuid, SourceBehavior,
                   SelectionResult, …)

 SelectionSettings ─ embedded per effect
     └─ ISelectionController resolves TargetRef[] → TargetSelectionResult
             via SelectionRequest (timing, filter mask, slot state)
```

## Notes

- **Core:** [[IEffect]] · [[BaseEffect]] · [[EffectData]] ·
  [[EffectContext]]
- **Concretes:** [[EffDealDamage]] · [[EffHeal]] · [[EffAddShield]] ·
  [[EffApplyImpulse]] · [[EffPlayFeedback]] · [[EffChain]] ·
  [[EffMove]] · [[EffForceDoor]] · [[EffPassDoor]] ·
  [[EffAddItemToInventory]] · [[EffRemoveInventoryItem]]
- **Args & enums:** [[DamageArgs]] · [[HealArgs]] · [[ShieldArgs]] ·
  [[ChainPhase]] · [[DamageSource]]
- **Selection:** [[SelectionSettings]] · [[ISelectionController]] ·
  [[SelectionRequest]] · [[TargetRef]] · [[TargetSelectionResult]] ·
  [[SelectionTiming]] · [[EntityFilterMask]] · [[SlotState]]
- **Readers:** [[IEntityReader]] · [[IPlayerReader]]
- **Value markers (`ICanBe*`):** [[ICanBeConstantValue]] ·
  [[ICanBeEntityAttribute]] · [[ICanBeEntityValue]] ·
  [[ICanBeGenericValue]] · [[ICanBeTriggeringEntityAttribute]] ·
  [[ICanBeAnimFeedback]] · [[ICanBeSFXFeedback]] · [[ICanBeVFXFeedback]]
- **Capability markers (`IUses*`):** [[IUsesAttribute]] ·
  [[IUsesFeedback]] · [[IUsesFeedbackSequence]] ·
  [[IUsesFeedbackTarget]] · [[IUsesGridSelection]] · [[IUsesValue]]
- **Behavior markers:** [[IHasDuration]] · [[IHasModifierDirection]] ·
  [[IHasOperation]] · [[IRequiresTriggerContext]] ·
  [[IShouldStoreValuesOnBehavior]]

## Cross-domain edges

- [[ActionDefinitionSO]]`.Effect` fires effects during
  [[TurnManager]]`.TryExecute`.
- [[BaseComboSO]]`.ExtraEffects` runs post-match combo-specific effects.
- Boss / support behaviors in [[Entities-MOC]] compose effects too.
- [[EffDealDamage]] calls [[DamagePipeline]]; [[EffHeal]] calls
  [[HealPipeline]] (see [[Combat-MOC]]).
- Preconditions live in [[PreConditions-MOC|PreConditions]]
  ([[BasePreCondition]]).
