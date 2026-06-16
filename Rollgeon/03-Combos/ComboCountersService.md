---
title: ComboCountersService
type: service
domain: 03-Combos
status: done
tags: [combos, counters, service, balatro]
---

# ComboCountersService

> Balatro-style combo counter system. Increments a per-combo counter
> every time a combo matches during a run and exposes a bonus multiplier
> computed from [[ComboCountersConfig]].

## API

```csharp
public interface IComboCountersService {
    int   GetCount(string comboId);
    void  IncrementCount(string comboId);
    float GetBonusMultiplier(string comboId);
}
```

## Lifecycle

- Global [[IPreloadableService]] with `Priority = 80` (after Energy /
  Turn / Reroll).
- Registers itself under `IComboCountersService` and subscribes to:
  - [[EventName]] `OnRunStart` → instantiate a fresh
    [[RunComboCounterState]] in `ServiceScope.Run`.
  - `OnRunEnd` → no-op (state is freed by `ClearScope(Run)`).
  - [[TypedEvent]]`<ComboMatchedPayload>` → bump the matching counter
    and fire `OnComboCounterIncremented`.
- Out of run (main menu, class preview), `GetCount` returns 0 and
  `GetBonusMultiplier` returns `1f`.

## Dependencies

- **Uses:** [[RunComboCounterState]], [[ComboCountersConfig]] (via
  [[RulesetSO]]`.Counters`), [[EventManager]], [[EventName]],
  [[TypedEvent]], [[ServiceLocator]].
- **Used by:** `AttackResolver` (future), combat HUD counter view,
  boss behaviors that react to counter thresholds.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Counters/ComboCountersService.cs`
- Interface: `.../IComboCountersService.cs`
- Bootstrap: `.../ComboCountersServiceBootstrap.cs`
- Tests: `.../Tests/ComboCountersServiceTests.cs`

## External references

- Setup: `docs/setup/Content#0097c_ComboCountersAndStrike.md`
- TECHNICAL.md: §5.5 Combo counters
