---
title: IUsesFeedbackTarget
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, feedback, interface]
---

# IUsesFeedbackTarget

> Marker interface declaring that the effect's feedback is anchored to
> a specific target entity (vs being a global / world-space
> feedback).

## Dependencies
**Used by:** any [[EffPlayFeedback]] variant that needs the feedback
to play at the target's position.

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
