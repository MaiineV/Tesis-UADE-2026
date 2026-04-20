---
title: Combo_Poker
type: so
domain: 03-Combos
status: done
tags: [combos, concrete]
---

# Combo_Poker

> Four of a kind.

## Detection

```csharp
Matches: at least one pip value appears ≥ 4 times.
CountUsed: 4.
BaseDamage (inspector default): 60.
```

## Dependencies

- **Uses:** [[BaseComboSO]].
- **Used by:** [[ComboCatalogSO]], [[ContractSheet]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Concretes/Combo_Poker.cs`

## External references

- TECHNICAL.md: §5.1.2 Combo list
