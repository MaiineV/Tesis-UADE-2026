---
title: EffMove
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, movement, grid]
---

# EffMove

> Concrete [[BaseEffect]] that moves the source entity to the
> coordinate selected via [[SelectionSettings]] /
> [[ISelectionController]].

## Overview

Reads `SelectionResult.FirstSelectedCoord` and delegates to
`IMovementService.Move(SourceGuid, coord)`. Returns `false` when
there's no coord selected or the movement service isn't registered;
otherwise returns whatever the service reports (true on a successful
move, false if the destination was rejected — out of range, occupied,
etc.).

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public sealed class EffMove : BaseEffect { }
```

The serialized state lives entirely in the inherited
[[SelectionSettings]] — there is no movement-specific field.

## Dependencies
**Uses:** [[BaseEffect]], [[EffectContext]] (`SelectionResult`,
`SourceGuid`), `IMovementService`, `IGridManager` (transitively),
`ServiceLocator`.
**Used by:** dash / reposition [[EffectData]] pipelines, AI move
behaviors.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffMove.cs`
