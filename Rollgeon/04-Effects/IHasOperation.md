---
title: IHasOperation
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, modifiers, interface]
---

# IHasOperation

> Marker declaring that the effect exposes a `ModifierOperation`
> dropdown (Add, Multiply, Override…).

## Overview

Pure marker — no members. Tells the inspector to surface the
operation selector for effects that author modifiers, alongside
[[IHasDuration]] and [[IHasModifierDirection]].

## API / Shape

Marker — no members.

## Dependencies
**Pairs with:** [[IHasDuration]], [[IHasModifierDirection]],
`Rollgeon.Attributes.Modifiers.ModifierOperation`.
**Used by:** modifier-producing effects.

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
