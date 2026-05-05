---
title: IHasDuration
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, modifiers, interface]
---

# IHasDuration

> Marker declaring that the effect creates or affects a modifier with
> a `ModifierLifetime` (turns, encounter, permanent…).

## Overview

Pure marker — no members. The inspector uses it to reveal the
`ModifierLifetime` dropdown alongside [[IHasOperation]] /
[[IHasModifierDirection]] when authoring a modifier-applying effect.

## API / Shape

Marker — no members.

## Dependencies
**Pairs with:** [[IHasOperation]], [[IHasModifierDirection]],
`Rollgeon.Attributes.Modifiers.ModifierLifetime`.
**Used by:** modifier-producing effects (`EffApplyModifier`-style).

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
