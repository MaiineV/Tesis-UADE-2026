---
title: TreeDrivenEnemyAI
type: service
domain: 02-Combat/AI
status: done
tags: [combat, ai, enemy, tree]
---

# TreeDrivenEnemyAI

> Production enemy AI handler that ticks the per-enemy decision tree
> registered in [[EnemyAIRegistry]]. Falls back to [[BasicEnemyAI]]
> when no tree is registered for the given guid (TECHNICAL.md §7.5).

## Shape

```csharp
public sealed class TreeDrivenEnemyAI : IEnemyAIHandler, IDisposable {
    public TreeDrivenEnemyAI(
        IEnemyAIRegistry registry,
        AttributesManager attributes,
        IPlayerService playerService,
        IDamagePipeline damagePipeline,
        BasicEnemyAI fallback,
        Action onTurnComplete);

    public int CurrentRoundIndex { get; }
    public void HandleEnemyTurn(Guid enemyId);
    public void Dispose();
}
```

## Behaviour

1. Subscribes to [[EventName]] `OnTurnQueueBuilt` to keep
   `CurrentRoundIndex` (consumed by [[AICond_RoundNumber]]).
2. `HandleEnemyTurn` looks up the enemy's tree via
   `registry.TryGet`. If missing → delegate to `BasicEnemyAI` and
   return.
3. Builds an [[AIContext]] (self guid, resolved services, round index,
   default RNG) and calls `root.Tick(ctx)`.
4. Wraps the tick in a try/catch — exceptions are logged, never
   propagated, and the turn still completes via the `onTurnComplete`
   callback.

## Dependencies

- **Uses:** [[IEnemyAIRegistry]], [[BasicEnemyAI]] (fallback),
  [[AttributesManager]], [[IPlayerService]], [[IDamagePipeline]],
  [[AIContext]], [[EventManager]], [[EventName]].
- **Used by:** [[RunController]] (instantiates and registers as
  `IEnemyAIHandler`).

## Code

`Assets/Scripts/Rollgeon/Combat/AI/TreeDrivenEnemyAI.cs`

## External references

- TECHNICAL.md: §7.5 Tree-driven enemy AI
