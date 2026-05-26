---
title: TurnManager
type: service
domain: 02-Combat/Actions
status: done
tags: [combat, actions, action-economy]
---

# TurnManager

> Action economy gate. Single service that decides whether any given
> [[ActionDefinitionSO]] can run right now, charges the energy cost, and
> tracks which `ActionId`s have been spent in the current turn.

## Responsibilities

1. **Repetition constraint** (§12.6) — opt-in via
   `ActionDefinitionSO.BlockOnRepeat`.
2. **Energy cost** — charges via [[EnergyService]]`.SpendEnergy`.
3. **Ruleset override hook** — future `RulesetSO.ForbiddenActionIds`
   ([[RulesetSO]]); currently a stub returning `false`.

## API

```csharp
public sealed class TurnManager : IPreloadableService, IDisposable {
    public int Priority => 60;                  // after EnergyService (50)

    public void Register();                     // from ServiceBootstrapSO
    public void ConfigureForTests(IEnergyService, ActionCatalogSO, RulesetSO);

    public bool CanExecute(ActionDefinitionSO action, Guid playerGuid, out string reason);
    public bool TryExecute(ActionDefinitionSO action, Guid playerGuid, EffectContext ctx);

    public bool WasUsedThisTurn(string actionId);
    public int  UsedActionsCount { get; }
}
```

## Clear semantics

Subscribed to [[EventName]] `OnTurnStarted`; clears the used-action set
on every turn start. Because the TurnManager is global (not per-actor),
the player→enemy→player cycle resets the set twice per round — the
intent is "mine, during my active slot", which matches.

## Execution flow

`TryExecute` = `CanExecute` → `SpendEnergy` → optional
`EffectData.TryExecute` → mark used (if effect ok or no effect). If the
effect returns `false`, energy is already spent and the action is
**not** marked used.

## Dependencies

- **Uses:** [[IPreloadableService]], [[ServiceLocator]],
  [[EnergyService]] (as `IEnergyService`), [[ActionCatalogSO]],
  [[RulesetSO]], [[EventManager]], `EffectData`, `EffectContext`,
  `PreConditionContext`.
- **Used by:** [[CombatController]], player input handlers, combat HUD
  action buttons.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Actions/TurnManager.cs`
- Bootstrap: `.../TurnManagerBootstrap.cs`
- Tests: `.../Tests/TurnManagerTests.cs`

## External references

- Setup: `docs/setup/System#0100b_ActionEconomyRepetition.md`
- TECHNICAL.md: §12.6 Action economy
