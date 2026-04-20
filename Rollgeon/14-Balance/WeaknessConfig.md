---
title: WeaknessConfig
type: config
domain: 14-Balance
status: done
tags: [balance, weakness, config]
---

# WeaknessConfig

> Sub-struct of [[RulesetSO]] that carries the default weakness
> multiplier used when an [[EnemyDataSO]] does not override it.

## Shape

```csharp
[Serializable]
public sealed class WeaknessConfig {
    public float DefaultMultiplier = 1.5f; // GDD baseline
    public void Validate();                 // clamp to ≥ 1f
}
```

## Consumers

- [[WeaknessChecker]] reads `DefaultMultiplier` when an
  [[EnemyDataSO]]`.WeaknessMultiplierOverride` is 0.
- [[EnemyDataSO]]`.WeaknessMultiplierOverride > 0` pins the per-enemy
  override above this default.

## Dependencies

- **Used by:** [[RulesetSO]], [[WeaknessChecker]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Balance/WeaknessConfig.cs`

## External references

- Setup: `docs/setup/Content#0097b_WarriorContractAndWeakness.md`
- TECHNICAL.md: §5 WeaknessConfig
