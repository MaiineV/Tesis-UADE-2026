---
title: FeedbackEntry
type: class
domain: 22-Feedback
status: done
tags: [feedback, data]
---

# FeedbackEntry

> Una entry serializable del [[FeedbackDBSO]]. El [[FeedbackType]]
> controla qué campos condicionales se muestran (Odin `[ShowIf]`).

## Overview

Identidad (`FeedbackId`, `Type`), positioning (`Position`,
`PositionReaderSO`, `PlayerTarget`, `PositionOffset`), completion
(`Duration`, `CompletionMode`), y blocks por tipo: VFX (`VfxPrefab`,
`ShouldDestroyOnParticleEnd`), SFX (`AudioClip`, `Volume`), Animation
(`AnimTrigger`, `TargetSourcePawn`), BehaviorValue (`BehaviorValueKey`,
`ValueTarget`), FloatingNumber (`FloatingNumberSourceKey`).

## API / Shape

```csharp
[Serializable]
public class FeedbackEntry {
    public string FeedbackId;
    public FeedbackType Type;
    public SpawnPosition Position;
    public ScriptableObject PositionReaderSO;
    public FeedbackPlayer PlayerTarget;
    public Vector3 PositionOffset;
    public float Duration;
    public FeedbackCompletionMode CompletionMode;

    // VFX
    public GameObject VfxPrefab;
    public bool ShouldDestroyOnParticleEnd;

    // SFX
    public AudioClip AudioClip;
    public float Volume;

    // Animation
    public string AnimTrigger;
    public bool TargetSourcePawn;

    // BehaviorValue
    public BehaviorValueKey BehaviorValueKey;
    public BehaviorValueTarget ValueTarget;

    // FloatingNumber
    public BehaviorValueKey FloatingNumberSourceKey;
}
```

## Dependencies

**Uses:** [[FeedbackType]], [[SpawnPosition]],
[[FeedbackCompletionMode]], [[FeedbackPlayer]],
[[BehaviorValueTarget]], [[IPositionReader]] (`PositionReaderSO` se
castea a la interfaz), `BehaviorValueKey` (05-Entities).
**Used by:** [[FeedbackDBSO]], [[FeedbackManager]] (dispatch),
[[FeedbackPositionResolver]].

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackEntry.cs`

## External references

- TECHNICAL.md §10.2 — Entry shape.
