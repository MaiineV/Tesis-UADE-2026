---
title: Combo_Par
type: so
domain: 03-Combos
status: done
tags: [combos, concrete]
---

# Combo_Par

> Pair of matching dice. Minimum combo in the Generala hierarchy.

## Detection

```csharp
Matches: at least one dice value appearing ≥ 2 times.
CountUsed: 2.
BaseDamage (inspector default): 10.
```

## Notes

A hand that contains a Trio / Poker / Generala also matches as Par
(because `count ≥ 2`). Tie-breaking to the highest-priority combo is
handled downstream via [[BaseComboSO]]`.Priority`.

## Dependencies

- **Uses:** [[BaseComboSO]].
- **Used by:** [[ComboCatalogSO]], [[ContractSheet]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Concretes/Combo_Par.cs`

## External references

- Setup: `docs/setup/Content#0097a_ComboBaseAndConcretes.md`
- TECHNICAL.md: §5.1.2 Combo list
