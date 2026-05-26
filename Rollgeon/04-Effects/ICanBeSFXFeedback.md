---
title: ICanBeSFXFeedback
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, feedback, interface]
---

# ICanBeSFXFeedback

> Marker that enables the "SFX" option in the feedback-type dropdown
> for an effect implementing [[IUsesFeedback]].

## Overview

Pure marker — no members. Reveals the SFX authoring section in the
inspector. Pairs with [[ICanBeAnimFeedback]] / [[ICanBeVFXFeedback]] so
the three feedback kinds stay independently opt-in.

## API / Shape

Marker — no members.

## Dependencies
**Pairs with:** [[IUsesFeedback]].
**Used by:** [[EffPlayFeedback]].

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
