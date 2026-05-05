---
title: EntityFilterMask
type: enum
domain: 04-Effects
status: done
tags: [effects, selection, enum]
---

# EntityFilterMask

> Flags-enum used by [[SelectionSettings]] (and future target queries)
> to decide which entity categories occupy a "valid" slot during
> selection.

## Overview

Resolved against the relationship `IEntityQueryService.GetRelationship`
returns for `(ownerGuid, occupantGuid)`. A slot passes the entity
filter when at least one bit overlaps.

## Shape

```csharp
[Flags]
public enum EntityFilterMask {
    None     = 0,
    Allies   = 1 << 0,
    Enemies  = 1 << 1,
    Neutrals = 1 << 2,
    Player   = 1 << 3,
    Props    = 1 << 4,
}
```

## Dependencies
**Used by:** [[SelectionSettings]], `IEntityQueryService`.

## Code
`Assets/Scripts/Rollgeon/Effects/Selection/EntityFilterMask.cs`
