---
title: EffectContext
type: system
domain: 04-Effects
status: done
tags: [effects, context]
---

# EffectContext

> The bag of state passed into every [[IEffect]]`.Apply`. Carries source
> / target, the current `lastResult` for short-circuit, the effect index
> in the chain, selection results, and behavior refs for
> [[BehaviorValueKey]] writes.

## Key fields

```csharp
public class EffectContext {
    public bool   lastResult;        // short-circuit signal
    public int    EffectIndex;       // set by EffectData.Execute before each Apply
    public Guid   SourceGuid;
    public Guid   TargetGuid;
    public Entity SourceEntity;      // may be null at pipeline stubs
    public BaseBehavior SourceBehavior; // behavior that fired the effect
    public TargetSelectionResult SelectionResult;
    // ... misc capability fields, see IUsesSelection / IUsesValue markers
}
```

## Contract

- Callers seed `lastResult = true` before running the pipeline.
- `Execute` sets `EffectIndex` to the 0-based position of the current
  effect before invoking `Apply`.
- Effects must not silently mutate `lastResult = true`; only the sealed
  `BaseEffect.Apply` writes the return value back into it.

## Dependencies

- **Uses:** [[Entity]] (stub), [[BaseBehavior]] (stub),
  [[TargetSelectionResult]].
- **Used by:** [[BaseEffect]], [[EffectData]], every concrete effect.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Effects/EffectContext.cs`

## External references

- TECHNICAL.md: §8.2 EffectContext
