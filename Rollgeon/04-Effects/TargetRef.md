---
title: TargetRef
type: class
domain: 04-Effects
status: done
tags: [effects, selection]
---

# TargetRef

> Reference to a single tile selection result — a `GridCoord`, plus
> any future metadata (highlight style, multi-cell shape) the system
> grows into. Used as the unit of selection in [[SelectionRequest]] and
> [[TargetSelectionResult]].

## API / Shape

```csharp
[Serializable]
public class TargetRef {
    public GridCoord Coord;

    public TargetRef(GridCoord coord);
    public static TargetRef At(GridCoord coord);
}
```

## Dependencies
**Uses:** `GridCoord`.
**Used by:** [[SelectionRequest]], [[TargetSelectionResult]],
[[SelectionSettings]].

## Code
`Assets/Scripts/Rollgeon/Effects/Selection/TargetRef.cs`
