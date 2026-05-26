---
title: Content-Catalogs
type: concept
domain: 13-Content
status: wip
tags: [content, catalog, index]
---

# Content Catalogs — status overview

> Index of every catalog specified in `TECHNICAL.md §0 / §13.6` vs.
> what is actually implemented in Sprint 03.

| Catalog | Status | Notes |
|---|---|---|
| [[ActionCatalogSO]]        | ✅ done  | action economy |
| [[ComboCatalogSO]]         | ✅ done  | 8 Warrior combos |
| [[EnemyCatalogSO]]         | ✅ done  | enemies |
| [[BehaviorLibrarySO]]      | ✅ done  | behaviors |
| EntityCatalogSO            | 🟡 TBD   | Unified entity catalog across enemies / props / npcs (§7). Today covered by EnemyCatalogSO. |
| RoomCatalogSO              | 🟡 TBD   | Currently inline via FloorLayoutSO references. |
| RulesetCatalogSO           | 🟡 TBD   | Today a single [[RulesetSO]] registered directly. |
| FeedbackDBSO               | 🟡 TBD   | §10 feedback pipeline unimplemented. |
| RewardCatalogSO            | 🟡 TBD   | §19 rewards not in Sprint 03 scope. |
| ItemCatalogSO              | 🟡 TBD   | §18 items not in Sprint 03 scope. |
| StatusCatalogSO            | 🟡 TBD   | §20 status effects not implemented. |
| QuestCatalogSO             | 🟡 TBD   | §21 quests not implemented. |
| DiceCatalogSO              | 🟡 TBD   | §6 dice types not yet catalogued; currently hard-coded. |
| UnlockCatalogSO            | 🟡 TBD   | §14 meta-progression not implemented. |

## Registration shape

Every implemented catalog inherits [[BaseCatalogSO]] and gets added to
[[ServiceBootstrapSO]]`.Catalogs`. At bootstrap time, reflection
(`AddService<T>(instance, Global)`) registers the concrete under its
runtime type so consumers can do
`ServiceLocator.GetService<MyCatalogSO>()`.

## External references

- TECHNICAL.md: §0 Catalog conventions / §13.6 Polymorphic SOs
