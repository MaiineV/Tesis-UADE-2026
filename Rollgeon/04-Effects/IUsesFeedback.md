---
title: IUsesFeedback
type: interface
domain: 04-Effects
status: done
tags: [effects, capabilities, marker, feedback, interface]
---

# IUsesFeedback

> Marker interface declaring that the effect produces a feedback
> request (§10). Pairs with [[ICanBeAnimFeedback]] /
> [[ICanBeSFXFeedback]] / [[ICanBeVFXFeedback]] markers (in
> `CapabilityInterfaces.cs`) to declare which feedback types are
> selectable in the inspector.

## Dependencies
**Used by:** [[EffPlayFeedback]].

## Code
`Assets/Scripts/Rollgeon/Effects/CapabilityInterfaces.cs`
