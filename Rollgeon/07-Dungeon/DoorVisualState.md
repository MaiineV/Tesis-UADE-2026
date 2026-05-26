---
title: DoorVisualState
type: enum
domain: 07-Dungeon
status: done
tags: [dungeon, doors, enum]
---

# DoorVisualState

> Visual state enum that [[DoorController]] toggles meshes against.
> The lock semantics live in [[DungeonManager]] + the door
> interactable behaviors (§13.6 / §7.7); the controller only swaps
> meshes.

## Shape

```csharp
public enum DoorVisualState {
    Open             = 0, // walkable — room cleared or door Forced
    LockedCombat     = 1, // Isaac-lock during combat
    LockedSkillCheck = 2, // skill-check door, not yet forced
    Tapiada          = 3, // walled-off — no neighbour
}
```

## Dependencies
**Used by:** [[DoorController]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/Components/DoorController.cs`
