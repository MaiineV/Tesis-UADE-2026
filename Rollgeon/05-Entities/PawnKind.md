---
title: PawnKind
type: enum
domain: 05-Entities
status: done
tags: [entities, visuals, enum, pawn]
---

# PawnKind

> Nested enum on [[EntityPawn]] that classifies a pawn as `Hero`,
> `Enemy` or `Boss`. Drives prefab selection in [[EntityVisualService]]
> and the per-kind Y-offset / health-bar conventions on the pawn.

## Shape

```csharp
public enum PawnKind { Hero, Enemy, Boss } // EntityPawn.PawnKind
```

## Why nested

Lives inside [[EntityPawn]] so callers always reference it as
`EntityPawn.PawnKind` — emphasizes that it describes "the kind of
pawn" specifically, not a generic entity classification. The logical
side ([[Entity]], [[EnemyDataSO]]) carries its own taxonomy decoupled
from the visual layer.

## Dependencies

- **Used by:** [[EntityPawn]]`.Kind`, [[EntityVisualService]]
  (`SpawnHero` -> `Hero`, `SpawnEnemy` -> `Enemy` / `Boss` based on
  `BaseHP` heuristic).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Visuals/EntityPawn.cs`
  (nested in `EntityPawn`).

## External references

- TECHNICAL.md: §17.I Visual entity layer
