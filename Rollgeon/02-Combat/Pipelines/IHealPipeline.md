---
title: IHealPipeline
type: interface
domain: 02-Combat
status: done
tags: [combat, pipelines, heal, interface]
---

# IHealPipeline

> Contract for the centralized heal pipeline. All healing in the game
> flows through an implementation of this interface.

## Overview

One-method surface that hides outgoing/incoming heal multipliers,
percent-of-max resolution, max-HP clamping, Health write-back, and
event firing.

## API / Shape

```csharp
public interface IHealPipeline {
    HealContext Resolve(HealContext ctx);
}
```

## Dependencies
**Used by:** [[EffHeal]], [[SupportHealBehavior]], potion handlers.
**Implemented by:** [[HealPipeline]].

## Code
`Assets/Scripts/Rollgeon/Combat/Pipelines/IHealPipeline.cs`
