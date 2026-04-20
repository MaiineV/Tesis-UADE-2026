---
title: Bootstrap
type: concept
domain: 00-Foundations
status: done
tags: [foundation, bootstrap, startup]
---

# Bootstrap

> Startup pipeline that runs in the `00_Bootstrap` scene: hydrates
> [[ServiceLocator]] with catalogs + settings + extra services, preloads
> async assets, then loads `01_MainMenu`.

## Flow

```
Unity loads 00_Bootstrap.unity
  └─ BootstrapRunner.Awake  (DefaultExecutionOrder = -10000)
       ├─ ServiceBootstrapSO.RegisterAll()
       │     ├─ each Catalog         → ServiceLocator.AddService(Global)
       │     ├─ each SettingsAsset   → ServiceLocator.AddService(Global)
       │     └─ each ExtraService    → IPreloadableService.Register()
       ├─ await ServiceBootstrapSO.PreloadAllCatalogsAsync()
       ├─ BootstrapHooks.Install()
       └─ SceneManager.LoadScene(NextSceneName)   // "01_MainMenu"
```

## Pieces

- [[ServiceBootstrapSO]] — ScriptableObject hub. Lists catalogs,
  settings, extra services, and next-scene name.
- [[BootstrapRunner]] — MonoBehaviour sitting in `00_Bootstrap` that
  orchestrates the Awake pipeline.
- [[IPreloadableService]] — marker for runtime services that need a
  `Register()` call during bootstrap.

## Why bootstrap at all

Gameplay MonoBehaviours need every service already registered when their
`Awake` runs. Without a single well-known entry point we would get
order-of-initialization bugs in the Unity scene graph. The
`DefaultExecutionOrder(-10000)` on [[BootstrapRunner]] guarantees it
wins every race.

## Dependencies

- **Uses:** [[ServiceLocator]], [[ServiceScope]], [[IPreloadableService]],
  [[ServiceBootstrapSO]], [[BootstrapRunner]].

## External references

- Setup: `docs/setup/Foundation#0005_CatalogsAndBootstrap.md`
- TECHNICAL.md: §1.1.1 Bootstrap pipeline
