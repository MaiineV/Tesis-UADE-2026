---
title: BasicEnemyAI
type: service
domain: 02-Combat/AI
status: done
tags: [combat, ai, enemy]
---

# BasicEnemyAI

> Production-ready enemy AI handler that reads an enemy's
> [[Attack]] stat and pushes damage through [[DamagePipeline]]. Replaces
> the earlier `StubEnemyAIHandler`.

## Shape

```csharp
public sealed class BasicEnemyAI : IEnemyAIHandler {
    public BasicEnemyAI(
        AttributesManager attributes,
        IPlayerService playerService,
        IDamagePipeline damagePipeline,
        Action onTurnComplete);

    public void HandleEnemyTurn(Guid enemyId);
}
```

## Algorithm

1. Read `Attack.ModifiedValue` for `enemyId`. Missing stat → warn,
   `onTurnComplete()`, return.
2. `attackValue <= 0` → skip (Support archetype like the Auditor); call
   `onTurnComplete()`.
3. Build a [[DamageContext]] (`Kind = BasicAttack`, `SourceId =
   enemyId`, `TargetId = player`, `BaseDamage = attackValue`).
4. `damagePipeline.Resolve(ctx)`.
5. `onTurnComplete()` (typically `ICombatSignaller.SignalEnemyDone`).

## Dependencies

- **Uses:** [[AttributesManager]], [[Attack]], [[IPlayerService]],
  [[DamagePipeline]], [[DamageContext]], [[AttackKind]].
- **Used by:** [[RunController]] (instantiates), [[EnemyTurnState]]
  (invokes via `EnemyActionHandler`).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/AI/BasicEnemyAI.cs`
- Signaller: `.../ICombatSignaller.cs`
- Tests: `.../Tests/BasicEnemyAITests.cs`

## External references

- Setup: `docs/setup/System#0012b_EnemyAIReal.md`
- TECHNICAL.md: §7.1 / §12.1 Basic enemy AI
