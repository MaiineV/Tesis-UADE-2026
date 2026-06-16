---
title: EffForceDoor
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, dungeon, door]
---

# EffForceDoor

> Concrete [[BaseEffect]] that tries to force a locked door using the
> sum of the current dice roll. On success it sets `DoorState.Forced`
> and crosses to the neighbouring room.

## Overview

Reads the combat dice from `EffectContext.DiceResult` and compares
their sum to `RequiredValue`. Below the threshold or with no roll
available, returns `false` and short-circuits the chain. Above it,
flips `DoorState.Forced` on the room's `ObjectStates` so the door
remembers it was forced, then delegates the actual room transition to
[[IDungeonService]]`.EnterRoomByDoor`.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public sealed class EffForceDoor : BaseEffect {
    public DoorDirection Direction = DoorDirection.North;
    [Min(1)] public int RequiredValue = 10;
}
```

## Dependencies
**Uses:** [[BaseEffect]], [[EffectContext]] (`DiceResult`),
[[IDungeonService]], `DoorDirection`, `DoorState`,
`ObjectStates`, `ServiceLocator`.
**Used by:** door-interaction [[EffectData]] pipelines on locked doors.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffForceDoor.cs`
