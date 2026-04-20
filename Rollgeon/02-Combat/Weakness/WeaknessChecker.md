---
title: WeaknessChecker
type: service
domain: 02-Combat/Weakness
status: done
tags: [combat, weakness, checker]
---

# WeaknessChecker

> Consults [[WeaknessRegistry]] to decide whether a given attack triggers
> a weakness multiplier, and returns that multiplier to [[DamagePipeline]].

## Shape

```csharp
public interface IWeaknessChecker {
    float GetMultiplier(Guid sourceId, Guid targetId, string comboId);
    // Returns 1f if no weakness or comboId mismatch.
}

public sealed class WeaknessChecker : IWeaknessChecker { ... }
```

## Behaviour

- Defaults to `1f` (no weakness) for every pair.
- If the target has an entry in [[WeaknessRegistry]] and the attack's
  `comboId` matches the registered weakness combo, returns the stored
  multiplier override (`> 1f` amplifies, `< 1f` resists).

## Dependencies

- **Uses:** [[WeaknessRegistry]].
- **Used by:** [[DamagePipeline]] (step 2 of `Resolve`).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/Weakness/WeaknessChecker.cs`
- Interface: `.../IWeaknessChecker.cs`
- Tests: `.../Tests/WeaknessCheckerTests.cs`

## External references

- Setup: `docs/setup/Content#0097b_WarriorContractAndWeakness.md`
- TECHNICAL.md: §7.2 Weakness checker
