---
title: IRequiresTriggerContext
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, behaviors, interface]
---

# IRequiresTriggerContext

> Generic marker declaring that the effect consumes a
> `BehaviorContext` of subtype `TCtx`. Unlike the other capability
> interfaces, this one carries a type parameter to tie an effect to a
> specific trigger payload.

## Overview

Authoring-side: the inspector cross-references `TCtx` with the
container [[BaseBehavior]]'s trigger and shows a soft warning (orange)
if they don't match — see TECHNICAL.md §8.5.
Runtime: the effect calls `EffectContext.TryGetTriggerContext<TCtx>`,
which returns `false` when the actual trigger context type differs.
Effects typically `return false` in that case to short-circuit the
[[EffectData]] chain (§8.8).

## API / Shape

```csharp
public interface IRequiresTriggerContext<TCtx> where TCtx : BehaviorContext { }
```

## Dependencies
**Uses:** `Rollgeon.Entities.Behaviors.BehaviorContext`,
[[EffectContext]] (`TryGetTriggerContext<T>`).
**Used by:** effects bound to a specific behavior trigger payload.

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
