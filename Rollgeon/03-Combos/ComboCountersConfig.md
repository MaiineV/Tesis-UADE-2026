---
title: ComboCountersConfig
type: config
domain: 03-Combos
status: done
tags: [combos, counters, config, balance]
---

# ComboCountersConfig

> Balance config for the Balatro-style counter multiplier. Lives inside
> [[RulesetSO]]`.Counters`.

## Role

Turns an integer `count` into a float bonus multiplier via
`ComputeMultiplier(count)`. The exact curve (linear, diminishing, tiered
thresholds) is tuneable in the inspector by designers.

## Dependencies

- **Uses:** Odin inspector for serialised curve / threshold data.
- **Used by:** [[ComboCountersService]]`.GetBonusMultiplier`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Combos/Counters/ComboCountersConfig.cs`
- Tests: `.../Tests/ComboCountersConfigTests.cs`

## External references

- Setup: `docs/setup/Content#0097c_ComboCountersAndStrike.md`
- TECHNICAL.md: §5.5 Counter formula
