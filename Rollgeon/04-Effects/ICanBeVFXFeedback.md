---
title: ICanBeVFXFeedback
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, feedback, interface]
---

# ICanBeVFXFeedback

> Marker that enables the "VFX" option in the feedback-type dropdown
> for an effect implementing [[IUsesFeedback]].

## Overview

Pure marker — no members. Reveals the VFX authoring section in the
inspector. Pairs with [[ICanBeAnimFeedback]] / [[ICanBeSFXFeedback]].

## API / Shape

Marker — no members.

## Dependencies
**Pairs with:** [[IUsesFeedback]].
**Used by:** [[EffPlayFeedback]].

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
