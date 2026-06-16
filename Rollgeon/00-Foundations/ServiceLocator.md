---
title: ServiceLocator
type: service
domain: 00-Foundations
status: done
tags: [foundation, service, di, patterns]
---

# ServiceLocator

> Static typed `Type → instance` registry with two scopes (`Global` / `Run`)
> that every Rollgeon subsystem uses to find its collaborators.

## Purpose

Centralised, allocation-free DI. Systems ask for a dependency by interface
(`ServiceLocator.GetService<IPhaseService>()`) and get back the singleton
that [[Bootstrap]] wired up on scene load. `Run`-scoped services get
flushed by `ClearScope(Run)` between runs without touching `Global` infra.

## API

```csharp
public static class ServiceLocator {
    public static void AddService<T>(object instance, ServiceScope scope = ServiceScope.Global);
    public static T GetService<T>();                   // throws if missing
    public static bool TryGetService<T>(out T service); // defensive
    public static void RemoveService<T>();
    public static bool HasService<T>();
    public static void ClearScope(ServiceScope scope); // disposes IDisposable
    public static void Clear();                         // teardown only
}
```

Namespace is `Patterns` (not `Rollgeon.Patterns`). `ClearScope` disposes
any registered service that implements `IDisposable`.

## Dependencies

- **Uses:** [[ServiceScope]]
- **Used by:** essentially every service in the codebase — see
  [[IPreloadableService]], [[ServiceBootstrapSO]], [[EventManager]]
  (co-tenants), [[AttributesManager]], [[PhaseService]], [[RunController]],
  etc.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/ServiceLocator.cs`
- Tests: covered indirectly through every service-consuming test suite.

## External references

- Setup: `docs/setup/Foundation#0001_ServiceLocatorEventManager.md`
- TECHNICAL.md: §1.1 Base patterns — ServiceLocator
