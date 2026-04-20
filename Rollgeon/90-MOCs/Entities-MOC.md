---
title: Entities-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, entities]
---

# 05-Entities — Map of Content

> Entity data assets + behavior templates used by enemies, bosses, and
> future props / NPCs.

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
                          └─ BossComboBlockBehavior

 BaseBehavior → Trigger (BehaviorTrigger) ∧ AllowedPhases (GamePhaseMask)
              ─ StoredValues API (§9.3)

 BehaviorLibrarySO  ─ optional pool of named templates

 EnemyCatalogSO     ─ catalog of all EnemyDataSO
 BossFloorManagerSO ─ packs boss room + boss enemy + rewards (stub)
```

## Notes

- **Data:** [[BaseEntitySO]] · [[EnemyDataSO]] · [[EnemyCatalogSO]]
- **Behaviors:** [[BaseBehavior]] · [[BehaviorTrigger]] ·
  [[GamePhaseMask]] · [[HealStrength]] · [[BehaviorLibrarySO]]
- **Concretes:** [[SupportHealBehavior]] · [[BossAttackBehavior]] ·
  [[BossEnergyBuildupBehavior]] · [[BossComboBlockBehavior]]
- **Boss meta:** [[BossFloorManagerSO]]

## Cross-domain edges

- Consumed by [[DefaultEnemySpawnResolver]] (see [[Combat-MOC]]).
- Writes to [[WeaknessRegistry]] on spawn.
- Behaviors often fire [[EffDamage]] / [[EffHeal]] from
  [[Effects-MOC]].
