---
title: PoolOfferingRow
type: behavior
domain: 12-UI
status: done
tags: [ui, screens, build-selection, mono-behaviour]
---

# PoolOfferingRow

> One row of the pool inside [[BuildSelectionScreen]]: a die type, a
> `current / max` count, and `+` / `-` buttons that emit add / remove
> events back to the screen.

## Overview

The screen owns the logic; the row only displays the type, the count,
and emits click events. `Refresh(currentCount, bagHasRoom)` is called
by the screen to disable `+` when the bag is full or `-` when the
count is zero.

## API / Shape

```csharp
public class PoolOfferingRow : MonoBehaviour {
    public DiceType Type     { get; }
    public int      MaxInBag { get; }

    public event Action<DiceType> OnAddRequested;
    public event Action<DiceType> OnRemoveRequested;

    public void Bind(DiceType type, int maxInBag);
    public void Unbind();
    public void Refresh(int currentCount, bool bagHasRoom);
}
```

## Dependencies
**Uses:** `DiceType`, TextMeshPro, Unity UI.
**Used by:** [[BuildSelectionScreen]].

## Code
`Assets/Scripts/Rollgeon/UI/Screens/PoolOfferingRow.cs`
