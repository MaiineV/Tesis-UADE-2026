---
title: EnemyCatalogSO
type: catalog
domain: 05-Entities
status: done
tags: [entities, catalog, so]
---

# EnemyCatalogSO

> Catalog SO listing every [[EnemyDataSO]] known to the game. Registered
> by [[Bootstrap]] and looked up by id from [[EnemyPoolSO]] entries.

## Responsibilities

- Fast `EntityId → EnemyDataSO` lookup.
- Validation: no duplicates, no null entries.
- Source of inspector dropdowns that pick enemies by id.

## Dependencies

- **Uses:** [[EnemyDataSO]], [[BaseCatalogSO]] base.
- **Used by:** [[EnemyPoolSO]], [[DefaultEnemySpawnResolver]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/EnemyCatalogSO.cs`
- Tests: `.../Tests/EnemyCatalogSOTests.cs`

## External references

- TECHNICAL.md: §7.3 Enemy catalog
