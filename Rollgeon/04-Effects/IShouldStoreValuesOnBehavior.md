---
title: IShouldStoreValuesOnBehavior
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, behaviors, interface]
---

# IShouldStoreValuesOnBehavior

> Marker declaring that the effect writes values into the runtime bag
> of its source [[BaseBehavior]] (TECHNICAL.md §9), keyed by
> [[BehaviorValueKey]].

## Overview

Pure marker — no members. Used by the feedback layer to know that an
effect publishes a numeric outcome (e.g. `FloatingDamage`,
`FloatingHeal`, `FloatingShield`) the feedback step can read back
from the behavior bag.

## API / Shape

Marker — no members.

## Dependencies
**Uses:** [[BehaviorValueKey]], [[BaseBehavior]] runtime value bag.
**Used by:** [[EffDealDamage]], [[EffHeal]], [[EffAddShield]].

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
