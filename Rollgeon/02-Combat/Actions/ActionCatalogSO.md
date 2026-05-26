---
title: ActionCatalogSO
type: catalog
domain: 02-Combat/Actions
status: done
tags: [combat, actions, catalog, so]
---

# ActionCatalogSO

> Catalog SO that groups every [[ActionDefinitionSO]] the player or
> enemies can fire. Registered in [[Bootstrap]] and consumed by
> [[TurnManager]] and combat HUDs.

## Responsibilities

- Fast lookup `ActionId → ActionDefinitionSO`.
- Validation (no duplicate ids, no null entries).
- Source of the `[ValueDropdown]` on `ActionDefinitionSO.ActionId` in
  the inspector (editor-time only).

## Dependencies

- **Uses:** [[ActionDefinitionSO]], [[BaseCatalogSO]] base.
- **Used by:** [[TurnManager]], combat HUD, combo executor.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Actions/ActionCatalogSO.cs`
- Tests: `.../Tests/ActionCatalogSOTests.cs`

## External references

- TECHNICAL.md: §12.6.0 Action catalog
