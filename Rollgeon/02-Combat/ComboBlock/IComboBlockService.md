---
title: IComboBlockService
type: interface
domain: 02-Combat
status: done
tags: [combat, combo-block, interface]
---

# IComboBlockService

> Run-scoped service that tracks combos blocked by the boss / floor
> manager. Each block has a turn-count duration that ticks down at the
> end of each player turn.

## Overview

Used by [[BossComboImmunityBehavior]] and the floor manager
(Content#0103) to neutralize specific combos for a window. `Block` is
idempotent on duplicate id (takes the max of the two durations) and
fires `OnComboBlocked`; `TickDuration` decrements all entries and
fires `OnComboUnblocked` for ones that hit zero. `Clear` is silent.

## API / Shape

```csharp
public interface IComboBlockService {
    void Block(string comboId, int durationTurns);
    bool IsBlocked(string comboId);
    int  GetRemainingTurns(string comboId);
    void TickDuration();
    void Clear();
    IReadOnlyDictionary<string, int> ActiveBlocks { get; }
}
```

## Dependencies
**Used by:** [[BossComboImmunityBehavior]], boss floor manager,
combat HUD.
**Implemented by:** [[ComboBlockService]].

## Code
`Assets/Scripts/Rollgeon/Combat/ComboBlock/IComboBlockService.cs`
