---
title: Combo_SumaX
type: so
domain: 03-Combos
status: done
tags: [combos, concrete, variable]
---

# Combo_SumaX

> Variable-damage combo: base damage equals the sum of the dice that
> match a configurable target pip X.

## Detection

- `Matches`: at least one die equals the configured target pip `X`.
- `CountUsed`: number of dice equal to X (variable, 1–5).
- `BaseDamage` (per asset): default **25**, plus the dynamic sum of
  matching dice rolled into `ComboDetectionResult.BaseDamage`.

## Override

Because the damage is data-dependent, `Combo_SumaX` overrides `Detect`
directly instead of relying on the default (which uses the static
`BaseDamage` inspector field).

## Dependencies

- **Uses:** [[BaseComboSO]], [[ComboDetectionResult]].
- **Used by:** [[ComboCatalogSO]], [[ContractSheet]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Concretes/Combo_SumaX.cs`

## External references

- TECHNICAL.md: §5.1.2 SumaX
