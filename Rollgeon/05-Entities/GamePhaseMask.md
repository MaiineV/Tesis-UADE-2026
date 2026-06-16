---
title: GamePhaseMask
type: concept
domain: 05-Entities
status: done
tags: [entities, behavior, phase, enum]
---

# GamePhaseMask

> Flags-enum that lists which [[GamePhase]]s a [[BaseBehavior]] is
> allowed to fire in. AND-gated with [[BehaviorTrigger]] before
> `Execute` runs.

## Shape

```csharp
[Flags]
public enum GamePhaseMask {
    None        = 0,
    Exploration = 1 << 0,
    Combat      = 1 << 1,
    Loading     = 1 << 2,
    GameOver    = 1 << 3,
    All         = Exploration | Combat | Loading | GameOver,
}
```

## Typical values

- `Combat` → enemy in-combat behaviors (attack, buff, block).
- `Exploration` → NPC / prop behaviors (dialogue idle, trap pulse).
- `All` → cross-phase systems (rare).

## Dependencies

- **Uses:** [[GamePhase]].
- **Used by:** [[BaseBehavior]], behavior dispatchers.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/GamePhaseMask.cs`

## External references

- TECHNICAL.md: §7.2 Phase mask
