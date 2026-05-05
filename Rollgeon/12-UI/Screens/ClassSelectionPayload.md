---
title: ClassSelectionPayload
type: payload
domain: 12-UI/Screens
status: done
tags: [ui, screen, payload, class-selection]
---

# ClassSelectionPayload

> Optional `IScreenPayload` for [[ClassSelectionScreen]]. The MVP flow
> pushes the screen with `OnPushed(null)`; this payload exists as an
> escape hatch for future callers that want to pre-select a class
> (e.g. "continue with the last hero played"). Plan §4.2.

## Shape

```csharp
public sealed class ClassSelectionPayload : IScreenPayload {
    public string PreSelectedClassId; // e.g. "hero.warrior"; null = no preselect
}
```

## Overview

Pure DTO. When non-null, [[ClassSelectionScreen]] highlights the row
matching `PreSelectedClassId`; otherwise the screen opens with no
selection and the player must click a class.

## Dependencies

- **Uses:** [[IScreenPayload]].
- **Used by:** [[ClassSelectionScreen]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/ClassSelectionPayload.cs`
