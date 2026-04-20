---
title: RulesetSO
type: so
domain: 14-Balance
status: done
tags: [balance, so, config, ruleset]
---

# RulesetSO

> Central balance asset (§14.7). One `RulesetSO` per game mode / "ruleset"
> — Arcade, Hardcore, Relaxed — carrying typed sub-configs that the
> services read at runtime.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Balance/Ruleset")]
public class RulesetSO : SerializedScriptableObject {
    public string RulesetId;       // catalog key
    public string DisplayName;
    public string Description;

    public EnergyConfig       Energy    { get; }   // T100a
    public TurnOrderConfig    TurnOrder { get; }   // T100c
    public WeaknessConfig     Weakness  { get; }   // T97b
    public ComboCountersConfig Counters { get; }   // T97c

    // Pending sub-configs: RollConfig, ScalingConfig, CritConfig, LootConfig, …
}
```

## Registration

- Goes into [[ServiceBootstrapSO]]`.SettingsAssets`.
- Registered globally under its runtime type `RulesetSO`.
- Resolved by [[EnergyService]], [[TurnOrderService]],
  [[WeaknessChecker]], [[ComboCountersService]] during their `Register`.

## Merge rule

Only **one** `RulesetSO.cs` exists in `Rollgeon.Balance`. Each new
worktree appends its sub-struct here; duplicated files are a merge bug.

## Dependencies

- **Uses:** [[EnergyConfig]], [[TurnOrderConfig]], [[WeaknessConfig]],
  [[ComboCountersConfig]].
- **Used by:** [[EnergyService]], [[TurnManager]], [[TurnOrderService]],
  [[WeaknessChecker]], [[ComboCountersService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Balance/RulesetSO.cs`

## External references

- TECHNICAL.md: §14.7 Ruleset
