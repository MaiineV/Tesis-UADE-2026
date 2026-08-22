---
title: TurnManager
type: service
domain: 02-Combat/Actions
status: done
tags: [combat, actions, action-economy]
---

# TurnManager

> Action economy gate. Single service that decides whether any given
> [[ActionDefinitionSO]] can run right now and charges its roll cost.
> There is **no per-turn action limit**: as long as the roll pool has rolls,
> any action (movement included) can be repeated in the same turn.

## Responsibilities

1. **Roll cost** — 1 roll per direct action, charged via `IRollPoolService`.
2. **Ruleset override hook** — tutorial action gate today; future
   `RulesetSO.ForbiddenActionIds` ([[RulesetSO]]).

## API

```csharp
public sealed class TurnManager : IPreloadableService, IDisposable {
    public int Priority => 60;                  // after EnergyService (50)

    public void Register();                     // from ServiceBootstrapSO
    public void ConfigureForTests(IEnergyService, ActionCatalogSO, RulesetSO);

    public bool CanExecute(ActionDefinitionSO action, Guid playerGuid, out string reason);
    public bool TryExecute(ActionDefinitionSO action, Guid playerGuid, EffectContext ctx);
}
```

## Execution flow

`TryExecute` = `CanExecute` → spend 1 roll (in combat) → optional
`EffectData.TryExecute`. If the effect returns `false`, the roll is
already spent.

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
