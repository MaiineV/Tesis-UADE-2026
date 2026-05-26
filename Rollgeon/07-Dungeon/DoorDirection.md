---
title: DoorDirection
type: enum
domain: 07-Dungeon
status: done
tags: [dungeon, doors, enum]
---

# DoorDirection

> Cardinal direction enum used to identify door slots and persist
> [[DoorState]] keys (`door_N`, `door_S`, `door_E`, `door_W`).

## Shape

```csharp
public enum DoorDirection {
    North = 0,
    South = 1,
    East  = 2,
    West  = 3,
}
```

`DoorDirectionExtensions` ships `Opposite()` and `DoorStateKey()`
helpers for connectivity resolution and state-dictionary lookups.

## Dependencies
**Used by:** [[DoorState]], [[DoorSlot]], [[DoorController]],
[[DungeonManager]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/Components/DoorSlot.cs`
