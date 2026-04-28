---
title: ClassPassiveSO
type: so
domain: 06-Heroes
status: done
tags: [heroes, so, passive]
---

# ClassPassiveSO

> `ScriptableObject` describing a hero class' passive — a list of
> [[PassiveHook]]s, each binding a legacy [[EventName]] trigger to an
> `EffectData` pipeline.

## Overview

Authored asset that lives next to a [[ClassHeroSO]]. At run start the
hero entity binds the passive: each `PassiveHook` subscribes its
`EffectData` to the named [[EventManager]] event. Handlers are filtered
by `InstanceId` at bind time so foreign carriers don't trigger.

## Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Heroes/Class Passive")]
public class ClassPassiveSO : SerializedScriptableObject {
    public string PassiveId;     // "passive.warrior.rage"
    public string DisplayName;
    public string Description;

    [OdinSerialize]
    public List<PassiveHook> Hooks = new();
}
```

## Dependencies

- **Uses:** [[PassiveHook]], [[EventName]], `EffectData`.
- **Used by:** [[ClassHeroSO]]`.Passive`, hero entity passive bind in
  [[RunController]]`.RegisterPlayer`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Heroes/ClassPassiveSO.cs`
- Tests: `.../Tests/ClassPassiveSOTests.cs`

## External references

- TECHNICAL.md: §4.4 / §4.4.1 Class passives
