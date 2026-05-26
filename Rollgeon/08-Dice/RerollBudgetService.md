---
title: RerollBudgetService
type: service
domain: 08-Dice
status: done
tags: [dice, reroll, service, energy]
---

# RerollBudgetService

> Tracks the player's free-reroll count for the current action and
> charges additional rerolls to [[Energy]]. Registered globally by
> `RerollBudgetServiceBootstrap` ([[IPreloadableService]]).

## API

```csharp
public interface IRerollBudgetService {
    void       StartAction(ActionDefinitionSO action);
    RerollQueryResult Query();              // "can I reroll? how much?"
    bool       ConsumeReroll(Guid playerGuid); // spends free or pays energy
    int        RemainingFreeRolls { get; }
}
```

## Budget rules

- Initial budget per action = `ActionDefinitionSO.FreeRollCount - 1`.
  The first roll is "roll", the rest are "rerolls".
- `AllowsEnergyReroll` flag on the action unlocks paid rerolls after
  the free budget runs out — charge comes from [[EnergyService]].
- `RerollQueryResult` carries `Allowed`, `FreeRemaining`, `EnergyCost`
  for HUD display.

## Dependencies

- **Uses:** [[ActionDefinitionSO]], [[EnergyService]] (via
  `IEnergyService`), `RerollBudget` internal model,
  `RerollQueryResult` DTO, [[EventManager]] (publishes
  `OnRerollConsumed`).
- **Used by:** combat HUD reroll button, combo roll flow.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dice/RerollBudgetService.cs`
- Bootstrap: `.../RerollBudgetServiceBootstrap.cs`
- Budget: `.../RerollBudget.cs`
- Query result: `.../RerollQueryResult.cs`
- Tests: `.../Tests/RerollBudgetServiceTests.cs`,
  `.../Tests/RerollFlowTests.cs`

## External references

- Setup: `docs/setup/Feature#0104_EnergyReroll.md`
- TECHNICAL.md: §6 / §12.2 Reroll budget
