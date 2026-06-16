---
title: ShieldArgs
type: struct
domain: 04-Effects
status: done
tags: [effects, args, shield]
---

# ShieldArgs

> Typed payload that [[EffAddShield]] resolves when adding `Shield`
> stat points to a target.

## Overview

Single-field struct today (`BaseAmount`). Kept separate from
[[DamageArgs]] / [[HealArgs]] so future shield-only fields (e.g.
`DecayTurns`, `MaxStack`, `IgnoresShieldCap`) don't pollute damage /
heal payloads.

## API / Shape

```csharp
[Serializable]
public struct ShieldArgs {
    public int BaseAmount;
}
```

## Dependencies
**Used by:** [[EffAddShield]].

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/ShieldArgs.cs`
