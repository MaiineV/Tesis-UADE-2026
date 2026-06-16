---
title: Combo_DoblePar
type: so
domain: 03-Combos
status: done
tags: [combos, concrete]
---

# Combo_DoblePar

> Two distinct pairs on the same roll.

## Detection

```csharp
Matches: exactly two distinct pip values with ≥ 2 occurrences each
         (or one with ≥ 4, since a Poker implies two pairs).
CountUsed: 4.
BaseDamage (inspector default): 18.
```

## Dependencies

- **Uses:** [[BaseComboSO]].
- **Used by:** [[ComboCatalogSO]], [[ContractSheet]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Concretes/Combo_DoblePar.cs`

## External references

- TECHNICAL.md: §5.1.2 Combo list
