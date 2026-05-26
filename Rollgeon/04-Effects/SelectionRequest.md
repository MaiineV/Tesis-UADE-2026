---
title: SelectionRequest
type: class
domain: 04-Effects
status: done
tags: [effects, selection]
---

# SelectionRequest

> Mutable bundle that the effect dispatcher hands to
> [[ISelectionController]]`.BeginSelection`. Carries the
> [[SelectionSettings]], the pre-resolved valid targets, the owner's
> `Guid`, and an optional highlight style key.

## API / Shape

```csharp
public class SelectionRequest {
    public SelectionSettings Settings;
    public List<TargetRef>   ValidTargets;
    public Guid              OwnerGuid;
    public string            HighlightStyle;
}
```

## Dependencies
**Uses:** [[SelectionSettings]], [[TargetRef]].
**Used by:** [[ISelectionController]], [[BaseEffect]] dispatch.

## Code
`Assets/Scripts/Rollgeon/Effects/Selection/SelectionRequest.cs`
