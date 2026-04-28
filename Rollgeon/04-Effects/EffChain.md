---
title: EffChain
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, chain]
---

# EffChain

> Concrete [[BaseEffect]] that runs an ordered list of [[ChainPhase]]
> sub-pipelines, each rolling its own dice budget and selection
> separately. The composition primitive behind multi-phase abilities.

## Overview

Selection-less itself — each phase configures its own per-effect
selection. Phases share the parent [[EffectContext]] so values written
to behavior bags are visible across the chain, but free rerolls /
energy carry through the *budget*, not the chain wrapper. A phase that
starts with zero rerolls and zero energy auto-terminates the chain.
Honors the §8.8 short-circuit: as soon as a phase sets
`context.lastResult = false`, EffChain stops.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public sealed class EffChain : BaseEffect {
    public List<ChainPhase> Phases = new();
    public int PhaseCount => Phases?.Count ?? 0;
}
```

## Dependencies
**Uses:** [[BaseEffect]], [[ChainPhase]], [[EffectData]],
[[EffectContext]], `PreConditionContext`.
**Used by:** advanced authored abilities, multi-step boss attacks.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffChain.cs`
