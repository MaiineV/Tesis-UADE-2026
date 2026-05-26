---
title: BossComboImmunityBehavior
type: behavior
domain: 05-Entities
status: done
tags: [entities, behavior, boss, combo-block]
---

# BossComboImmunityBehavior

> Boss passive that blocks a specific combo (typically `Par`) from
> hitting the boss. Each boss turn re-blocks the combo via
> [[IComboBlockService]] with a long duration; because `Block` keeps
> the max of competing durations, the immunity effectively never
> expires inside the fight.

## Why a dedicated behavior

Boss immunity is data-driven: floor authors drop in a
`BossComboImmunityBehavior` referencing a `BaseComboSO`, instead of
hard-coding boss-specific damage filters. Added in commit `23e162a` —
fixes the bug where a blocked combo still applied via
`EffDealDamage`'s constant-fallback path.

## Behavior

- `BehaviorName = "Boss Combo Immunity"`.
- `Trigger = OnTurnStart` (no `OnCombatStart` trigger exists today).
  Idempotent — re-applying every boss turn keeps the block fresh.
- Cleanup is automatic: `ComboBlockService` listens to `OnCombatEnd`
  and clears all blocks, so immunity never leaks into later combats.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class BossComboImmunityBehavior : BaseBehavior {
    public BaseComboSO ImmuneCombo;
    public int RefreshDurationTurns = 99;
}
```

## Dependencies
**Uses:** [[BaseBehavior]], [[IComboBlockService]], `BaseComboSO`,
`ServiceLocator`.
**Used by:** the floor-1 boss `EnemyDataSO`.

## Code
`Assets/Scripts/Rollgeon/Entities/Behaviors/BossComboImmunityBehavior.cs`
