---
title: ISaveable
type: interface
domain: 00-Foundations
status: done
tags: [foundation, save, interface]
---

# ISaveable

> Persistence contract consumed by [[SaveSystem]] (§15). Saveables register on
> creation and unregister on dispose; the system captures/restores through
> this contract.

## Shape

```csharp
public interface ISaveable {
    string SaveKey { get; }                // e.g. "run.combo_counter_state"
    object CaptureState();                 // dict / list / struct
    void RestoreState(object state);
}
```

## Status

**Done (2026-07-06)** — the real [[SaveSystem]] landed and consumes this
contract. Non-trivial rehydration (e.g. contained `Modifier<T>`, §3.5) is the
saveable's responsibility inside `RestoreState`.

## Dependencies

- **Consumed by:** [[SaveSystem]].
- **Implemented by:** `RunContext`, `InventoryService` (+ `InventoryState` as
  converter), [[RunComboCounterState]], [[RunUnlockState]],
  `MetaProgressionState` (persists via its own store, excluded from run save),
  `AudioManager`. Run-scoped implementers also implement `IDisposable` →
  `SaveSystem.Unregister`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/Save/ISaveable.cs`

## External references

- TECHNICAL.md: §15 Save / persistence
