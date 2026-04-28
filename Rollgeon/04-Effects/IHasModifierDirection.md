---
title: IHasModifierDirection
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, modifiers, interface]
---

# IHasModifierDirection

> Marker declaring that the effect exposes a `ModifierDirection`
> dropdown (buff vs. debuff orientation).

## Overview

Pure marker — no members. Triggers the inspector to render the
direction dropdown for modifier-producing effects, alongside
[[IHasDuration]] and [[IHasOperation]].

## API / Shape

Marker — no members.

## Dependencies
**Pairs with:** [[IHasDuration]], [[IHasOperation]],
`Rollgeon.Attributes.Modifiers.ModifierDirection`.
**Used by:** modifier-producing effects.

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
