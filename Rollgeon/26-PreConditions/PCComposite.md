---
title: PCComposite
type: class
domain: 26-PreConditions
status: done
tags: [preconditions, gating, composite, boolean]
---

# PCComposite

> Boolean-composition precondition. Wraps a list of child
> [[BasePreCondition]]s and combines them under one of three modes
> (`And` / `Or` / `Not`), enabling expressions richer than the default
> AND fold of an [[EffectData]] group.

## Overview

The PC list inside an [[EffectData]] is hard-AND ([[BasePreCondition]]
`.EvaluateAll`). When an author needs OR or NOT, they drop a single
`PCComposite` into the group's list and populate its `Children`. This
keeps the group-level semantic dumb and the boolean tree fully data-
driven.

## Configuration

- `Mode` ([[CompositeMode]]) — `And` / `Or` / `Not`. Default `And`.
- `Children` (`List<BasePreCondition>`) — polymorphic via
  `[OdinSerialize] + [SerializeReference]`, drawn as a draggable list.

Empty-list semantics:

- `And` → `true` (vacuously).
- `Or`  → `false` (no one to approve).
- `Not` → `false` (NAND of empty AND).

`Not` is implemented as NAND — the AND of children, then negated.

## Dependencies

**Uses:** [[BasePreCondition]], [[PreConditionContext]],
[[CompositeMode]]
**Used by:** [[EffectData]] groups that need OR / NOT trees.

## Code

`Assets/Scripts/Rollgeon/PreConditions/PCComposite.cs`

## External references

- TECHNICAL.md: §8.1 / §8.2 PreConditions
