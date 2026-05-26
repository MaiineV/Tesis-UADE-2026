---
title: FeedbackManager
type: service
domain: 22-Feedback
status: done
tags: [feedback, service, monobehaviour]
---

# FeedbackManager

> Orquestador del pipeline §10. Implementa [[IFeedbackService]] como
> singleton `MonoBehaviour` (dueña coroutines), resuelve el id en el
> [[FeedbackDBSO]], dispatcha por [[FeedbackType]], y completa via
> listener natural / timer / watchdog.

## Overview

`RequestFeedbackBlocking` distingue entre single feedback (resolución
por id) y secuencia (`request.IsSequence`). El watchdog
(`WatchdogSafetySeconds`, `SequenceSafetySeconds`) garantiza que el
callback dispare aunque un listener cuelgue. Ruteo SFX vía
[[IAudioService]] cuando está disponible; fallback a
`AudioSource.PlayClipAtPoint` para EditMode.

## Dispatch por tipo

- `VFX`: instancia `VfxPrefab`, suma `FeedbackCallbackListener` si
  `CompletionMode == ParticleEnd`.
- `SFX`: enruta al [[IAudioService]] (audio service path canónico).
- `Animation`: aplica floats acumulados (`ApplyAnimatorFloats`),
  `SetTrigger`, listener si `CompletionMode == AnimationEvent`.
- `BehaviorValue`: lee del bag (`request.StoredValues`) por key,
  consume `ImpulseBehaviorValue` via [[HitImpulseConsumer]].
- `FloatingNumber`: instancia el prefab `Resources/FloatingNumber`,
  inicializa el [[FloatingNumberView]] con `NumberType` derivado de
  la `BehaviorValueKey`. Honra `Delay` y `TargetEntityGuid` del valor.
- `Wait`: no-op — la duración la impone el step / timer.

## Sequences (§10.8)

`RunSequence` arma un [[FeedbackEventBus]] y un [[FeedbackSequenceRuntime]].
Cada step corre en su propia coroutine respetando `StartMode` /
`EndMode`. La secuencia se completa cuando todos los steps con
`BlockSequence == true` terminan.

## API / Shape

```csharp
public sealed class FeedbackManager : MonoBehaviour, IFeedbackService {
    public void Configure(FeedbackDBSO db);
    public void RequestFeedbackBlocking(FeedbackRequest request, Action onComplete);
}
```

## Dependencies

**Uses:** [[IFeedbackService]], [[FeedbackDBSO]], [[FeedbackEntry]],
[[FeedbackRequest]], [[FeedbackType]], [[FeedbackSequenceStep]],
[[FeedbackEventBus]], [[FeedbackSequenceRuntime]],
[[FeedbackPositionResolver]], [[FeedbackCallbackListener]],
[[FloatingNumberView]], [[HitImpulseConsumer]], [[IAudioService]] (21-Audio),
`BehaviorValueKey` / `BaseBehaviorStoredValue` (05-Entities).
**Used by:** [[FeedbackManagerBootstrap]] (dueño), [[EffPlayFeedback]]
(04-Effects — caller principal).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackManager.cs`

## External references

- TECHNICAL.md §10.1, §10.5, §10.8, §10.10, §10.12.
