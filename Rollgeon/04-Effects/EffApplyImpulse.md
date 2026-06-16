---
title: EffApplyImpulse
type: class
domain: 04-Effects
status: done
tags: [effects, concrete, knockback, feedback]
---

# EffApplyImpulse

> Concrete [[BaseEffect]] that applies a knockback impulse by writing
> a `Vector3` under [[BehaviorValueKey]] `HitImpulse`. The downstream
> feedback / physics layer reads the value and physically moves the pawn.

## Overview

Atomic by design — the direction + magnitude are authored as a literal
`Vector3`; the effect does NOT derive direction from source→target.
Composition with a higher-level effect that reads grid positions is
fine, but it's not this effect's concern.

## API / Shape

```csharp
[Serializable, HideReferenceObjectPicker]
public class EffApplyImpulse : BaseEffect, IShouldStoreValuesOnBehavior {
    private Vector3 _impulse = new Vector3(0f, 0f, 1f);
}
```

## Dependencies
**Uses:** [[BaseEffect]], [[BehaviorValueKey]], `IGridManager`,
`ImpulseBehaviorValue`.
**Used by:** [[EffectData]] in attack pipelines that want knockback.

## Code
`Assets/Scripts/Rollgeon/Effects/Concretes/EffApplyImpulse.cs`
