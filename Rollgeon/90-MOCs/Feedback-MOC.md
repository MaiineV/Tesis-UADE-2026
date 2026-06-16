---
title: Feedback-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, feedback]
---

# 22-Feedback — Map of Content

> Visual / sonic / animation feedback pipeline (TECHNICAL.md §10).
> Single blocking entry point — `IFeedbackService.RequestFeedbackBlocking`
> — drives single feedbacks and multi-step sequences with watchdogs that
> guarantee the `onComplete` callback fires exactly once.

## Relationships

```
 EffPlayFeedback (04-Effects)
       │  request: id, source/target, stored values, sequence?
       ▼
 IFeedbackService ── RequestFeedbackBlocking(request, onComplete)
       │
 FeedbackManager (impl, MonoBehaviour)
       ├─ FeedbackDBSO  → FeedbackEntry(id → FeedbackType + payload)
       ├─ Dispatch by FeedbackType:
       │     VFX  → prefab + FeedbackCallbackListener
       │     SFX  → IAudioService.PlaySfx
       │     Animation → animator floats / triggers + listener
       │     BehaviorValue → HitImpulseConsumer (bag lookup)
       │     FloatingNumber → FloatingNumberView prefab
       │     Wait → no-op
       ├─ FeedbackPositionResolver (IGridManager + IPawnRegistry)
       └─ Sequences: FeedbackEventBus + FeedbackSequenceRuntime
                     (per-step coroutines, StartMode / EndMode)

 IPawnRegistry ── Guid → Transform (entity world refs)
```

## Pages

### Core contract
- [[IFeedbackService]] — public interface
- [[FeedbackManager]] — real impl with DB + dispatch + watchdog
- [[FeedbackManagerBootstrap]]
- [[FeedbackServiceStub]] · [[FeedbackServiceStubBootstrap]] — instant-
  callback fallback for EditMode / tests
- [[FeedbackPlayer]]

### Data / DB
- [[FeedbackDBSO]] · [[FeedbackEntry]] · [[FeedbackRequest]]
- [[FeedbackSequenceStep]] · [[FeedbackType]]
- [[BehaviorValueTarget]] · [[SpawnPosition]]
- [[FeedbackCompletionMode]] · [[StepSource]] · [[StepStartMode]] · [[StepEndMode]]

### Runtime / dispatch
- [[FeedbackEventBus]] · [[FeedbackSequenceRuntime]]
- [[FeedbackPositionResolver]] · [[FeedbackCallbackListener]]
- [[FloatingNumberView]] · [[HitImpulseConsumer]]

### Pawn registry / position
- [[IPawnRegistry]] · [[PawnRegistry]] · [[PawnRegistryBinding]] · [[PawnRegistryBootstrap]]
- [[IPositionReader]] · [[PositionReadInfo]]

## Cross-domain edges

- **Incoming** (consumers):
  - 04-Effects: [[EffPlayFeedback]] is the canonical caller.
  - 02-Combat: [[DamageContext]] surfaces `HitImpulseDir` consumed by
    [[HitImpulseConsumer]]; floating damage numbers from damage events.
  - 14-UI: floating-damage / floating-gold spawners pipe through here.
- **Outgoing** (dependencies):
  - 00-Foundations: [[ServiceLocator]], [[IPreloadableService]],
    [[EventManager]], [[EventName]].
  - 21-Audio: [[IAudioService]]`.PlaySfx` for SFX dispatch.
  - 23-Camera: [[ICameraService]]`.Shake` for camera-shake feedback.
  - 17-Grid: [[IGridManager]] used by [[FeedbackPositionResolver]].
  - 05-Entities: [[IPawnRegistry]] resolves entity GUID → Transform;
    `BehaviorValueKey` / `BaseBehaviorStoredValue` for stored-value
    payloads.
