---
title: EntityPawn
type: class
domain: 05-Entities
status: done
tags: [entities, visuals, monobehaviour, pawn]
---

# EntityPawn

> `MonoBehaviour` that lives on the GameObject prefab of an entity
> (hero / enemy / boss). Exposes the logical `Guid` and the API
> [[EntityVisualService]] uses to keep the pawn aligned with the
> grid as the entity moves.

## Overview

Placeholder-friendly: the FP uses primitives, the art layer can swap
prefabs later without touching the contract. Heroes are lifted by a
constant `HeroYOffset = 1.4f` so the model doesn't clip the floor /
grid; enemies stay at `Y=0`. May reference an optional
[[WorldSpaceHealthBar]] (`HealthBar` getter) — heroes typically leave
it null.

## API / Shape

```csharp
[AddComponentMenu("Rollgeon/Entities/Entity Pawn")]
public sealed class EntityPawn : MonoBehaviour {
    public WorldSpaceHealthBar HealthBar { get; }
    public Guid     EntityGuid { get; }
    public PawnKind Kind       { get; }

    public void Bind(Guid guid, PawnKind kind);
    public void SetWorldPosition(Vector3 world);
    public void SnapToGrid(IGridManager grid, GridCoord coord);

    public enum PawnKind { Hero, Enemy, Boss } // see [[PawnKind]]
}
```

## Dependencies

- **Uses:** [[PawnKind]] (nested enum), [[WorldSpaceHealthBar]],
  [[IGridManager]], `GridCoord`.
- **Used by:** [[EntityVisualService]] (spawn / despawn / move sync),
  [[IPawnRegistry]] / `PawnRegistryBinding`, [[IFeedbackService]]
  (anchor target).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Visuals/EntityPawn.cs`

## External references

- TECHNICAL.md: §17.I Visual entity layer
