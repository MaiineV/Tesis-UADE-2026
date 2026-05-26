---
title: RunComboCounterState
type: system
domain: 03-Combos
status: done
tags: [combos, counters, state, saveable]
---

# RunComboCounterState

> Per-run counter storage backing [[ComboCountersService]]. Flat
> `Dictionary<string, int>` registered in [[ServiceScope]] `Run` so it
> resets on run boundaries.

## Shape

```csharp
public sealed class RunComboCounterState : ISaveable {
    public int Get(string comboId);
    public int Increment(string comboId); // returns new count

    // ISaveable stub — reconciled with full SaveSystem later.
    public string SaveKey => "run.combo_counter_state";
    public object CaptureState();
    public void   RestoreState(object state);
}
```

## Save-readiness

Implements [[ISaveable]] today even though the Save System itself is
TBD. When it lands, runs will be able to resume counters mid-encounter
without special migration.

## Dependencies

- **Uses:** [[ISaveable]].
- **Used by:** [[ComboCountersService]], tests
  (`RunComboCounterStateTests`).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Counters/RunComboCounterState.cs`
- Tests: `.../Tests/RunComboCounterStateTests.cs`

## External references

- TECHNICAL.md: §5.5 / §15 Counter state + save hook
