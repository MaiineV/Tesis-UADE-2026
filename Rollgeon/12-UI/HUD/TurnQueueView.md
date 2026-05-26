---
title: TurnQueueView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, combat, turn-order]
---

# TurnQueueView

> Horizontal list that shows the order of the upcoming turns in combat.
> Backed by [[TurnOrderService]] via [[EventName]] `OnTurnQueueBuilt`.

## Behaviour

- `OnTurnQueueBuilt(snapshot, roundIndex)` → rebuild the row from the
  snapshot `IReadOnlyList<Guid>`.
- Per entry, instantiate a `TurnSlotView` prefab (portrait + tint for
  ally / enemy / player).
- Highlights [[TurnOrderService]]`.Current`.

## Design note

`OnTurnQueueBuilt` publishes a `ReadOnlyCollection` built on a copy —
safe to iterate without cloning again.

## Dependencies

- **Uses:** [[TurnOrderService]], [[EventManager]], `TurnSlotView`
  prefab.
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/TurnQueueView.cs`,
  `.../TurnSlotView.cs`
- Tests: `.../Tests/TurnQueueViewTests.cs`

## External references

- Setup: `docs/setup/UI#0095b_CombatHUD.md`
- TECHNICAL.md: §D / §12.7 Turn queue
