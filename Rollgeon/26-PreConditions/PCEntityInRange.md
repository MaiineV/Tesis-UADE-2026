---
title: PCEntityInRange
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, gating, grid, distance]
---

# PCEntityInRange

> Passes when `OpponentGuid` is within `MaxRange` tiles of `OwnerGuid`
> on the active grid, measured by the chosen [[DistanceMetric]].

## Overview

Used by attack/skill effects that have a melee or short-range
constraint. Reads positions through `IGridManager`; if either entity
isn't registered (room not loaded, entity has no position), it
evaluates `false`.

## Configuration

- `MaxRange` (`int`, ≥0) — inclusive tile range. `0` means same tile.
- `Metric` ([[DistanceMetric]]) — Manhattan (4-grid) or Chebyshev
  (8-grid). Default Manhattan.

## Dependencies

**Uses:** [[BasePreCondition]], [[PreConditionContext]],
`IGridManager`, [[DistanceMetric]]
**Used by:** [[EffectData]] groups gating range-bound effects.

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/PCEntityInRange.cs`
