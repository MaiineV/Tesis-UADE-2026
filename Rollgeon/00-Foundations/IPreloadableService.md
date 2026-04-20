---
title: IPreloadableService
type: interface
domain: 00-Foundations
status: done
tags: [foundation, bootstrap, service, interface]
---

# IPreloadableService

> Marker interface for runtime services that must be registered during
> [[Bootstrap]]. Lets [[ServiceBootstrapSO]] carry a polymorphic list of
> pre-instantiated services.

## Shape

```csharp
public interface IPreloadableService {
    void Register();
    int Priority => 0; // lower = earlier
}
```

## Contract

- `Register()` is the service's own responsibility — it typically calls
  `ServiceLocator.AddService<ISelfType>(this, ServiceScope.Global)` plus
  whatever internal wiring it needs.
- `Priority` breaks ties when multiple services in `ExtraServices` depend
  on registration order.

## Dependencies

- **Uses:** [[ServiceLocator]].
- **Used by:** every bootstrap-registered service (e.g. [[TurnManager]],
  [[ComboCountersService]], [[PhaseService]], [[PlayerService]],
  [[RerollBudgetService]], [[TurnOrderService]], [[WeaknessService]] —
  see Wave 3/4 notes).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/Bootstrap/IPreloadableService.cs`

## External references

- Setup: `docs/setup/Foundation#0005_CatalogsAndBootstrap.md`
- TECHNICAL.md: §1.1.1 IPreloadableService
