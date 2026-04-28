---
title: FloorTransitionPayload
type: payload
domain: 12-UI/Screens
status: done
tags: [ui, screen, payload, floor]
---

# FloorTransitionPayload

> `IScreenPayload` passed to [[FloorTransitionScreen]] carrying the
> floor number and an optional title. UI#0013b.

## Shape

```csharp
public sealed class FloorTransitionPayload : IScreenPayload {
    public int FloorNumber;     // 1-based display number
    public string FloorTitle;   // optional, e.g. "Catacumbas Profundas"
}
```

## Overview

Plain DTO. The producer (floor / dungeon controller) increments
`FloorNumber` on each new floor and pushes the screen with this
payload. [[FloorTransitionScreen]] formats `Floor {FloorNumber}` and
appends `FloorTitle` when non-null.

## Dependencies

- **Uses:** [[IScreenPayload]].
- **Used by:** [[FloorTransitionScreen]].

## Code

- Runtime: `Assets/Scripts/Rollgeon/UI/Screens/FloorTransitionPayload.cs`
