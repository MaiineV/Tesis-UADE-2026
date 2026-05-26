---
title: BossAttackBehavior
type: system
domain: 05-Entities
status: done
tags: [entities, behavior, boss, attack]
---

# BossAttackBehavior

> Boss-tier attack that bypasses [[BasicEnemyAI]]. Builds its own
> [[DamageContext]] with configurable base damage and drives
> [[DamagePipeline]] directly.

## Why a dedicated behavior

Bosses are not "just enemies with bigger numbers". They can:

- Ignore weakness multipliers.
- Hit multiple targets via [[SelectionSettings]] / [[ISelectionController]].
- Trigger their own `ExtraEffects` (status, movement, etc.).
- Branch on phase / hp threshold via overridden `CanExecute`.

`BasicEnemyAI` is too narrow for any of that; this behavior owns the
decision tree.

## Dependencies

- **Uses:** [[BaseBehavior]], [[DamagePipeline]], [[DamageContext]],
  [[AttackKind]], [[ISelectionController]].
- **Used by:** the floor-1 boss `EnemyDataSO` (Content#0103).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BossAttackBehavior.cs`
- Tests: `Entities/Bosses/Tests/BossAttackBehaviorTests.cs`

## External references

- Setup: `docs/setup/Content#0103_BossFloorManager.md`
- TECHNICAL.md: §7.2 Boss behaviors
