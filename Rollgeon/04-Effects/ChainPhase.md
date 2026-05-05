---
title: ChainPhase
type: class
domain: 04-Effects
status: done
tags: [effects, chain]
---

# ChainPhase

> Wrapper that pairs a label with an [[EffectData]] sub-pipeline,
> letting [[BaseBehavior]] / advanced effects composer split execution
> into named phases (e.g. "OnDeclare", "OnHit", "OnApply").

## Overview

Odin-serialized so each phase keeps its label visible in the inspector.
The label is authoring-only — runtime treats the phase as the inner
`Effects` pipeline.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class ChainPhase {
    public string     Label   = "Phase";
    public EffectData Effects = new EffectData();
}
```

## Dependencies
**Uses:** [[EffectData]].
**Used by:** advanced behaviors that author multi-phase pipelines.

## Code
`Assets/Scripts/Rollgeon/Effects/ChainPhase.cs`
