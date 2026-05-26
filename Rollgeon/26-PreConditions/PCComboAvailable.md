---
title: PCComboAvailable
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, gating, combos]
---

# PCComboAvailable

> Passes when the active hero's [[ContractSheet]] still has the combo
> identified by `ComboId` listed and not crossed off.

## Overview

Gates effects/behaviors that depend on a combo being legal in the
current run. Resolves the hero through `IPlayerService` (via
`ServiceLocator`); if the service is missing, the hero is null, the
sheet is null, or the id is unset/not found, the PC evaluates `false`.

## Configuration

- `ComboId` (`string`, required) — stable id of `BaseComboSO.ComboId`,
  must match the catalog entry.

## Dependencies

**Uses:** [[BasePreCondition]], [[PreConditionContext]],
`IPlayerService`, [[ContractSheet]], `BaseComboSO`
**Used by:** [[EffectData]] groups gating combo-specific effects.

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/PCComboAvailable.cs`
