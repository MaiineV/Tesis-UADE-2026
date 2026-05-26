---
title: PCHasIntAttribute
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, gating, attributes]
---

# PCHasIntAttribute

> Passes when an owner stat (some `BaseAttribute<int>` like Energy or
> Health) compares against a literal `Value` under the chosen
> [[IntComparison]] operator.

## Overview

Most common gate for resource-checking effects ("has ≥ 1 Energy", "HP <
threshold"). Reads attributes through `AttributesManager`; if the entity
isn't registered or doesn't carry the requested type, evaluates `false`
— it's the behavior author's responsibility to wire the stat upstream.

## Configuration

- `AttributeType` (`Type`, OdinSerialize) — concrete subclass of
  `BaseAttribute<int>` (e.g. Energy, Health).
- `Comparison` ([[IntComparison]]) — operator. Default
  `GreaterOrEqual`.
- `Value` (`int`) — literal compared against the stat.
- `UseModifiedValue` (`bool`) — when true (default), compares against
  `ModifiedValue` (post-Intrinsic-stack); when false, against the raw
  `Value`.

## Dependencies

**Uses:** [[BasePreCondition]], [[PreConditionContext]],
[[AttributesManager]], `BaseAttribute<int>`, [[IntComparison]]
**Used by:** [[EffectData]] groups gating on resource thresholds.

## Code

`Assets/Scripts/Rollgeon/PreConditions/Concretes/PCHasIntAttribute.cs`
