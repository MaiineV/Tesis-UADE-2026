---
title: TargetSelectionResult
type: class
domain: 04-Effects
status: done
tags: [effects, selection]
---

# TargetSelectionResult

> Output of an [[ISelectionController]] selection cycle. Carries the
> selected targets and three orthogonal flags so callers can
> distinguish completion vs cancel vs skip.

## API / Shape

```csharp
public class TargetSelectionResult {
    public bool            WasCompleted;
    public bool            WasCancelled;
    public bool            WasSkipped;
    public List<TargetRef> SelectedTargets;

    public GridCoord? FirstSelectedCoord { get; }
    public bool TryGetFirstSelectedCell(out GridCoord cell);
}
```

## Dependencies
**Uses:** [[TargetRef]], `GridCoord`.
**Used by:** [[ISelectionController]], [[EffectContext]],
[[SelectionSettings]], every concrete effect that targets entities.

## Code
`Assets/Scripts/Rollgeon/Effects/Selection/TargetSelectionResult.cs`
