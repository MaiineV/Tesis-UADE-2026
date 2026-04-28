---
title: EnemyAIRegistry
type: service
domain: 02-Combat/AI
status: done
tags: [combat, ai, registry, service]
---

# EnemyAIRegistry

> Default in-memory implementation of [[IEnemyAIRegistry]]. Maps
> `enemyId -> (AIDecisionNode root, int maxHp)` so
> [[TreeDrivenEnemyAI]] can resolve the per-instance behavior tree on
> each enemy turn.

## Shape

```csharp
public sealed class EnemyAIRegistry : IEnemyAIRegistry {
    public void Register(Guid enemyId, AIDecisionNode root, int maxHp);
    public void Unregister(Guid enemyId);
    public bool TryGet(Guid enemyId, out AIDecisionNode root, out int maxHp);
    public bool Has(Guid enemyId);
}
```

## Behaviour

- `Register` clones the authored root tree per enemy at spawn time
  (clone is the responsibility of the caller —
  `DefaultEnemySpawnResolver`).
- `Guid.Empty` register is rejected.
- `TryGet` returns `(null, 0)` when the id is unknown so
  [[TreeDrivenEnemyAI]] can fall back to [[BasicEnemyAI]].

## Dependencies

- **Uses:** [[AIDecisionNode]].
- **Used by:** [[EnemyAIRegistryBootstrap]] (registers),
  [[TreeDrivenEnemyAI]] (consumer), `DefaultEnemySpawnResolver`
  (writer).

## Code

`Assets/Scripts/Rollgeon/Combat/AI/EnemyAIRegistry.cs`

## External references

- TECHNICAL.md: §7.5 AI registry
