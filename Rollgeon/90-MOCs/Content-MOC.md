---
title: Content-MOC
type: moc
domain: 90-MOCs
status: wip
tags: [moc, content, catalogs]
---

# 13-Content — Map of Content

> Catalogs that index data assets (combos, actions, enemies, …). Many
> are still **TBD** for Sprint 03.

## Relationships

```
 BaseCatalogSO (abstract) ← ICatalog
        ↑
    implementations:
      ActionCatalogSO   ✅  (02-Combat/Actions)
      ComboCatalogSO    ✅  (03-Combos)
      EnemyCatalogSO    ✅  (05-Entities)
      BehaviorLibrarySO ✅  (05-Entities)
      EntityCatalogSO   🟡  TBD
      RoomCatalogSO     🟡  TBD
      RewardCatalogSO   🟡  TBD
      ItemCatalogSO     🟡  TBD
      StatusCatalogSO   🟡  TBD
      QuestCatalogSO    🟡  TBD
      DiceCatalogSO     🟡  TBD
      UnlockCatalogSO   🟡  TBD
      FeedbackDBSO      🟡  TBD
      RulesetCatalogSO  🟡  TBD
```

## Notes

- [[BaseCatalogSO]] · [[Content-Catalogs]]
- **Shipped catalogs** (home folders):
  [[ActionCatalogSO]] · [[ComboCatalogSO]] · [[EnemyCatalogSO]] ·
  [[BehaviorLibrarySO]]

## Cross-domain edges

- [[ServiceBootstrapSO]] lists every catalog.
- Each concrete catalog links back to the domain that owns it.
