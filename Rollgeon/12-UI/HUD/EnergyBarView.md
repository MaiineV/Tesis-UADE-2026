---
title: EnergyBarView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, energy]
---

# EnergyBarView

> Renders the player's current / max [[Energy]] as a slider plus a
> `current / max` label.

## Event binding

- Subscribes to [[EventName]] `OnPlayerEnergyChanged(entityId, current,
  max)` published by [[EnergyService]].
- Treats `args[0]` as `Guid`, `args[1]` and `args[2]` as `int` per the
  documented schema.

## Dependencies

- **Uses:** [[Energy]], [[EnergyService]], [[IPlayerService]],
  [[EventManager]].
- **Used by:** [[ExplorationHUDView]], [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/EnergyBarView.cs`

## External references

- Setup: `docs/setup/UI#0095a_ExplorationHUD.md`
- TECHNICAL.md: §D Energy HUD
