---
title: EnemyPoolSO
type: so
domain: 07-Dungeon
status: done
tags: [dungeon, so, pool, enemy]
---

# EnemyPoolSO

> Weighted pool of [[EnemyDataSO]] candidates for combat rooms. Supports
> per-entry `Weight` and deterministic sampling via a seeded
> `System.Random`.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Dungeon/EnemyPool")]
public class EnemyPoolSO : ScriptableObject {
    public List<WeightedEntry<EnemyDataSO>> Entries;
    public EnemyDataSO Sample(System.Random rng);
    public List<EnemyDataSO> SampleWithoutReplacement(int count, System.Random rng);
}
```

## Why weighted

Lets designers bias encounters without special-casing code (e.g. 70 %
grunts, 25 % elites, 5 % Auditor at the start; skewing later as floors
progress).

## Dependencies

- **Uses:** [[EnemyDataSO]], [[WeightedEntry]].
- **Used by:** [[RoomSO]], [[DefaultEnemySpawnResolver]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Dungeon/EnemyPoolSO.cs`
- Tests: `.../Tests/EnemyPoolTests.cs`

## External references

- TECHNICAL.md: §13.3 Enemy pool
