---
title: CombatController
type: service
domain: 02-Combat/FSM
status: done
tags: [combat, fsm, controller]
---

# CombatController

> Facade that owns a [[CombatTurnFSM]] instance plus the outside-world
> integration points (start / end / HUD freeze). Scene scripts and
> handoff services talk to this class, not directly to the FSM.

## Responsibilities

- Build a [[CombatContext]] from [[ServiceLocator]] dependencies
  ([[TurnOrderService]], [[TurnManager]], [[EnergyService]],
  [[IPlayerService]]).
- Wrap `CombatTurnFSM` lifecycle (`StartCombat`, `NotifyCombatEnded`).
- Surface `OnFinished` so [[CombatReturnService]] can route the outcome
  back to exploration / victory / defeat UI.
- Freeze-gate policy: ignore inputs after `CombatEnded` until the next
  combat starts (verified by `CombatControllerFreezeTests`).

## Dependencies

- **Uses:** [[CombatTurnFSM]], [[CombatContext]], [[ServiceLocator]],
  [[EventManager]].
- **Used by:** [[CombatHandoffService]], [[CombatControllerAdapter]],
  combat HUD scripts.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/FSM/CombatController.cs`
- Tests: `.../Tests/CombatControllerFreezeTests.cs`

## External references

- TECHNICAL.md: §12.1 Combat controller
