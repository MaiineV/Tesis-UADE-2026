---
title: ComboCatalogSO
type: catalog
domain: 03-Combos
status: done
tags: [combos, catalog, so]
---

# ComboCatalogSO

> Catalog SO that groups every [[BaseComboSO]] concrete available in the
> game. Registered globally by [[Bootstrap]] and used by [[ContractSheet]]
> to pick combos by id.

## API

- `IEnumerable<string> AllIds` — source of Odin `[ValueDropdown]` on
  combo-id fields in the inspector.
- Lookup by id (exact match against `ComboId`).
- Validation: no duplicate ids, no null entries, all `ComboId`s follow
  `combo.<snake_case>`.

## Dependencies

- **Uses:** [[BaseComboSO]], [[BaseCatalogSO]] base.
- **Used by:** [[ContractSheet]], [[BaseComboSO]] dropdown,
  [[ActionCatalogSO]] cross-refs, AttackResolver.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/ComboCatalogSO.cs`
- Tests: `.../Tests/ComboCatalogTests.cs`

## External references

- Setup: `docs/setup/Content#0097a_ComboBaseAndConcretes.md`
- TECHNICAL.md: §5.2 Combo catalog
