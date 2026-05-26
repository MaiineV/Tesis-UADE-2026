---
title: TurnOrderConfig
type: config
domain: 14-Balance
status: done
tags: [balance, turn-order, config]
---

# TurnOrderConfig

> Sub-struct of [[RulesetSO]] configuring initiative rolls.

## Shape (typical)

```csharp
[Serializable]
public struct TurnOrderConfig {
    public int  SpeedDieMin;       // lower bound of the speed-die
    public int  SpeedDieMax;       // upper bound
    public int  FallbackInitiative; // for entities without Speed stat

    public void OnValidate();
}
```

## Consumers

- [[DefaultInitiativeProvider]] rolls the speed die between
  `SpeedDieMin`..`SpeedDieMax` and adds
  `Speed.ModifiedValue`, falling back to `FallbackInitiative` when the
  entity lacks the stat.
- [[IInitiativeRng]] is pluggable so tests can inject
  `FixedInitiativeRng`.

## Dependencies

- **Used by:** [[RulesetSO]], [[DefaultInitiativeProvider]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Balance/TurnOrderConfig.cs`

## External references

- Setup: `docs/setup/System#0100c_TurnOrderHiddenSpeed.md`
- TECHNICAL.md: §12.7 TurnOrderConfig
