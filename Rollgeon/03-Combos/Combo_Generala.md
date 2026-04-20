---
title: Combo_Generala
type: so
domain: 03-Combos
status: done
tags: [combos, concrete, top-tier]
---

# Combo_Generala

> Five of a kind. Highest-priority combo.

## Detection

```csharp
Matches: all 5 dice share the same pip value.
CountUsed: 5.
BaseDamage (inspector default): 100.
Priority => int.MaxValue;   // always wins overlap resolution
```

## Why override `Priority`

Every other combo defaults `Priority = BaseDamage`. Generala pins itself
to `int.MaxValue` so it always wins against edge cases — e.g. a custom
SumaX build whose dynamic damage happened to exceed 100 at runtime.

## Dependencies

- **Uses:** [[BaseComboSO]].
- **Used by:** [[ComboCatalogSO]], [[ContractSheet]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Concretes/Combo_Generala.cs`

## External references

- TECHNICAL.md: §5.1.2 / §10.7 Combo priority
