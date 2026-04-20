---
title: EnergyConfig
type: config
domain: 14-Balance
status: done
tags: [balance, energy, config]
---

# EnergyConfig

> Sub-struct of [[RulesetSO]] carrying the three Energy knobs.

## Shape

```csharp
[Serializable]
public sealed class EnergyConfig {
    public int EnergyMax         = 4;   // GDD default
    public int EnergyAtRunStart  = 2;
    public int EnergyRegenBase   = 2;

    public void Validate(); // clamp to non-negative, cap start ≤ max
}
```

## Consumers

- [[EnergyService]]`.InitializeForEntity` — seeds `Energy.Value` with
  `EnergyAtRunStart`.
- [[EnergyService]]`.SpendEnergy` — checks against `EnergyMax` for
  over-cap prevention.
- [[EnergyRegenPolicy]]`.ComputeNewCurrent` — adds `EnergyRegenBase`
  clamped to `EnergyMax`.

## Dependencies

- **Used by:** [[RulesetSO]], [[EnergyService]], [[EnergyRegenPolicy]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Balance/EnergyConfig.cs`

## External references

- Setup: `docs/setup/System#0100a_EnergyAttributeAndRegen.md`
- TECHNICAL.md: §12.6 Energy config
