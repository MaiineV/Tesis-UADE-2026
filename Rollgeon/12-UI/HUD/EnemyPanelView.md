---
title: EnemyPanelView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, combat, enemy]
---

# EnemyPanelView

> Panel showing the current target enemy's [[Health]], intent preview,
> status icons, and weakness tag.

## Event binding

- `OnAttributeChanged(entityId, typeof(Health))` with entity id == the
  `CombatHUDPayload.EnemyTargetGuid`.
- `OnWeaknessHit(sourceId, targetId, multiplier)` → brief tint /
  tooltip.
- `OnTurnStarted(entityId)` on the enemy → refresh intent preview.

## Dependencies

- **Uses:** [[AttributesManager]], [[EnemyDataSO]],
  [[WeaknessRegistry]] (for tags), [[EventManager]].
- **Used by:** [[CombatHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/EnemyPanelView.cs`
- Tests: `.../Tests/EnemyPanelViewTests.cs`

## External references

- Setup: `docs/setup/UI#0095b_CombatHUD.md`
- TECHNICAL.md: §D Enemy panel
