# System#0011b -- DungeonManager

## Overview

`DungeonManager` handles procedural floor generation and room-by-room
navigation during a dungeon run. It implements `IDungeonService` and
registers itself in `ServiceScope.Run` so other systems can query the
current room, advance through the floor, and react to dungeon events.

## Prerequisites

| Dependency | Task |
|---|---|
| `ServiceLocator` | Foundation#0001 |
| `EventManager` | Foundation#0001 |
| `RoomSO`, `FloorLayoutSO` | System#0011a |
| `EnemyDataSO` | Foundation entities |
| `RunContext` (optional consumer) | Foundation#0010 |

## Files

| File | Purpose |
|---|---|
| `Assets/Scripts/Rollgeon/Dungeon/IDungeonService.cs` | Service interface |
| `Assets/Scripts/Rollgeon/Dungeon/DungeonManager.cs` | Sealed implementation |
| `Assets/Scripts/Rollgeon/Dungeon/Tests/DungeonManagerTests.cs` | 15 EditMode tests |

## Floor Generation Rules

`GenerateFloor(FloorLayoutSO layout, int seed)`:

1. **Room count** -- random between `layout.RoomCountMin` and
   `layout.RoomCountMax` (inclusive), clamped to a minimum of 3.
2. **Combat rooms** -- fills available slots by randomly picking from
   `layout.CombatRooms`.
3. **Shop room** -- one shop room inserted at a random middle position
   (if `layout.ShopRooms` is not empty).
4. **Potion room** -- one potion room inserted at a random middle
   position (if `layout.PotionRooms` is not empty).
5. **Boss room** -- a runtime `RoomSO` is created via
   `ScriptableObject.CreateInstance<RoomSO>()` from a random
   `layout.BossCandidates` entry, placed as the last room.
6. After generation, `_currentIndex` is set to 0 and `OnRoomEntered`
   fires.

All randomness uses `System.Random(seed)` for deterministic replays.

## Navigation

- `NextRoom()` -- advances to the next room and fires `OnRoomEntered`.
  Returns `false` and fires `OnFloorCleared` when already at the last
  room.
- `IsLastRoom` -- true only when standing on the final room (boss).
- `CurrentRoom` / `CurrentRoomIndex` -- read the active room state.

## Usage

### Via factory (recommended)

```csharp
// Inside RunBootstrapper or equivalent startup code:
var dungeon = DungeonManager.CreateAndRegister(floorLayout, seed);
```

This creates the manager, generates the floor, and registers it as
`IDungeonService` in `ServiceScope.Run`.

### Manual

```csharp
var dm = new DungeonManager();
dm.GenerateFloor(layout, seed);
ServiceLocator.AddService<IDungeonService>(dm, ServiceScope.Run);
```

### Querying from other systems

```csharp
var dungeon = ServiceLocator.GetService<IDungeonService>();
Debug.Log(dungeon.CurrentRoom.DisplayName);
```

## Cleanup

`DungeonManager` implements `IDisposable`. When `ServiceLocator.ClearScope(Run)`
runs, it automatically disposes the manager, destroying the runtime boss
`RoomSO` and clearing internal state.

## Verification

1. Create a `FloorLayoutSO` asset with at least 2 combat rooms, 1 shop
   room, 1 potion room, and 1 boss candidate.
2. Call `DungeonManager.CreateAndRegister(layout, 42)` from a test
   MonoBehaviour or bootstrap script.
3. Log `GetFloorRooms()` -- confirm the last room is type `Boss`, and
   shop/potion rooms appear in middle positions.
4. Call `NextRoom()` repeatedly -- confirm `OnRoomEntered` fires each
   time and `OnFloorCleared` fires at the end.
5. Run EditMode tests: 15 tests in `DungeonManagerTests` should pass.
