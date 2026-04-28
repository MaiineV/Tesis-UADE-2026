---
title: ICanBeAnimFeedback
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, feedback, interface]
---

# ICanBeAnimFeedback

> Marker that enables the "animation" option in the feedback-type
> dropdown for an effect that already implements [[IUsesFeedback]].

## Overview

Pure marker — no members. The inspector reads it to reveal the anim
sub-section when authoring a feedback-producing effect. Pairs with
[[ICanBeSFXFeedback]] and [[ICanBeVFXFeedback]] so each feedback kind
opts in independently.

## API / Shape

Marker — no members.

## Dependencies
**Pairs with:** [[IUsesFeedback]].
**Used by:** [[EffPlayFeedback]] and any future anim-driven feedback
effect.

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
