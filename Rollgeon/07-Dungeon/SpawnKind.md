---
title: SpawnKind
type: enum
domain: 07-Dungeon
status: done
tags: [dungeon, spawn, enum]
---

# SpawnKind

> Categorisation enum nested inside [[SpawnPoint]]. Tells consumers
> what kind of entity (or prop) the marker is for. Code path:
> `SpawnPoint.SpawnKind`.

## Shape

```csharp
public enum SpawnKind {
    Player,
    Enemy,
    NPC,
    Prop,
}
```

## Dependencies
**Used by:** [[SpawnPoint]].

## Code
`Assets/Scripts/Rollgeon/Dungeon/Components/SpawnPoint.cs`
