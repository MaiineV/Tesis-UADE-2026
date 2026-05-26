---
title: EffPassDoor
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, dungeon, door]
---

# EffPassDoor

> Concrete [[BaseEffect]] that crosses the player through whichever
> non-walled door of the current room is adjacent (Chebyshev ≤ 1) to
> the player's grid coordinate.

## Overview

Selection-less — direction is detected at runtime, mirroring the logic
of `PCAdjacentToDoor`. Iterates `DoorController` components in the
spawned room prefab, skips `Tapiada` (walled) doors and any direction
the room has no `Connections` entry for, and picks the first door
within Chebyshev distance 1 of the player's grid coordinate. Then
delegates the transition to [[IDungeonService]]`.EnterRoomByDoor`.
Returns `false` (no-op) when any required service is missing or no
adjacent door is found.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public sealed class EffPassDoor : BaseEffect { }
```

## Dependencies
**Uses:** [[BaseEffect]], [[EffectContext]] (`SourceGuid`),
`IGridManager`, [[IDungeonService]], `DoorController`,
`DoorVisualState`, `ServiceLocator`.
**Used by:** door-cross [[EffectData]] pipelines bound to room exit
input.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffPassDoor.cs`
