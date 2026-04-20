---
title: BaseCatalogSO
type: so
domain: 13-Content
status: done
tags: [content, catalog, base, so]
---

# BaseCatalogSO

> Abstract base for every catalog asset. Provides the `ICatalog` hook
> (name + async preload) and lets [[ServiceBootstrapSO]] register any
> catalog generically by runtime type.

## Shape

```csharp
public abstract class BaseCatalogSO : SerializedScriptableObject, ICatalog {
    public abstract string CatalogName { get; }
    public virtual Task PreloadAsync() => Task.CompletedTask;
    // concrete catalogs provide their own typed lookup API.
}
```

## Why a shared base

- Lets [[ServiceBootstrapSO]]`.RegisterAll` iterate
  `List<BaseCatalogSO>` and register each under its runtime type.
- `PreloadAsync` supports Addressables-driven catalogs (`EntityCatalogSO`,
  future `FeedbackDBSO`). Default implementation is a no-op for inline
  catalogs.
- Centralises validation hooks so every catalog gets the same "no
  duplicates, no null entries" policy.

## Dependencies

- **Uses:** `ICatalog` interface.
- **Used by:** [[ActionCatalogSO]], [[ComboCatalogSO]],
  [[EnemyCatalogSO]], [[BehaviorLibrarySO]], future catalogs (reward,
  item, status, quest, feedback, room, entity, ruleset).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/Catalogs/BaseCatalogSO.cs`,
  `ICatalog.cs`

## External references

- TECHNICAL.md: §0 / §13.6 Catalogs
