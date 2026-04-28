---
title: DamageArgs
type: struct
domain: 04-Effects
status: done
tags: [effects, args, damage]
---

# DamageArgs

> Typed payload that [[EffDealDamage]] resolves and feeds into the
> [[DamagePipeline]]. Demonstrates the `TArgs` slot of
> `BaseEffect<TArgs, TValue>`.

## Overview

Currently a single-field struct (`BaseAmount`). Reserved for upcoming
fields the combat task already anticipates: `CritChance`, elemental
`DamageType`, `IgnoresArmor`, etc. Lives next to the effect that
resolves it.

## API / Shape

```csharp
[Serializable]
public struct DamageArgs {
    public int BaseAmount; // base damage before mitigation / crits
}
```

## Dependencies
**Used by:** [[EffDealDamage]], [[DamagePipeline]] /
[[DamageContext]].

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/DamageArgs.cs`
