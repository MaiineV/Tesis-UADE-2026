---
title: ISaveable
type: interface
domain: 00-Foundations
status: tbd
tags: [foundation, save, interface, stub]
---

# ISaveable

> Stub contract for the future Save System. Exposed early so run-scoped
> services can implement it now and reconcile with the full
> implementation later.

## Shape

```csharp
public interface ISaveable {
    string SaveKey { get; }                // e.g. "run.combo_counter_state"
    object CaptureState();                 // dict / list / struct
    void RestoreState(object state);
}
```

## Status

**[STUB]** — the Save System itself is not implemented in Sprint 03. The
interface is published in `Patterns.Save` so consumers (e.g.
[[RunComboCounterState]]) can already declare intent. When the real Save
System worktree lands, its contract must match `SaveKey` / `CaptureState` /
`RestoreState` exactly.

> If another worktree stubs this interface concurrently, the first merge
> wins; the rest delete their copy during rebase.

## Dependencies

- **Used by:** [[RunComboCounterState]] and any other run-scoped state
  that wants to be save-ready from day one.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/Save/ISaveable.cs`

## External references

- TECHNICAL.md: §15 Save / persistence
