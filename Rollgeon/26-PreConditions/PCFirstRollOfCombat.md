---
title: PCFirstRollOfCombat
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, gating, combat, first-roll]
---

# PCFirstRollOfCombat

> Passes when the entity in `OwnerGuid` has not yet resolved any roll
> since the last `OnCombatStart`.

## Overview

Backs class-passive effects like the Berserker "Primer golpe ×3". Reads
the per-combat tracker through `IFirstRollTracker`; if the service is
missing or `OwnerGuid` is empty, returns `false`.

## Configuration

No serialized fields — the predicate depends entirely on
`PreConditionContext.OwnerGuid`.

## Dependencies

**Uses:** [[BasePreCondition]], [[PreConditionContext]],
`IFirstRollTracker`
**Used by:** Class-passive [[EffectData]] groups (e.g. Berserker first-
strike multiplier — `CP_Warrior`).

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/PCFirstRollOfCombat.cs`
