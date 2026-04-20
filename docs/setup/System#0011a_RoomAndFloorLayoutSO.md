# System#0011a -- RoomSO + FloorLayoutSO

## Files created

| File | Purpose |
|------|---------|
| `Assets/Scripts/Rollgeon/Dungeon/RoomType.cs` | Enum: Start, Combat, Boss, Shop, Potion |
| `Assets/Scripts/Rollgeon/Dungeon/WeightedEntry.cs` | Generic serializable weighted-random entry |
| `Assets/Scripts/Rollgeon/Dungeon/EnemyPoolSO.cs` | ScriptableObject pool that rolls N enemies via weighted random (seeded `System.Random`) |
| `Assets/Scripts/Rollgeon/Dungeon/RoomSO.cs` | SerializedScriptableObject representing a single room (identity + enemy pool) |
| `Assets/Scripts/Rollgeon/Dungeon/FloorLayoutSO.cs` | SerializedScriptableObject defining a floor's room configuration and boss candidates |
| `Assets/Scripts/Rollgeon/Dungeon/Tests/Rollgeon.Dungeon.Tests.asmdef` | Test assembly definition |
| `Assets/Scripts/Rollgeon/Dungeon/Tests/EnemyPoolTests.cs` | 7 EditMode tests for EnemyPoolSO.RollForSpawns |

## How to create assets in Unity

### EnemyPoolSO

1. Right-click in Project window > **Create > Rollgeon > Dungeon > Enemy Pool**
2. Name it (e.g. `Floor1_CombatPool`)
3. Add entries: drag an `EnemyDataSO` into each `Item` slot, set `Weight` (higher = more likely to spawn)

### RoomSO

1. Right-click > **Create > Rollgeon > Dungeon > Room**
2. Fill in `RoomId` (unique string), `DisplayName`, and `Type` (Combat, Shop, etc.)
3. Assign an `EnemyPoolSO` to the `EnemyPool` field (Combat rooms need this; Shop/Potion can leave it null)

### FloorLayoutSO

1. Right-click > **Create > Rollgeon > Dungeon > Floor Layout**
2. Set `RoomCountMin` / `RoomCountMax` (how many rooms the floor generator will produce)
3. Populate `CombatRooms`, `ShopRooms`, `PotionRooms` lists with the appropriate `RoomSO` assets
4. Add boss `EnemyDataSO` references to `BossCandidates`

## How to run tests

1. Open Unity > **Window > General > Test Runner**
2. Select **EditMode** tab
3. Expand **Rollgeon.Dungeon.Tests > EnemyPoolTests**
4. Click **Run All** or run individual tests

All 7 tests validate `EnemyPoolSO.RollForSpawns`:
- Empty/zero-count guards
- Single entry always returns that enemy
- Count=N returns exactly N results
- High-weight entries dominate distribution (>70% with weight 90 vs 10)
- Zero-weight entries are never selected
- Deterministic seeding produces identical sequences

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `EnemyDataSO` not found | Ensure `Rollgeon.asmdef` compiles. `EnemyDataSO` lives in `Rollgeon.Entities`. |
| Odin errors on `RoomSO`/`FloorLayoutSO` | Verify Odin Inspector is installed. These inherit `SerializedScriptableObject`. |
| Tests not visible in Test Runner | Check that `Rollgeon.Dungeon.Tests.asmdef` has `UNITY_INCLUDE_TESTS` in defineConstraints and `Editor` in includePlatforms. |
| `WeightedEntry` fields not serializing | `WeightedEntry<T>` is `[Serializable]`. For polymorphic T, host SO should use `SerializedScriptableObject`. `EnemyPoolSO` uses plain `ScriptableObject` since `EnemyDataSO` is a direct Unity reference. |
