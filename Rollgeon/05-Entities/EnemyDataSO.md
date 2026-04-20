---
title: EnemyDataSO
type: so
domain: 05-Entities
status: done
tags: [entities, so, enemy]
---

# EnemyDataSO

> Concrete [[BaseEntitySO]] for enemies. Carries weakness config, base
> stats, and a polymorphic list of [[BaseBehavior]]s.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Entities/Enemy Data")]
public class EnemyDataSO : BaseEntitySO {
    // Weakness (§5 — T97b)
    public string WeaknessComboId;
    public float  WeaknessMultiplierOverride; // 0 = use ruleset default

    // Base stats (Content#0099)
    public int BaseHP = 20;
    public int BaseAttack = 0;       // supports like Auditor run at 0
    public int BaseHealStrength = 5;
    public int BaseSpeed = 4;
    public int MaxEnergy = 3;

    // Behaviors
    [OdinSerialize] public List<BaseBehavior> Behaviors = new();

    public override ModifiableAttributes CreateRuntimeStats(); // Health, Attack, Speed, Energy, HealStrength
    public List<BaseBehavior> CreateRuntimeBehaviors();         // deep-clones via Odin SerializationUtility
}
```

## Runtime cloning

Both stats and behaviors are deep-cloned on spawn:

- `CreateRuntimeStats()` instantiates fresh [[Health]], [[Attack]],
  [[Speed]], [[Energy]], [[HealStrength]] stats.
- `CreateRuntimeBehaviors()` uses Odin's `SerializationUtility.CreateCopy`
  so every spawn has its own `StoredValues` bag and no reference back to
  the asset.

## Dependencies

- **Uses:** [[BaseEntitySO]], [[Health]], [[Attack]], [[Speed]],
  [[Energy]], [[HealStrength]], [[BaseBehavior]], [[ComboCatalogSO]]
  (dropdown source).
- **Used by:** [[EnemyCatalogSO]], [[EnemyPoolSO]],
  [[DefaultEnemySpawnResolver]], [[WeaknessRegistry]] (on spawn).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/EnemyDataSO.cs`
- Tests: `.../Tests/EnemyDataSOTests.cs`

## External references

- Setup: `docs/setup/Content#0099_SupportEnemyAuditor.md`
- TECHNICAL.md: §7.1 EnemyDataSO
