---
title: ShopItemState
type: class
domain: 07-Dungeon
status: tbd
tags: [dungeon, state, shop]
---

# ShopItemState

> [[RoomObjectState]] stub for items reserved in a shop. No runtime
> consumer yet — declared to close the hierarchy and reserve the
> `Purchased` / `ReservedItemId` / `ReservedPrice` fields for the
> shop system.

## API / Shape

```csharp
[Serializable]
public class ShopItemState : RoomObjectState {
    public bool   Purchased;
    public string ReservedItemId;
    public int    ReservedPrice;
}
```

## Dependencies
**Uses:** [[RoomObjectState]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/State/RoomObjectState.cs`
