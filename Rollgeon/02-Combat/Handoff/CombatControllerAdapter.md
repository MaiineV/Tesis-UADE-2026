---
title: CombatControllerAdapter
type: service
domain: 02-Combat/Handoff
status: done
tags: [combat, handoff, adapter]
---

# CombatControllerAdapter

> Implementation of [[ICombatStarter]] that hides [[CombatController]]'s
> concrete construction from the handoff layer.

## Why an adapter

[[CombatHandoffService]] should not know how to build a
[[CombatTurnFSM]] nor which services to pull. The adapter takes that
responsibility:

- Resolves [[TurnOrderService]], [[TurnManager]], [[EnergyService]],
  [[IPlayerService]] from [[ServiceLocator]].
- Constructs [[CombatContext]] + [[CombatTurnFSM]] + [[CombatController]].
- Forwards `StartCombat(playerGuid, participants, roomInstanceId,
  aiHandler)` to the controller.

## Dependencies

- **Uses:** [[CombatController]], [[CombatContext]], [[CombatTurnFSM]],
  [[ServiceLocator]].
- **Used by:** [[CombatHandoffService]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Handoff/CombatControllerAdapter.cs`

## External references

- TECHNICAL.md: §12.0 Handoff adapter
