---
title: PotionState
type: class
domain: 07-Dungeon
status: tbd
tags: [dungeon, state, potions]
---

# PotionState

> [[RoomObjectState]] stub for ground-spawn potions. No runtime
> consumer yet — declared to close the hierarchy.

## API / Shape

```csharp
[Serializable]
public class PotionState : RoomObjectState {
    public bool Collected;
}
```

## Dependencies
**Uses:** [[RoomObjectState]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/State/RoomObjectState.cs`
