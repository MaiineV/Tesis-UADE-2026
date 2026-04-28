---
title: PhaseStackEntry
type: struct
domain: 09-Phase
status: done
tags: [phase, struct]
---

# PhaseStackEntry

> Read-only struct representing one frame of the [[PhaseService]]
> overlay stack: the base [[GamePhase]] active at push time plus the
> [[PhaseOverlay]] applied on top.

## Shape

```csharp
public readonly struct PhaseStackEntry {
    public readonly GamePhase    Base;
    public readonly PhaseOverlay Overlay;

    public PhaseStackEntry(GamePhase basePhase, PhaseOverlay overlay);
}
```

## Usage

`PhaseService` keeps an internal `Stack<PhaseStackEntry>`. `PushOverlay`
captures `CurrentBase` into the entry so `PopOverlay` can restore the
base phase even if base transitions happened while the overlay was
active.

## Dependencies

- **Uses:** [[GamePhase]], [[PhaseOverlay]].
- **Used by:** [[PhaseService]] (overlay stack).

## Code

`Assets/Scripts/Rollgeon/Phase/PhaseStackEntry.cs`

## External references

- TECHNICAL.md: §17.PHA Phase overlay stack
