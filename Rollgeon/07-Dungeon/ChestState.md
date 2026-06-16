---
title: ChestState
type: class
domain: 07-Dungeon
status: tbd
tags: [dungeon, state, chest]
---

# ChestState

> [[RoomObjectState]] stub for chests. No runtime consumer yet —
> declared to close the polymorphic hierarchy (§13.6.1) and avoid data
> migrations when chests land.

## API / Shape

```csharp
[Serializable]
public class ChestState : RoomObjectState {
    public bool         Opened;
    public List<string> LootRolled = new List<string>();
}
```

## Dependencies
**Uses:** [[RoomObjectState]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/State/RoomObjectState.cs`
