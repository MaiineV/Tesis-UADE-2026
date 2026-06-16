---
title: EnergyRegenPolicy
type: system
domain: 02-Combat/Energy
status: done
tags: [combat, energy, policy]
---

# EnergyRegenPolicy

> Pure function that computes the new Energy value after a turn-end
> regeneration tick.

## API

```csharp
public static class EnergyRegenPolicy {
    public static int ComputeNewCurrent(int current, int max, int regenBase);
    // = clamp(current + regenBase, 0, max)
}
```

Splitting the math out lets tests exercise edge cases (overflow, zero
regen, already-full) without spinning up [[EnergyService]] and
[[AttributesManager]].

## Dependencies

- **Used by:** [[EnergyService]]`.RegenerateAtTurnEnd`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Energy/EnergyRegenPolicy.cs`
- Tests: `.../Tests/EnergyRegenPolicyTests.cs`

## External references

- Setup: `docs/setup/System#0100a_EnergyAttributeAndRegen.md`
- TECHNICAL.md: §12.6 Regeneration
