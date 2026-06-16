---
title: SerializableObjectStates
type: class
domain: 07-Dungeon
status: done
tags: [dungeon, state, serialization]
---

# SerializableObjectStates

> Dict-like wrapper around polymorphic [[RoomObjectState]] entries that
> survives Unity serialization (TECHNICAL §13.6.1).

## Overview

Unity's serializer doesn't handle `Dictionary<string, TBase>` with
`[SerializeReference]` natively, so this type stores two parallel lists
— `_keys` and `_values` — index-aligned. The `[SerializeReference]` on
the values list preserves the concrete subtype across `JsonUtility`,
YAML, and Odin round trips, which is the prerequisite for a future
SaveService to dump runs to disk without a data migration.

Held by [[RoomInstance.ObjectStates]] and keyed by `SpawnPointId` (e.g.
`door_N`, `spawn_enemy_3`, `chest_0`).

### Invariant

`_keys.Count == _values.Count` and entries are aligned by index. The
public mutators (`Set`, `Remove`, `Clear`) maintain this invariant; do
not mutate the lists directly.

## API / Shape

```csharp
[Serializable]
public sealed class SerializableObjectStates {
    public int                                Count    { get; }
    public IReadOnlyList<string>              Keys     { get; }
    public IReadOnlyList<RoomObjectState>     Values   { get; }

    public void Set(string key, RoomObjectState value);
    public bool ContainsKey(string key);
    public bool TryGet(string key, out RoomObjectState value);
    public bool TryGet<T>(string key, out T value) where T : RoomObjectState;
    public bool Remove(string key);
    public void Clear();

    public IEnumerable<KeyValuePair<string, RoomObjectState>> Enumerate();
}
```

## Dependencies

**Uses:** [[RoomObjectState]] and its concrete subtypes ([[DoorState]],
[[EnemySpawnState]], [[ChestState]], [[PotionState]],
[[ShopItemState]]).

**Used by:** [[RoomInstance]], [[DungeonManager]], future SaveService.

## Code

`Assets/Scripts/Rollgeon/Dungeon/SerializableObjectStates.cs`

## External references

- TECHNICAL.md: §13.6.1 Per-object state.
