---
title: SlotState
type: enum
domain: 04-Effects
status: done
tags: [effects, selection, enum]
---

# SlotState

> Filter for which grid slots [[SelectionSettings]] considers valid:
> the owner's own tile, occupied tiles, empty tiles, or both
> occupied + empty.

## Shape

```csharp
public enum SlotState {
    Self     = 0,
    Occupied = 1,
    Empty    = 2,
    Both     = 3,
}
```

## Dependencies
**Used by:** [[SelectionSettings]].

## Code
`Assets/Scripts/Rollgeon/Effects/Selection/SlotState.cs`
