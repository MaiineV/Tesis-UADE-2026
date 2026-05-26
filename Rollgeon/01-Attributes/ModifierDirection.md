---
title: ModifierDirection
type: concept
domain: 01-Attributes
status: done
tags: [attributes, modifier, enum]
---

# ModifierDirection

> Direction label on every [[Modifier]]. Determines which pipeline reads
> it: intrinsic (raw stat), outgoing (source role), incoming (target
> role).

## Shape

```csharp
public enum ModifierDirection {
    Outgoing,   // applies when entity is the SOURCE (dealing damage/heal)
    Incoming,   // applies when entity is the TARGET (receiving damage/heal)
    Intrinsic,  // always applies to the stat itself (e.g. +10 Max HP)
}
```

## Who reads what

- `Intrinsic` → [[BaseAttribute]]`.ComputeModifiedValue` (and therefore
  `IModifiable.ModifiedValue`).
- `Outgoing` → [[DamagePipeline]] step 1 (attacker's outgoing multipliers)
  and analogous heal-outgoing steps.
- `Incoming` → [[DamagePipeline]] step 3 (defender's incoming
  multipliers, after weakness).

## Dependencies

- **Used by:** [[Modifier]], [[BaseAttribute]], [[DamagePipeline]],
  [[HealPipeline]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/Modifiers/ModifierDirection.cs`

## External references

- TECHNICAL.md: §3.2 Modifier direction
