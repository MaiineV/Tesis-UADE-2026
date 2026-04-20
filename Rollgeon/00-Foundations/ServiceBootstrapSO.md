---
title: ServiceBootstrapSO
type: so
domain: 00-Foundations
status: done
tags: [foundation, bootstrap, so, catalog]
---

# ServiceBootstrapSO

> `ScriptableObject` that lists every catalog, settings asset, and
> runtime service that [[BootstrapRunner]] must register at startup.
> Designed to grow pluggably: each worktree adds its catalog / service
> reference without touching the code.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Service Bootstrap")]
public class ServiceBootstrapSO : SerializedScriptableObject {
    public List<BaseCatalogSO>     Catalogs;
    public List<ScriptableObject>  SettingsAssets;
    public List<IPreloadableService> ExtraServices;
    public string NextSceneName { get; } // default "01_MainMenu"

    public void RegisterAll();
    public Task PreloadAllCatalogsAsync();
}
```

## Registration rules

- Every catalog / settings asset is registered under its **runtime
  `Type`** via reflection (Odin needs this because [[ServiceLocator]]'s
  public API is generic).
- `ExtraServices` are called in ascending `Priority` order via
  `IPreloadableService.Register()`.
- Null entries are skipped with a log warning. Duplicate instances are
  rejected by Odin `ValidateInput` attributes.

## Dependencies

- **Uses:** [[ServiceLocator]], [[ServiceScope]] (always `Global`),
  [[IPreloadableService]], [[BaseCatalogSO]].
- **Used by:** [[BootstrapRunner]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/Bootstrap/ServiceBootstrapSO.cs`
- Asset: `Assets/Rollgeon/ServiceBootstrap.asset`

## External references

- Setup: `docs/setup/Foundation#0005_CatalogsAndBootstrap.md`
- TECHNICAL.md: §1.1.1 ServiceBootstrapSO
