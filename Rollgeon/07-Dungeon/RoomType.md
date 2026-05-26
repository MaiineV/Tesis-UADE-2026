---
title: RoomType
type: concept
domain: 07-Dungeon
status: done
tags: [dungeon, enum]
---

# RoomType

> Enum classifying each [[RoomSO]] into a gameplay bucket. Used by the
> handoff layer to pick the right encounter shape.

## Shape

```csharp
public enum RoomType {
    Combat,
    Boss,
    Shop,
    Potion,
    Rest,
    // ... extendable
}
```

## Heuristics

- `Combat` → default spawn count of 2 enemies.
- `Boss` → spawn count of 1 + uses [[BossFloorManagerSO]].
- `Shop` / `Potion` / `Rest` → no combat handoff; push a dedicated
  screen instead.

## Dependencies

- **Used by:** [[RoomSO]], [[CombatHandoffService]],
  [[DungeonManager]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dungeon/RoomType.cs`

## External references

- TECHNICAL.md: §13.1 RoomType
