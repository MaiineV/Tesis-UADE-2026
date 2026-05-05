---
title: DiceSlotView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, dice]
---

# DiceSlotView

> Sub-view for a single die slot. Used in two contexts: build selection
> (shows die-type label) and combat (shows rolled face value, hold
> toggle). UI#0013a / T97c.

## Overview

In build selection, the parent calls `Bind(diceTypeName)` to label each
slot. In combat, [[DiceZoneView]] calls `ShowFace(int)` after a roll
and `SetHeld(bool)` to tint the background blue when the die is held;
`OnToggled` fires when the player clicks the slot.

## API / Shape

```csharp
public class DiceSlotView : MonoBehaviour {
    public UnityEvent OnToggled;

    public void Bind(string diceTypeName);
    public void ShowFace(int face);
    public void SetHeld(bool held);
    public void Clear();
}
```

Serialized: `_diceLabel`, `_button` (optional), `_background`
(optional `Graphic`).

## Dependencies

- **Used by:** [[DiceZoneView]], [[BuildSelectionScreen]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/DiceSlotView.cs`
