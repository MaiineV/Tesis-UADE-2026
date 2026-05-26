---
title: AIContext
type: class
domain: 02-Combat/AI
status: done
tags: [combat, ai, context]
---

# AIContext

> Per-turn context object passed to every [[AIDecisionNode]]`.Tick`
> call. Carries the self/player guids, resolved services, round index,
> and an injectable RNG (TECHNICAL.md §7.5).

## Shape

```csharp
public sealed class AIContext {
    public Guid SelfGuid;
    public Guid PlayerGuid;
    public int  SelfMaxHp;     // captured at spawn for AICond_HPBelow

    public AttributesManager Attributes;
    public IDamagePipeline   DamagePipeline;
    public IGridManager      Grid;
    public IMovementService  Movement;
    public IPlayerService    PlayerService;

    public int          RoundIndex;
    public System.Random Rng;   // tests inject seeded RNG
}
```

## Behaviour

Built fresh in [[TreeDrivenEnemyAI]]`.HandleEnemyTurn` once per enemy
turn. Service fields may be `null` if a service is unregistered (unit
tests) — every node must early-return [[AIResult]]`.Failed` instead of
NRE-ing.

## Dependencies

- **Uses:** [[AttributesManager]], [[IDamagePipeline]], `IGridManager`,
  `IMovementService`, [[IPlayerService]].
- **Used by:** [[TreeDrivenEnemyAI]] (builds), every [[AIDecisionNode]]
  / [[AICondition]] (consumes).

## Code

`Assets/Scripts/Rollgeon/Combat/AI/AIContext.cs`

## External references

- TECHNICAL.md: §7.5 AI tree context
