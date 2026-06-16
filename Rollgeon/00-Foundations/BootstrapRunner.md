---
title: BootstrapRunner
type: service
domain: 00-Foundations
status: done
tags: [foundation, bootstrap, monobehaviour]
---

# BootstrapRunner

> `MonoBehaviour` that lives in `00_Bootstrap.unity` and drives the
> startup pipeline: register → preload → load next scene.

## Shape

```csharp
[DefaultExecutionOrder(-10000)]
public class BootstrapRunner : MonoBehaviour {
    [SerializeField] ServiceBootstrapSO _bootstrap;
    [SerializeField] string _nextScene;           // override of SO value
    [SerializeField] bool _dontDestroyOnLoad;
    [SerializeField] bool _preloadCatalogs;

    async void Awake(); // registers, preloads, loads next scene
}
```

The `async void Awake` is intentional — Unity does not await a `Task`
returned from `Awake`, so the only way to `await PreloadAllCatalogsAsync`
before calling `SceneManager.LoadScene` is this pattern. The body is
wrapped in `try/catch` to surface bootstrap exceptions.

## Execution order

`-10000` beats any gameplay `MonoBehaviour` (whose default order is 0),
so every downstream `Awake` runs with a fully hydrated [[ServiceLocator]].

## Dependencies

- **Uses:** [[ServiceBootstrapSO]], [[ServiceLocator]], Unity
  `SceneManager`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/Bootstrap/BootstrapRunner.cs`
- Siblings: `BootstrapHooks.cs`, `BootstrapLog.cs` (install + logging
  helpers).

## External references

- Setup: `docs/setup/Foundation#0005_CatalogsAndBootstrap.md`
- TECHNICAL.md: §1.1.2 BootstrapRunner
