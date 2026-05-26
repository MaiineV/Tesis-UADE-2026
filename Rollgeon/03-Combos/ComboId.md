---
title: ComboId
type: concept
domain: 03-Combos
status: done
tags: [combos, id, convention]
---

# ComboId

> Canonical string id for a combo. Format `combo.<snake_case>`
> (examples: `combo.par`, `combo.doble_par`, `combo.full_house`,
> `combo.generala`). Case-sensitive.

## Why a string id

- Survives rename refactors without churning binary serialization.
- Indexable by [[ComboCatalogSO]].
- Cross-references in [[ActionDefinitionSO]]`.ActionId`
  (`combo.full_house` etc.) line up with the action economy layer.

## Where it lives

The `ComboId.cs` file holds either a small helper class or constants
for the canonical string forms — the source of truth for combo ids is
still each [[BaseComboSO]] asset.

## Dependencies

- **Used by:** [[BaseComboSO]], [[ComboCatalogSO]],
  [[ActionDefinitionSO]], [[ContractSheet]], [[ComboCountersService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/ComboId.cs`

## External references

- TECHNICAL.md: §5.1 Combo id convention
