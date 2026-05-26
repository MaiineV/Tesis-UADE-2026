---
title: PCHasModifier
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, gating, attributes, modifiers]
---

# PCHasModifier

> Passes when the owner has at least `MinCount` matching `Modifier<T>`
> instances on the requested `AttributeType` (filters AND-combine).

## Overview

Used to gate effects on the presence of a buff / debuff stack — e.g.
"has my class skill applied" or "has at least 2 Intrinsic Energy
modifiers". If no extra filter is set, any modifier on the stack
counts. Reads through `AttributesManager`; missing entity or stat
evaluates `false`.

## Configuration

- `AttributeType` (`Type`, OdinSerialize) — stat whose modifier stack
  is inspected.
- `SourceIdString` (`string`) — when parseable to non-empty `Guid`,
  only modifiers with that `SourceId` count.
- `FilterByDirection` (`bool`) + `Direction` ([[ModifierDirection]]) —
  optional direction filter (`Intrinsic`, …).
- `MinCount` (`int`, ≥1) — minimum matching modifiers required.

## Dependencies

**Uses:** [[BasePreCondition]], [[PreConditionContext]],
[[AttributesManager]], [[Modifier]], [[ModifierDirection]]
**Used by:** [[EffectData]] groups gating on buff/debuff presence.

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/PCHasModifier.cs`
