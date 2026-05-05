---
title: IRerollBudgetService
type: interface
domain: 08-Dice
status: done
tags: [dice, reroll, service, energy, interface]
---

# IRerollBudgetService

> API publica del servicio de reroll budget. Superset del contrato
> `IRerollBudget` original (plan §4.3). TECHNICAL.md §6.5.

## Overview

Mantiene un unico budget activo (single-player, single-active-action)
con free rolls + paid rolls cargados a [[Energy]]. Las firmas reciben
`Guid playerGuid` para que el bookkeeping y los eventos lleven el
actor en el payload. `OnRerollStarted` se levanta despues del
bookkeeping — los handlers ven post-spend state. Implementaciones son
`IPreloadableService` y se registran via
`ServiceBootstrapSO.ExtraServices`.

## API / Shape

```csharp
public interface IRerollBudgetService {
    RerollBudget Current { get; } // null fuera de un budget activo

    event Action<RerollStartedPayload> OnRerollStarted;

    void StartBudget(ActionDefinitionSO action);  // throws si ya hay uno activo
    void EndBudget();                              // idempotente
    RerollQueryResult QueryExtraRoll(Guid playerGuid); // pure
    bool TryExtraRoll(Guid playerGuid);             // gasta free, sino energia
}

public readonly struct RerollStartedPayload {
    public readonly Guid PlayerGuid;
    public readonly ActionDefinitionSO Action;
    public readonly bool IsFree;
    public readonly int FreeRollsRemaining;
    public readonly int PaidRollsUsed;
}
```

## Reglas

- `FreeRollsRemaining = max(0, action.FreeRollCount - 1)` al
  `StartBudget` — el budget cuenta **re-rolls**, no la tirada inicial.
- `TryExtraRoll` consume free primero; si no hay y la accion permite
  energy-reroll, intenta `IEnergyService.SpendEnergy(1)`.
- `OnRerollStarted` solo se dispara si el reroll fue concedido.

## Dependencies

- **Uses:** [[RerollBudget]] (state), [[RerollQueryResult]],
  `ActionDefinitionSO`, `IEnergyService`.
- **Used by:** [[RerollBudgetService]] (default impl), `DiceRoller`
  flow (via adapter), CombatHUD reroll button, secondary action
  handlers (heal / force-door comparten budget — §12.5).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/IRerollBudgetService.cs`
- Default impl: [[RerollBudgetService]].

## External references

- Setup: `docs/setup/Feature#0104_EnergyReroll.md`
- TECHNICAL.md: §6.5 Reroll budget service contract
