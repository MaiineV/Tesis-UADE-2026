---
title: PreConditions-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, preconditions]
---

# 26-PreConditions — Map of Content

> Polymorphic predicate library that gates [[EffectData]] groups.
> Default group semantic is AND (`BasePreCondition.EvaluateAll`); OR / NOT
> trees are built by dropping a single [[PCComposite]] into the list.

## Relationships

```
 EffectData (04-Effects)
   ├─ PreConditions: List<BasePreCondition>   (AND fold)
   └─ Effects:       List<IEffect>

 BasePreCondition (abstract)
   ├─ Evaluate(PreConditionContext) : bool
   ├─ _isConstantValue (editor fold flag)
   └─ static EvaluateAll(IEnumerable<BasePreCondition>, ctx)

 PreConditionContext
   ├─ OwnerGuid · OpponentGuid · Entity · ...

 PCComposite(Mode = And | Or | Not, Children: List<BasePreCondition>)
       ├─ Or  / empty → false
       ├─ And / empty → true
       └─ Not = NAND of children

 Concretes: PCComboAvailable · PCCurrentPhase · PCEntityInRange ·
            PCFirstRollOfCombat · PCHasIntAttribute · PCHasModifier ·
            PCAdjacentToDoor · PCHasInventoryItem
```

## Pages

### Core
- [[BasePreCondition]] — abstract predicate base
- [[PreConditionContext]] — runtime context bag
- [[PCComposite]] · [[CompositeMode]] — boolean composition
- [[IntComparison]] · [[DistanceMetric]] — shared enums

### Concrete predicates
- [[PCComboAvailable]] · [[PCCurrentPhase]]
- [[PCEntityInRange]] · [[PCFirstRollOfCombat]]
- [[PCHasIntAttribute]] · [[PCHasModifier]]
- [[PCAdjacentToDoor]] · [[PCHasInventoryItem]]

## Cross-domain edges

- **Incoming** (consumers):
  - 04-Effects: every [[EffectData]] group runs `EvaluateAll` on its
    `PreConditions` list before firing effects.
  - 02-Combat / 05-Entities: action / combo / boss / support behaviors
    compose [[BasePreCondition]] lists for guarded triggers.
  - 25-Exploration: [[ExplorationBehaviorService]] runs `ShowConditions`
    against a [[PreConditionContext]] before spending energy.
- **Outgoing** (dependencies, by predicate):
  - 00-Foundations: [[ServiceLocator]], [[EventManager]], [[EventName]].
  - 02-Combat / Combos: [[PCComboAvailable]], [[PCFirstRollOfCombat]].
  - 06-Run / Phase: [[PCCurrentPhase]] reads [[GamePhase]] /
    [[IPhaseService]].
  - 17-Grid: [[PCEntityInRange]], [[PCAdjacentToDoor]] read
    [[IGridManager]] occupancy + adjacency.
  - 03-Attributes: [[PCHasIntAttribute]] reads attribute snapshots.
  - Modifiers: [[PCHasModifier]] queries modifier stacks.
  - 24-Items: [[PCHasInventoryItem]] queries [[IInventoryService]].
