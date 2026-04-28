---
title: IntComparison
type: enum
domain: 26-PreConditions
status: done
tags: [preconditions, enum, comparison]
---

# IntComparison

> Comparison operator for preconditions that test an `int` stat against a
> literal value.

## Shape

```csharp
public enum IntComparison {
    Equal          = 0, // ==
    NotEqual       = 1, // !=
    Less           = 2, // <
    LessOrEqual    = 3, // <=
    Greater        = 4, // >
    GreaterOrEqual = 5, // >=
}
```

## Dependencies

**Used by:** [[PCHasIntAttribute]]

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/IntComparison.cs`
