---
title: FloorExitInteractable
type: system
domain: 07-Dungeon
status: done
tags: [dungeon, interaction, exit]
---

# FloorExitInteractable

> Script / prefab that represents the end-of-floor door. When the player
> interacts with it, it fires [[EventName]] `OnFloorExitRequested` which
> the [[ExplorationController]] consumes to advance the run.

## Behaviour

- Only interactable when the boss of the floor is defeated — gated by
  [[DungeonManager]]`.MarkCurrentRoomCleared`.
- Pushes the [[FloorTransitionScreen]] and calls
  [[IRunContextService]]`.AdvanceFloor`.
- Responsible for the "force door" skill-check variant (see
  `ActionId = "move.force_door"`) when the door is locked — consumes an
  [[ActionDefinitionSO]] via [[TurnManager]].

## Dependencies

- **Uses:** [[EventManager]], [[EventName]], [[DungeonManager]],
  [[IRunContextService]], [[ActionDefinitionSO]].
- **Used by:** exploration scene prefab, [[FloorTransitionScreen]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dungeon/FloorExitInteractable.cs`

## External references

- Setup: `docs/setup/UI#0013b_FloorTransitionScreen.md`
- TECHNICAL.md: §13.4 Floor exit
