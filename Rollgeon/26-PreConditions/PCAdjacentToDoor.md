---
title: PCAdjacentToDoor
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, gating, dungeon, doors]
---

# PCAdjacentToDoor

> Passes when `OwnerGuid` is within Chebyshev distance ≤ 1 (8-direction
> adjacency) of any non-tapiada door of the current room.

## Overview

Backs door-interaction effects (e.g. `EffPassDoor`). Reads
`IGridManager` for the player position and `IDungeonService` for the
current room's `DoorController`s. Tapiada doors and unconnected
directions are skipped; missing services / no position returns `false`
with a warning.

## Configuration

No serialized fields — context-driven only (`OwnerGuid`).

## Dependencies

**Uses:** [[BasePreCondition]], [[PreConditionContext]],
`IGridManager`, [[IDungeonService]], [[DoorController]],
[[DoorVisualState]]
**Used by:** Door-interaction [[EffectData]] groups (e.g.
[[EffPassDoor]]).

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/PCAdjacentToDoor.cs`
