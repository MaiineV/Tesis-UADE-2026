---
title: HealStrength
type: system
domain: 05-Entities
status: done
tags: [entities, stat, heal]
---

# HealStrength

> Stat (`int`) added by support enemies to scale their heals. Read by
> `SupportHealBehavior` and folded into the final heal amount via
> [[HealPipeline]].

## Shape

```csharp
public sealed class HealStrength : BaseAttribute<int> { ... }
```

A `BaseAttribute<int>` like [[Attack]] — lets [[ModifiableAttributes]]
treat it uniformly.

## Dependencies

- **Uses:** [[BaseAttribute]].
- **Used by:** `SupportHealBehavior`, [[EnemyDataSO]]`.BaseHealStrength`,
  HUD enemy panel (optional).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Entities/Behaviors/HealStrength.cs`

## External references

- TECHNICAL.md: §7.1 Support stats
