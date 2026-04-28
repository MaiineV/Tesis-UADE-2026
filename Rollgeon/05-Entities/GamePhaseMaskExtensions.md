---
title: GamePhaseMaskExtensions
type: class
domain: 05-Entities
status: done
tags: [entities, behavior, extension, phase, static]
---

# GamePhaseMaskExtensions

> Static helpers to test whether a `GamePhase` is contained in a
> [[GamePhaseMask]]. Single point of decoding so callers dont
> hand-roll `(mask & GamePhaseMask.Combat) != 0` everywhere.
> TECHNICAL.md §7.2.

## Shape

```csharp
public static class GamePhaseMaskExtensions {
    public static bool Allows(this GamePhaseMask mask, GamePhase phase);
}
```

`Allows` returns `true` when:

- `phase == Exploration` and `mask` includes `Exploration`.
- `phase == Combat` and `mask` includes `Combat`.
- `phase == None` and `mask == None`.
- For `Loading` / `GameOver` (currently un-bit-mapped) returns `false`.

## Dependencies

- **Uses:** [[GamePhaseMask]], `GamePhase`.
- **Used by:** behavior dispatcher (gates `BaseBehavior.Execute`
  before invoking it), tests for phase-filtering behaviors.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/GamePhaseMask.cs`

## External references

- TECHNICAL.md: §7.2 Phase mask gating
