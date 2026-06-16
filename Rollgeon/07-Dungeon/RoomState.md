---
title: RoomState
type: enum
domain: 07-Dungeon
status: done
tags: [dungeon, state]
---

# RoomState

> Lifecycle state of a [[RoomInstance]] (TECHNICAL §13.6).

## Overview

Drives door gating — [[IDungeonService.CanEnterRoomByDoor]] only
returns `true` if the current room is `Cleared` or the relevant
[[DoorState]] is `Forced`. Combat sets the state back to `Uncleared`
visually until the encounter ends; once `Cleared`, doors stay open and
enemies don't respawn.

## API / Shape

```csharp
public enum RoomState {
    Uncleared = 0, // pending encounter — doors lock on entry
    Cleared   = 1, // resolved — doors open, no respawn
    Locked    = 2, // gated externally (boss key, scripted event, …)
}
```

## Dependencies

**Used by:** [[RoomInstance]], [[IDungeonService]], [[DungeonManager]],
[[ExplorationController]], [[CombatHandoffService]].

## Code

`Assets/Scripts/Rollgeon/Dungeon/RoomState.cs`

## External references

- TECHNICAL.md: §13.6 Isaac topology.
