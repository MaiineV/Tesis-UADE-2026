---
title: MinimapView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, minimap]
---

# MinimapView

> Renders a schematic of the current floor's room graph with the player
> marker at the current [[DungeonManager]]`.CurrentRoom`.

## Event binding

- Subscribes to `OnRoomChanged(roomInstanceId)` to move the player
  marker.
- Subscribes to `OnFloorChanged(runId, floorIndex)` to rebuild the
  graph.

## Dependencies

- **Uses:** [[DungeonManager]], [[FloorLayoutSO]], [[RoomType]] (icon
  by type), [[EventManager]].
- **Used by:** [[ExplorationHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/MinimapView.cs`

## External references

- TECHNICAL.md: §D Minimap
