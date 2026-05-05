---
title: SelectionTiming
type: enum
domain: 04-Effects
status: done
tags: [effects, selection, enum]
---

# SelectionTiming

> When the selection is resolved relative to [[EffectData]]
> `.TryExecute`. TECHNICAL §11.2. Expandable — new modes append
> without renumbering.

## Shape

```csharp
public enum SelectionTiming {
    /// <summary>Selection resolves BEFORE the dice roll.</summary>
    BeforeRoll = 0,
    /// <summary>Selection resolves AFTER the dice roll resolves.</summary>
    AfterRoll  = 1,
}
```

## Dependencies
**Used by:** [[SelectionSettings]], [[BaseEffect]],
[[ISelectionController]].

## Code
`Assets/Scripts/Rollgeon/Effects/Selection/SelectionTiming.cs`
