---
title: SupportHealBehavior
type: system
domain: 05-Entities
status: done
tags: [entities, behavior, heal, support]
---

# SupportHealBehavior

> Concrete [[BaseBehavior]] that heals the lowest-HP ally (or a chosen
> target) during the carrier's turn. Canonical support archetype — used
> by the Auditor enemy (Content#0099).

## Behaviour

- `Trigger = OnTurnStart`, `AllowedPhases = Combat`.
- On `Execute(ctx)`:
  1. Resolve target via [[ISelectionController]] (typically allies excluding
     self).
  2. Compute heal amount = `BaseHealAmount + HealStrength.ModifiedValue`.
  3. Push through [[HealPipeline]] clamped to `EnemyDataSO.BaseHP`.

## Dependencies

- **Uses:** [[BaseBehavior]], [[HealStrength]], [[HealPipeline]],
  [[ISelectionController]], `BehaviorContext`.
- **Used by:** the Auditor enemy data asset, any other support that
  shares this recipe.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/SupportHealBehavior.cs`
- Tests: `.../Tests/SupportHealBehaviorTests.cs`

## External references

- Setup: `docs/setup/Content#0099_SupportEnemyAuditor.md`
- TECHNICAL.md: §7.2 Support archetype
