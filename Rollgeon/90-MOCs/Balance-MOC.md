---
title: Balance-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, balance]
---

# 14-Balance — Map of Content

> `RulesetSO` plus its sub-configs. One asset per game mode.

## Relationships

```
 RulesetSO
   ├─ Energy    : EnergyConfig       → EnergyService / EnergyRegenPolicy
   ├─ TurnOrder : TurnOrderConfig    → DefaultInitiativeProvider
   ├─ Weakness  : WeaknessConfig     → WeaknessChecker
   └─ Counters  : ComboCountersConfig → ComboCountersService
```

## Notes

- [[RulesetSO]] · [[EnergyConfig]] · [[TurnOrderConfig]] ·
  [[WeaknessConfig]]
- Also part of this pipeline (lives in Combos): [[ComboCountersConfig]]

## Cross-domain edges

- Registered as a `SettingsAsset` in [[ServiceBootstrapSO]].
- Consumed by [[EnergyService]], [[TurnOrderService]], [[WeaknessChecker]],
  [[ComboCountersService]].
- `ForbiddenActionIds` hook is stubbed inside [[TurnManager]] — pending
  Balance#0101.
