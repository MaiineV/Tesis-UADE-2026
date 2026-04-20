# Content#0011e — SampleFloorFactory

## Summary

`SampleFloorFactory` is a static factory that programmatically creates a complete
`FloorLayoutSO` graph (enemies, pools, rooms, layout) without requiring any
`.asset` files. Intended for EditMode tests and rapid prototyping.

## Created Assets (in-memory)

| Type | Count | Details |
|------|-------|---------|
| `EnemyDataSO` | 8 | 6 normal + 2 bosses (Dragon, Lich) |
| `EnemyPoolSO` | 3 | Cellar, Crypt, Cave |
| `RoomSO` | 5 | 3 Combat, 1 Shop, 1 Potion |
| `FloorLayoutSO` | 1 | RoomCount 8-12, all rooms + boss candidates |

## Usage

```csharp
using (var sample = SampleFloorFactory.Create())
{
    FloorLayoutSO layout = sample.Layout;
    // use layout for testing / prototyping
}
// all ScriptableObject instances are destroyed on Dispose
```

## Verification

1. Open Unity and let scripts compile (no errors expected).
2. Run EditMode tests: **Window > General > Test Runner > EditMode**.
3. Filter by `SampleFloorFactory` — all 14 tests should pass.

### Test List

| # | Test | Validates |
|---|------|-----------|
| 1 | `Create_ReturnsNonNullResult` | Factory returns valid result |
| 2 | `Create_LayoutHasExpectedRoomCounts` | Min=8, Max=12 |
| 3 | `Create_HasThreeCombatRooms` | 3 combat rooms with correct type |
| 4 | `Create_HasOneShopRoom` | 1 shop room |
| 5 | `Create_HasOnePotionRoom` | 1 potion room |
| 6 | `Create_HasTwoBossCandidates` | Dragon + Lich |
| 7 | `Create_CombatRoomsHaveEnemyPools` | All combat rooms have non-empty pools |
| 8 | `Create_SpecialRoomsHaveNullPools` | Shop/Potion rooms have null pools |
| 9 | `Create_AllRoomIdsAreUnique` | No duplicate room ids |
| 10 | `Create_AllEnemyIdsAreUnique` | No duplicate enemy EntityIds |
| 11 | `Create_AllCreatedListContainsAllInstances` | 17 total SOs tracked |
| 12 | `Create_DisposeDestroysAllObjects` | Dispose cleans up all SOs |
| 13 | `Create_BossCandidatesHaveHighHP` | Bosses have HP >= 50 |
| 14 | `Create_EnemyPoolsCanRoll` | All combat pools can roll spawns |

## Files

- `Assets/Scripts/Rollgeon/Dungeon/SampleFloorFactory.cs`
- `Assets/Scripts/Rollgeon/Dungeon/Tests/SampleFloorFactoryTests.cs`
