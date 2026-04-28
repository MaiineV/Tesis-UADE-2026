---
title: CompositeMode
type: enum
domain: 26-PreConditions
status: done
tags: [preconditions, enum, boolean]
---

# CompositeMode

> Selects how a [[PCComposite]] folds its children: `And`, `Or`, or
> `Not` (NAND).

## Shape

```csharp
public enum CompositeMode {
    And = 0, // all children must pass (matches the group default)
    Or  = 1, // at least one child passes
    Not = 2, // negation of AND-fold (useful with a single child)
}
```

## Dependencies

**Used by:** [[PCComposite]]

## Code

`Assets/Scripts/Rollgeon/PreConditions/PCComposite.cs`
