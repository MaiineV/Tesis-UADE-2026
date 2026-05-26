---
title: Combo_FullHouse
type: so
domain: 03-Combos
status: done
tags: [combos, concrete]
---

# Combo_FullHouse

> Full House: a Trio plus a Pair on the same roll.

## Detection

```csharp
Matches: one pip appears exactly 3 times AND a different pip appears exactly 2 times.
CountUsed: 5.
BaseDamage (inspector default): 40.
```

A Poker (four of a kind) does **not** count as a Full House because the
second pair requirement fails — the stricter equality check excludes
overlap cases.

## Dependencies

- **Uses:** [[BaseComboSO]].
- **Used by:** [[ComboCatalogSO]], [[ContractSheet]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Concretes/Combo_FullHouse.cs`

## External references

- TECHNICAL.md: §5.1.2 Combo list
