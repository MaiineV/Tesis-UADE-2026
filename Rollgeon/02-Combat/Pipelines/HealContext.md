---
title: HealContext
type: class
domain: 02-Combat
status: done
tags: [combat, pipelines, heal]
---

# HealContext

> Data object that travels through every stage of [[HealPipeline]]
> `.Resolve`. Callers populate the input fields; the pipeline fills the
> output fields.

## Overview

Mirrors [[DamageContext]] for the healing path. Supports flat and
percent-of-max heals, max-HP clamp tracking, and emits a
`HealResolvedPayload` `TypedEvent` once committed.

## API / Shape

```csharp
public class HealContext {
    // Inputs
    public Guid   SourceId;
    public Guid   TargetId;
    public int    BaseHeal;
    public string SourceTag;       // e.g. "potion", "support.heal"
    public bool   IsPercentOfMax;

    // Outputs
    public int  FinalHeal;
    public bool WasClamped;
}
```

## Dependencies
**Used by:** [[HealPipeline]], [[IHealPipeline]], [[EffHeal]].

## Code
`Assets/Scripts/Rollgeon/Combat/Pipelines/HealContext.cs`
