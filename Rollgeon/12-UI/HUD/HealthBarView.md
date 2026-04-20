---
title: HealthBarView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, exploration]
---

# HealthBarView

> Exploration / combat HUD sub-view that renders the player's current
> [[Health]] as a slider + numeric label.

## Event binding

- Subscribes to [[EventName]] `OnAttributeChanged(entityId, Type)` and
  filters on `Type == typeof(Health)` and `entityId ==
  IPlayerService.PlayerGuid`.
- Reads new value via [[AttributesManager]]`.GetAttributeValue<Health,int>`.

## Dependencies

- **Uses:** [[Health]], [[AttributesManager]], [[IPlayerService]],
  [[EventManager]], Unity `Slider` + `TMP_Text`.
- **Used by:** [[ExplorationHUDView]], [[CombatHUDView]]
  (as the player row inside [[EnemyPanelView]] counterpart).

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/HealthBarView.cs`

## External references

- Setup: `docs/setup/UI#0095a_ExplorationHUD.md`
- TECHNICAL.md: §D HealthBarView
