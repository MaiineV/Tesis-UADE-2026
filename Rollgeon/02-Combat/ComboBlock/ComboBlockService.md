---
title: ComboBlockService
type: service
domain: 02-Combat/ComboBlock
status: done
tags: [combat, combos, block]
---

# ComboBlockService

> Enforces per-run bookkeeping that decides whether a given combo can be
> played right now. Bridges [[ContractSheet]] activation state to the
> combat HUD's combo buttons.

## Responsibilities

- Hold the set of combo ids **blocked** for the current run or combat
  (e.g. via status effect, boss ability, or contract activation rules).
- Surface `IsBlocked(comboId)` for the HUD to gray out buttons.
- Accept `Block(comboId)` / `Unblock(comboId)` from effects and
  behaviors.

## Scope

Registered globally (survives between combats) but flushed on
[[EventName]] `OnRunEnd` so the next run starts clean.

## Dependencies

- **Uses:** `IComboBlockService` interface, [[EventManager]] for lifecycle.
- **Used by:** [[ContractSheet]], combo action buttons, boss passive
  behaviors.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combat/ComboBlock/ComboBlockService.cs`
- Interface: `.../IComboBlockService.cs`
- Tests: `.../Tests/ComboBlockServiceTests.cs`

## External references

- TECHNICAL.md: §5.5 Combo block
