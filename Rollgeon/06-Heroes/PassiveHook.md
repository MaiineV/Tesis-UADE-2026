---
title: PassiveHook
type: class
domain: 06-Heroes
status: done
tags: [heroes, passive, hook]
---

# PassiveHook

> Single binding inside a [[ClassPassiveSO]] — pairs an [[EventName]]
> trigger with an `EffectData` pipeline.

## Overview

When the hero entity binds its passive, each `PassiveHook` registers
an [[EventManager]] subscription on `TriggerEvent` that runs `Effect`
filtered by the carrier's `InstanceId`.

## Shape

```csharp
[Serializable]
public class PassiveHook {
    public EventName TriggerEvent;

    [OdinSerialize]
    public EffectData Effect = new EffectData();
}
```

## Dependencies

- **Uses:** [[EventName]], `EffectData`.
- **Used by:** [[ClassPassiveSO]]`.Hooks`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Heroes/PassiveHook.cs`

## External references

- TECHNICAL.md: §4.4.1 Passive hook bind
