---
title: IUsesValue
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, interface]
---

# IUsesValue

> Marker interface declaring that the effect consumes a `value` —
> sourced from a constant, an entity attribute, or a generic
> parameter. Pairs with [[ICanBeConstantValue]] / [[ICanBeEntityValue]] /
> [[ICanBeGenericValue]] to control which sub-options the inspector
> shows.

## Dependencies
**Used by:** [[EffDealDamage]], [[EffAddShield]], any effect with a
configurable scalar amount.

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
