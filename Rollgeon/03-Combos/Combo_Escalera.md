---
title: Combo_Escalera
type: so
domain: 03-Combos
status: done
tags: [combos, concrete]
---

# Combo_Escalera

> Straight: five dice in ascending sequence (e.g. `1-2-3-4-5` or
> `2-3-4-5-6`).

## Detection

```csharp
Matches: sorted unique dice values form a consecutive run of length 5.
CountUsed: 5.
BaseDamage (inspector default): 35.
```

Implementation normalises by sorting and de-duplicating first, so dice
order off the roll does not matter.

## Dependencies

- **Uses:** [[BaseComboSO]].
- **Used by:** [[ComboCatalogSO]], [[ContractSheet]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Concretes/Combo_Escalera.cs`

## External references

- TECHNICAL.md: §5.1.2 Combo list
