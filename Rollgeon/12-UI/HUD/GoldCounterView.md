---
title: GoldCounterView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, currency]
---

# GoldCounterView

> Renders the player's gold total in the exploration HUD.

## Event binding

Subscribes to `OnPlayerGoldChanged(entityId, newAmount)` fired by the
future shop / reward pipeline. Today the value is stubbed to 0 until
the economy layer lands.

## Status

`done` for the view itself (displays the value), but depends on the
economy system that is still **TBD** for Sprint 03. Expect a cleanup
commit once rewards land.

## Dependencies

- **Uses:** [[EventManager]], UnityUI `TMP_Text`.
- **Used by:** [[ExplorationHUDView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/GoldCounterView.cs`

## External references

- TECHNICAL.md: §D / §19 Gold / rewards
