---
title: BossEnergyBuildupBehavior
type: system
domain: 05-Entities
status: done
tags: [entities, behavior, boss, energy]
---

# BossEnergyBuildupBehavior

> Boss behavior that accumulates [[Energy]] over turns and releases a
> burst attack once a threshold is crossed.

## Pattern

- `Trigger = OnTurnEnd`.
- Spends no energy; instead increments the boss's own `Energy` stat via
  [[AttributesManager]]`.Modify<Energy,int>`.
- On reaching `Threshold`, swaps into a burst phase: fires an
  [[EffDamage]]-style effect, then resets the counter.

This is a common boss rhythm pattern — visible telegraph, punishable
charge window, predictable burst.

## Dependencies

- **Uses:** [[BaseBehavior]], [[AttributesManager]], [[Energy]],
  [[EffDamage]] (or equivalent `ExtraEffects`).
- **Used by:** the floor-1 boss `EnemyDataSO`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/BossEnergyBuildupBehavior.cs`
- Tests: `Entities/Bosses/Tests/BossEnergyBuildupBehaviorTests.cs`

## External references

- Setup: `docs/setup/Content#0103_BossFloorManager.md`
- TECHNICAL.md: §7.2 Boss energy buildup
