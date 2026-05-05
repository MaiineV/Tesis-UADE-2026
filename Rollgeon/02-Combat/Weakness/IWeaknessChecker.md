---
title: IWeaknessChecker
type: interface
domain: 02-Combat
status: done
tags: [combat, weakness, interface]
---

# IWeaknessChecker

> Service that, given a target and the matched `ComboId`, returns the
> weakness damage multiplier (or `1.0` if there is no weakness).

## Overview

Returns `1.0f` for the no-weakness case (target unknown, combo doesn't
match weakness, null/empty `comboId`, `Guid.Empty`). On a successful
weakness match the implementation also fires
`EventName.OnWeaknessHit` with `(attackerGuid, targetGuid)`.

## API / Shape

```csharp
public interface IWeaknessChecker {
    float GetMultiplier(Guid attacker, Guid target, string matchedComboId);
}
```

## Dependencies
**Uses:** [[IWeaknessRegistry]], `RulesetSO`, `EventManager`.
**Used by:** [[DamagePipeline]], any caller building a [[DamageContext]].
**Implemented by:** [[WeaknessChecker]].

## Code
`Assets/Scripts/Rollgeon/Combat/Weakness/IWeaknessChecker.cs`
