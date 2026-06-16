---
title: HealArgs
type: struct
domain: 04-Effects
status: done
tags: [effects, args, heal]
---

# HealArgs

> Typed payload that [[EffHeal]] resolves and pushes through the
> [[HealPipeline]]. Mirrors [[DamageArgs]] for symmetry.

## Overview

Single-field struct today (`BaseAmount`). The downstream combat task
extends this with `Overheal`, `HealsShield`, `CritChance`, etc.
without breaking existing authored heal effects.

## API / Shape

```csharp
[Serializable]
public struct HealArgs {
    public int BaseAmount; // base heal before modifiers
}
```

## Dependencies
**Used by:** [[EffHeal]], [[HealPipeline]].

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/HealArgs.cs`
