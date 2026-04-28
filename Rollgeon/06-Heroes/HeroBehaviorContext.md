---
title: HeroBehaviorContext
type: class
domain: 06-Heroes
status: done
tags: [heroes, behavior, context]
---

# HeroBehaviorContext

> [[BaseBehavior]] context subclass carrying hero-specific runtime
> data: dice result, matched combo, target guid, selection result, and
> the `EnergyPrepaid` flag.

## Overview

Passed into `HeroActionBehavior.Execute` to bridge dice/combo state and
target selection from the action handler into the effect pipeline.

## Shape

```csharp
public class HeroBehaviorContext : BehaviorContext {
    public IReadOnlyList<int>   DiceResult;
    public ComboDetectionResult? MatchedComboResult;
    public Guid                 TargetGuid;
    public TargetSelectionResult SelectionResult;
    public bool                 EnergyPrepaid;
}
```

## Dependencies

- **Uses:** `BehaviorContext`, `ComboDetectionResult`,
  `TargetSelectionResult`.
- **Used by:** [[HeroActionBehavior]]`.Execute`, action handlers that
  trigger hero behaviors.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Heroes/HeroBehaviorContext.cs`

## External references

- TECHNICAL.md: §4.3 Hero action behaviors
