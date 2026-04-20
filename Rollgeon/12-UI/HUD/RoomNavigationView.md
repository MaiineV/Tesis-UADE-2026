---
title: RoomNavigationView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, navigation]
---

# RoomNavigationView

> Renders navigation arrows / waypoints toward adjacent rooms during
> exploration. Forwards clicks to [[ExplorationController]]`.EnterRoom`.

## Behaviour

- Reads the adjacency info from [[DungeonManager]]`.CurrentRoom`.
- Each arrow button shows the destination room's type icon + gating
  state (cleared, locked, available).
- Disables arrows while the room is not yet cleared (combat rooms must
  be won first).

## Dependencies

- **Uses:** [[DungeonManager]], [[RoomSO]], [[RoomType]],
  [[ExplorationController]], Unity `Button`, `TMP_Text`.
- **Used by:** [[ExplorationHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/RoomNavigationView.cs`
- Tests: `.../Tests/RoomNavigationViewTests.cs`

## External references

- Setup: `docs/setup/UI#0011d_ExplorationScreen.md`
- TECHNICAL.md: §D Room navigation
