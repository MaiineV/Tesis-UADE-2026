---
title: TurnSlotView
type: view
domain: 12-UI/HUD
status: done
tags: [ui, hud, view, turn-order]
---

# TurnSlotView

> Single slot widget inside [[TurnQueueView]] — represents one round
> participant (player or enemy) with portrait, order index, and two
> overlays (active highlight, destroyed). Plan §3.2 / §4.3.

## Overview

Pure display — no event subscriptions. The parent [[TurnQueueView]]
listens to `OnTurnQueueBuilt`, `OnTurnStarted`, `OnDeath` and calls
`Bind`, `SetActive(bool)`, `SetDestroyed(bool)`, `SetPortrait(Sprite)`
on each slot.

Active highlight tints the portrait with `_highlightColor` and toggles
the `_activeHighlight` GameObject. Display index is 0-based on input
but rendered 1-based in the label (humans count from 1).

## API / Shape

```csharp
public class TurnSlotView : MonoBehaviour {
    public Guid SlotGuid { get; }
    public bool IsPlayer { get; }

    public void Bind(Guid slotGuid, bool isPlayer, int displayIndex);
    public void SetActive(bool isActive);
    public void SetDestroyed(bool destroyed);
    public void SetPortrait(Sprite portrait);
}
```

Serialized: `_portrait`, `_label`, `_activeHighlight`,
`_destroyedOverlay`, `_highlightColor`, `_idleColor`.

## Dependencies

- **Used by:** [[TurnQueueView]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/HUD/TurnSlotView.cs`
