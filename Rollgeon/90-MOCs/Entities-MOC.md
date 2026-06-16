---
title: Entities-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, entities]
---

# 05-Entities — Map of Content

> Entity data assets + behavior templates used by enemies, bosses, and
> future props / NPCs. Plus the visual layer (pawns, world-space HUD).

## Relationships

```
 BaseEntitySO
     └─ EnemyDataSO
           ├─ CreateRuntimeStats()  → Health, Attack, Speed, Energy, HealStrength
           ├─ CreateRuntimeBehaviors() → deep copy of Behaviors (Odin)
           └─ Behaviors : List<BaseBehavior>
                          ├─ SupportHealBehavior (Auditor)
                          ├─ BossAttackBehavior  (floor-1 boss)
                          ├─ BossEnergyBuildupBehavior
                          ├─ BossComboBlockBehavior
                          └─ BossComboImmunityBehavior

 BaseBehavior → Trigger (BehaviorTrigger) ∧ AllowedPhases (GamePhaseMask)
              ─ BehaviorContext + BaseBehaviorStoredValue (§9.3)
                   └─ FloatBehaviorValue, FloatingNumberBehaviorValue,
                      ImpulseBehaviorValue (keyed by BehaviorValueKey)

 BehaviorLibrarySO  ─ optional pool of named templates

 EnemyCatalogSO     ─ catalog of all EnemyDataSO
 BossFloorManagerSO ─ packs boss room + boss enemy + rewards (stub)

 EntityVisualService (IEntityVisualService)
     └─ EntityPawn (PawnKind) → WorldSpaceHealthBar
```

## Notes

- **Data:** [[BaseEntitySO]] · [[EnemyDataSO]] · [[EnemyCatalogSO]]
- **Behaviors:** [[BaseBehavior]] · [[BehaviorTrigger]] ·
  [[BehaviorContext]] · [[GamePhaseMask]] ·
  [[GamePhaseMaskExtensions]] · [[HealStrength]] ·
  [[BehaviorLibrarySO]]
- **Stored values:** [[BaseBehaviorStoredValue]] ·
  [[BehaviorValueKey]] · [[FloatBehaviorValue]] ·
  [[FloatingNumberBehaviorValue]] · [[ImpulseBehaviorValue]]
- **Concretes:** [[SupportHealBehavior]] · [[BossAttackBehavior]] ·
  [[BossEnergyBuildupBehavior]] · [[BossComboBlockBehavior]] ·
  [[BossComboImmunityBehavior]]
- **Boss meta:** [[BossFloorManagerSO]]
- **Visuals:** [[EntityPawn]] · [[EntityVisualService]] ·
  [[IEntityVisualService]] · [[PawnKind]] · [[WorldSpaceHealthBar]]

## Cross-domain edges

- Consumed by [[DefaultEnemySpawnResolver]] (see [[Combat-MOC]]).
- Writes to [[WeaknessRegistry]] on spawn.
- Behaviors often fire [[EffDealDamage]] / [[EffHeal]] from
  [[Effects-MOC]].
- [[EntityPawn]] positions integrate with [[Movement-MOC|Movement]] and
  trigger pulses through [[Feedback-MOC|Feedback]].
- Death events drive gold drops via [[Economy-MOC|Economy]]
  ([[EnemyGoldDropService]]).
