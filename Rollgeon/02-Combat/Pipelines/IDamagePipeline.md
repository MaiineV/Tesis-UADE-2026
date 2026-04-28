---
title: IDamagePipeline
type: interface
domain: 02-Combat
status: done
tags: [combat, pipelines, damage, interface]
---

# IDamagePipeline

> Contract for the centralized damage pipeline (TECHNICAL §12.2). Every
> hit in the game flows through an implementation of this interface.

## Overview

Single entry point for damage resolution. Hides outgoing/incoming
multipliers, weakness, shield absorption, Health write-back, and event
firing behind a one-method surface so callers (effects, behaviors, AI)
don't reimplement the math.

## API / Shape

```csharp
public interface IDamagePipeline {
    DamageContext Resolve(DamageContext ctx);
}
```

## Dependencies
**Used by:** [[EffDealDamage]], [[BossAttackBehavior]], any effect /
behavior that deals damage.
**Implemented by:** [[DamagePipeline]].

## Code
`Assets/Scripts/Rollgeon/Combat/Pipelines/IDamagePipeline.cs`
