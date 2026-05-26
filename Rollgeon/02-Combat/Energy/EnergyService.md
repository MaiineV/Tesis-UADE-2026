---
title: EnergyService
type: service
domain: 02-Combat/Energy
status: done
tags: [combat, energy, service]
---

# EnergyService

> Runtime service that initialises, spends, and regenerates the player's
> [[Energy]] stat using values pulled from [[RulesetSO]]`.Energy`.

## API

```csharp
public sealed class EnergyService : IEnergyService, IPreloadableService, IDisposable {
    public int Priority => 50;                 // after catalogs / settings

    public void Register();
    public void ConfigureForTests(RulesetSO, AttributesManager);

    public void InitializeForEntity(Guid entityId);  // caches player id
    public bool SpendEnergy(Guid entityId, int cost);
    public void RegenerateAtTurnEnd(Guid entityId);
    public int  GetCurrent(Guid entityId);
    public int  GetMax(Guid entityId);
}
```

## Player-only regeneration

Subscribes to [[EventName]] `OnTurnFinished` but only regenerates if the
incoming `entityId` matches the cached `_playerId` (set by
`InitializeForEntity`). Enemies get their own energy economy when
Support / Boss behaviors need it — out of scope for Sprint 03.

## Events

On every value change: `OnEnergyChanged(entityId, current, max)` plus
`OnPlayerEnergyChanged` when the affected entity is the cached player.

## Dependencies

- **Uses:** [[AttributesManager]], [[Energy]] stat, [[RulesetSO]],
  [[EnergyRegenPolicy]], [[EventManager]].
- **Used by:** [[TurnManager]] (spend), [[RerollBudgetService]],
  combat HUD energy bar.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Energy/EnergyService.cs`
- Interface: `.../IEnergyService.cs`
- Tests: `.../Tests/EnergyServiceTests.cs`

## External references

- Setup: `docs/setup/System#0100a_EnergyAttributeAndRegen.md`
- TECHNICAL.md: §12.6 Energy economy
