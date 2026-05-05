---
title: Dice-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, dice, reroll]
---

# 08-Dice — Map of Content

> Dice rerolls + energy-to-reroll conversion. Full dice bag, types, and
> enchantments are `TECHNICAL.md §6` — TBD for Sprint 03.

## Relationships

```
 ActionDefinitionSO.FreeRollCount / AllowsEnergyReroll
            │
 RerollBudgetService.StartAction(action)
            │
 RerollBudget (pure state) ─ Remaining / TryConsumeFree
            │
 RerollBudgetService.Query → RerollQueryResult
            │
 RerollCountView (HUD)
```

## Notes

- **Reroll:** [[RerollBudgetService]] · [[IRerollBudgetService]] ·
  [[RerollBudget]] · [[RerollQueryResult]]
- **Roller:** [[DiceRoller]] · [[IDiceRoller]]
- **Bags & types:** [[DiceBagSO]] · [[DiceBagPoolSO]] ·
  [[DicePoolEntry]] · [[DiceType]] · [[DiceTypeExt]]

## Cross-domain edges

- Consumes [[EnergyService]] for paid rerolls (see [[Combat-MOC]]).
- Read by [[RerollCountView]] in [[UI-MOC]].
- **TBD:** `DiceEnchantmentSO` — see [[Crosscutting-Overview]].
