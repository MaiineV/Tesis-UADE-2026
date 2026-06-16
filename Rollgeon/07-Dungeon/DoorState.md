---
title: DoorState
type: class
domain: 07-Dungeon
status: done
tags: [dungeon, state, doors]
---

# DoorState

> [[RoomObjectState]] subtype tracking a single door's runtime status:
> direction, whether it was forced via skill check during combat, and
> whether it has been unlocked (room cleared).

## Overview

The combat Isaac-lock and the post-clear unlocked state both persist
between visits. `Forced = true` lets the player walk through a still-
locked door once via the skill-check path (TECHNICAL §13.6, §12 door
prefab example).

## API / Shape

```csharp
[Serializable]
public class DoorState : RoomObjectState {
    public DoorDirection Direction;
    public bool Forced;     // skill-check during combat
    public bool Unlocked;   // permanent after room clear
}
```

## Dependencies
**Uses:** [[RoomObjectState]], [[DoorDirection]].
**Used by:** [[DungeonManager]], [[DoorController]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/State/RoomObjectState.cs`
