---
title: DamageSource
type: enum
domain: 04-Effects
status: done
tags: [effects, damage, enum]
---

# DamageSource

> Two-value enum used by [[EffDealDamage]] (and [[EffAddShield]]) to
> pick whether the base amount comes from a constant inspector field
> or from the matched combo's `BaseDamage`.

## Shape

```csharp
public enum DamageSource {
    Constant,
    ComboValue,
}
```

## Dependencies
**Used by:** [[EffDealDamage]], [[EffAddShield]].

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/DamageSource.cs`
