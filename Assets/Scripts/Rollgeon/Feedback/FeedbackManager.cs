using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Audio;
using Rollgeon.Entities.Behaviors;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Orquestador del pipeline §10. Recibe <see cref="FeedbackRequest"/> via
    /// <see cref="RequestFeedbackBlocking"/>, resuelve la entry en <see cref="FeedbackDBSO"/>,
    /// dispatcha por <see cref="FeedbackType"/> y llama al callback cuando termina
    /// (natural, timer, o watchdog). Corre como MonoBehaviour singleton para dueñar
    /// coroutines. TECHNICAL.md §10.1 / §10.5 / §10.8 / §10.12.
    /// </summary>
    /// <remarks>
    /// El manager se instancia desde <see cref="FeedbackManagerBootstrap"/> — no se
    /// agrega a mano a una scene. Vive en <c>DontDestroyOnLoad</c> y se registra a sí
    /// mismo como <see cref="IFeedbackService"/> en el <see cref="ServiceLocator"/>.
    /// </remarks>
    public sealed class FeedbackManager : MonoBehaviour, IFeedbackService
    {
        private const float WatchdogSafetySeconds = 0.5f;
        private const float SequenceSafetySeconds = 2f;
        private const float UnknownStepDurationEstimate = 5f;

        private FeedbackDBSO _db;
        private int _nextInstanceId = 1;
        private readonly Dictionary<int, ActiveFeedback> _activeFeedbacks = new Dictionary<int, ActiveFeedback>();

        private static FloatingNumberView _floatingNumberPrefabCache;

        // ===================================================================
        // Bootstrap wiring
        // ===================================================================

        public void Configure(FeedbackDBSO db)
        {
            _db = db;
            if (_db == null)
                Debug.LogWarning("[FeedbackManager] Configure called without FeedbackDBSO — " +
                                 "single feedbacks por id no van a resolver.");
        }

        // ===================================================================
        // IFeedbackService
        // ===================================================================

        public void RequestFeedbackBlocking(FeedbackRequest request, Action onComplete)
        {
            if (request.IsSequence)
            {
                RunSequence(request, onComplete);
                return;
            }

            if (_db == null)
            {
                Debug.LogWarning($"[FeedbackManager] No DB configured — short-circuit '{request.FeedbackId}'.");
                onComplete?.Invoke();
                return;
            }

            if (!_db.TryGetFeedback(request.FeedbackId, out var entry) || entry == null)
            {
                Debug.LogWarning($"[FeedbackManager] Feedback id '{request.FeedbackId}' not found in DB.");
                onComplete?.Invoke();
                return;
            }

            int instanceId = _nextInstanceId++;
            var active = new ActiveFeedback { CompletionCallback = onComplete };
            _activeFeedbacks[instanceId] = active;

            StartCoroutine(ExecuteLocalFeedback(instanceId, entry, request));
            StartCoroutine(FeedbackTimeoutCoroutine(instanceId, entry.Duration + WatchdogSafetySeconds));
        }

        // ===================================================================
        // Single feedback execution
        // ===================================================================

        private IEnumerator ExecuteLocalFeedback(int instanceId, FeedbackEntry entry, FeedbackRequest request)
        {
            var handle = PlayFeedbackEntry(entry, request);

            if (handle.Listener != null)
            {
                float deadline = Time.time + Mathf.Max(0.1f, entry.Duration);
                while (handle.Listener != null && !handle.Listener.IsCompleted && Time.time < deadline)
                    yield return null;
            }
            else
            {
                yield return new WaitForSeconds(entry.Duration);
            }

            // Cleanup VFX si corresponde
            if (handle.SpawnedVfx != null)
            {
                if (entry.ShouldDestroyOnParticleEnd && handle.Listener != null && handle.Listener.IsCompleted)
                    Destroy(handle.SpawnedVfx);
                else if (!entry.ShouldDestroyOnParticleEnd)
                    Destroy(handle.SpawnedVfx);
                // si ShouldDestroyOnParticleEnd pero el listener no terminó, dejamos que
                // el particle se auto-destruya (stopAction en el prefab).
            }

            CompleteFeedback(instanceId);
        }

        // ===================================================================
        // Dispatch por tipo
        // ===================================================================

        private PlaybackHandle PlayFeedbackEntry(FeedbackEntry entry, FeedbackRequest request)
        {
            var handle = new PlaybackHandle();
            var position = FeedbackPositionResolver.Resolve(
                entry, request.SourceGuid, request.TargetGuid, request.WorldPosition, request.Player);

            switch (entry.Type)
            {
                case FeedbackType.VFX:
                    DispatchVFX(entry, position, handle);
                    break;
                case FeedbackType.SFX:
                    DispatchSFX(entry, position);
                    break;
                case FeedbackType.Animation:
                    DispatchAnimation(entry, request, handle);
                    break;
                case FeedbackType.BehaviorValue:
                    DispatchBehaviorValue(entry, request);
                    break;
                case FeedbackType.FloatingNumber:
                    DispatchFloatingNumber(entry, request);
                    break;
                case FeedbackType.Wait:
                    // no-op — la duración la impone el timer/step
                    break;
            }

            return handle;
        }

        private void DispatchVFX(FeedbackEntry entry, Vector3 position, PlaybackHandle handle)
        {
            if (entry.VfxPrefab == null) return;
            handle.SpawnedVfx = Instantiate(entry.VfxPrefab, position, Quaternion.identity);
            if (entry.CompletionMode == FeedbackCompletionMode.ParticleEnd)
            {
                var listener = handle.SpawnedVfx.GetComponent<FeedbackCallbackListener>()
                               ?? handle.SpawnedVfx.AddComponent<FeedbackCallbackListener>();
                listener.ListenForParticleEnd();
                handle.Listener = listener;
            }
        }

        private void DispatchSFX(FeedbackEntry entry, Vector3 position)
        {
            if (entry.AudioClip == null) return;

            // Routing canónico (§17.A.4): IAudioService respeta master/sfx volumes y el mixer.
            // Fallback a PlayClipAtPoint cuando el service no está registrado — pasa en EditMode
            // tests y en scenes sin AudioManagerBootstrap. Feature no queda rota, sólo sin mixer.
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx(entry.AudioClip, position, entry.Volume);
            else
                AudioSource.PlayClipAtPoint(entry.AudioClip, position, entry.Volume);
        }

        private void DispatchAnimation(FeedbackEntry entry, FeedbackRequest request, PlaybackHandle handle)
        {
            if (string.IsNullOrEmpty(entry.AnimTrigger)) return;

            var target = entry.TargetSourcePawn ? request.SourceGuid : request.TargetGuid;
            var animator = ResolveAnimator(target);
            if (animator == null) return;

            ApplyAnimatorFloats(animator, request.StoredValues);
            animator.SetTrigger(entry.AnimTrigger);

            if (entry.CompletionMode == FeedbackCompletionMode.AnimationEvent)
            {
                var listener = animator.gameObject.GetComponent<FeedbackCallbackListener>()
                               ?? animator.gameObject.AddComponent<FeedbackCallbackListener>();
                listener.ListenForAnimatorStateEnd(animator, entry.AnimTrigger, entry.Duration);
                handle.Listener = listener;
            }
        }

        private void DispatchBehaviorValue(FeedbackEntry entry, FeedbackRequest request)
        {
            if (request.StoredValues == null) return;
            if (!request.StoredValues.TryGetValue(entry.BehaviorValueKey, out var list) || list == null) return;

            var targetGuid = entry.ValueTarget == BehaviorValueTarget.Source
                ? request.SourceGuid
                : request.TargetGuid;

            foreach (var value in list)
            {
                switch (value)
                {
                    case ImpulseBehaviorValue impulse:
                        ApplyImpulse(targetGuid, impulse);
                        break;
                    case FloatingNumberBehaviorValue _:
                        Debug.LogWarning("[FeedbackManager] FloatingNumberBehaviorValue consumed via " +
                                         "FeedbackType.BehaviorValue — prefer FeedbackType.FloatingNumber.");
                        break;
                    // FloatBehaviorValue se consume por ApplyAnimatorFloats — ver §10.10.
                }
            }
        }

        private void DispatchFloatingNumber(FeedbackEntry entry, FeedbackRequest request)
        {
            if (request.StoredValues == null) return;
            if (!request.StoredValues.TryGetValue(entry.FloatingNumberSourceKey, out var list) || list == null) return;

            foreach (var raw in list)
            {
                if (raw is FloatingNumberBehaviorValue fn)
                    StartCoroutine(SpawnFloatingNumberDelayed(fn, request, entry.FloatingNumberSourceKey));
            }
        }

        private IEnumerator SpawnFloatingNumberDelayed(
            FloatingNumberBehaviorValue fn, FeedbackRequest request, BehaviorValueKey key)
        {
            if (fn.Delay > 0f) yield return new WaitForSeconds(fn.Delay);

            // Delegamos al spawner moderno (FloatingDamageSpawner) via el evento legacy.
            // Why: el path antiguo (Resources/FloatingNumber world-space + IPawnRegistry)
            // perdía el ancla cuando el target no estaba en el PawnRegistry, y aún cuando
            // anclaba bien renderizaba con coords del RT del pixel-art pipeline. El spawner
            // moderno ya tiene el fix RT→Screen y resuelve por IEntityPositionResolver +
            // IPawnRegistry como fallback. Misma estética en damage / heal / shield.
            var targetGuid = fn.TargetEntityGuid != Guid.Empty ? fn.TargetEntityGuid : request.TargetGuid;
            var type = KeyToFloatingNumberType(key);
            EventManager.Trigger(EventName.OnFloatingNumberRequested, targetGuid, type, fn.Value, fn.Offset);
        }

        private static FloatingNumberType KeyToFloatingNumberType(BehaviorValueKey key) => key switch
        {
            BehaviorValueKey.FloatingDamage => FloatingNumberType.Damage,
            BehaviorValueKey.FloatingHeal   => FloatingNumberType.Heal,
            BehaviorValueKey.FloatingShield => FloatingNumberType.Shield,
            _                               => FloatingNumberType.Damage,
        };

        // ===================================================================
        // Animator floats — §10.10
        // ===================================================================

        private static void ApplyAnimatorFloats(
            Animator animator,
            IReadOnlyDictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>> storedValues)
        {
            if (animator == null || storedValues == null) return;
            foreach (var kv in storedValues)
            {
                if (kv.Value == null) continue;
                for (int i = kv.Value.Count - 1; i >= 0; i--)
                {
                    if (kv.Value[i] is FloatBehaviorValue f)
                    {
                        animator.SetFloat(kv.Key.ToString(), f.Value);
                        break;
                    }
                }
            }
        }

        // ===================================================================
        // Sequences — §10.8
        // ===================================================================

        private void RunSequence(FeedbackRequest request, Action onComplete)
        {
            int instanceId = _nextInstanceId++;
            var active = new ActiveFeedback { CompletionCallback = onComplete };
            _activeFeedbacks[instanceId] = active;

            var steps = request.SequenceSteps ?? new List<FeedbackSequenceStep>();
            float budget = EstimateSequenceDuration(steps) + SequenceSafetySeconds;

            StartCoroutine(ExecuteLocalSequence(instanceId, steps, request));
            StartCoroutine(FeedbackTimeoutCoroutine(instanceId, budget));
        }

        private IEnumerator ExecuteLocalSequence(
            int instanceId, List<FeedbackSequenceStep> steps, FeedbackRequest request)
        {
            var bus = new FeedbackEventBus();
            var handles = new StepHandle[steps.Count];
            for (int i = 0; i < steps.Count; i++) handles[i] = new StepHandle();

            FeedbackSequenceRuntime.SetCurrent(bus);

            for (int i = 0; i < steps.Count; i++)
                StartCoroutine(RunStep(i, steps, handles, bus, request));

            while (true)
            {
                bool allBlockingDone = true;
                for (int i = 0; i < steps.Count; i++)
                {
                    if (steps[i].BlockSequence && !handles[i].Done)
                    {
                        allBlockingDone = false;
                        break;
                    }
                }
                if (allBlockingDone) break;
                yield return null;
            }

            FeedbackSequenceRuntime.ClearCurrent(bus);
            CompleteFeedback(instanceId);
        }

        private IEnumerator RunStep(
            int stepIndex,
            List<FeedbackSequenceStep> steps,
            StepHandle[] handles,
            FeedbackEventBus bus,
            FeedbackRequest request)
        {
            var step = steps[stepIndex];

            yield return WaitStartTrigger(step, stepIndex, handles, bus);
            if (step.StartDelay > 0f) yield return new WaitForSeconds(step.StartDelay);

            var playbackHandle = DispatchStep(step, request);
            yield return WaitEndTrigger(step, playbackHandle, bus);

            handles[stepIndex].Done = true;
            bus.Publish($"$step.{stepIndex}.end");

            if (playbackHandle.SpawnedVfx != null) Destroy(playbackHandle.SpawnedVfx);
        }

        private IEnumerator WaitStartTrigger(
            FeedbackSequenceStep step, int index, StepHandle[] handles, FeedbackEventBus bus)
        {
            switch (step.StartMode)
            {
                case StepStartMode.Immediate:
                    yield break;
                case StepStartMode.AfterPrevious:
                    if (index == 0) yield break;
                    while (!handles[index - 1].Done) yield return null;
                    yield break;
                case StepStartMode.AfterStep:
                    var dep = Mathf.Clamp(step.StartDependsOnStepIndex, 0, handles.Length - 1);
                    while (!handles[dep].Done) yield return null;
                    yield break;
                case StepStartMode.OnEvent:
                    while (!bus.HasFired(step.StartOnEventKey)) yield return null;
                    yield break;
            }
        }

        private IEnumerator WaitEndTrigger(
            FeedbackSequenceStep step, PlaybackHandle playback, FeedbackEventBus bus)
        {
            switch (step.EndMode)
            {
                case StepEndMode.Immediate:
                    yield break;
                case StepEndMode.OnDuration:
                    var dur = step.DurationOverride > 0f ? step.DurationOverride : GuessDuration(step);
                    yield return new WaitForSeconds(dur);
                    yield break;
                case StepEndMode.OnNaturalEnd:
                    if (playback.Listener != null)
                    {
                        float deadline = Time.time + UnknownStepDurationEstimate;
                        while (!playback.Listener.IsCompleted && Time.time < deadline) yield return null;
                    }
                    else
                    {
                        yield return new WaitForSeconds(GuessDuration(step));
                    }
                    yield break;
                case StepEndMode.OnEvent:
                    while (!bus.HasFired(step.EndOnEventKey)) yield return null;
                    yield break;
            }
        }

        private PlaybackHandle DispatchStep(FeedbackSequenceStep step, FeedbackRequest request)
        {
            var handle = new PlaybackHandle();
            switch (step.Source)
            {
                case StepSource.FeedbackRef:
                    if (_db != null && _db.TryGetFeedback(step.FeedbackRefId, out var entry))
                        handle = PlayFeedbackEntry(entry, request);
                    break;
                case StepSource.InlineWait:
                    break; // duration controlled by EndMode
                case StepSource.InlineAnimation:
                    var target = step.InlineAnimOnSource ? request.SourceGuid : request.TargetGuid;
                    var animator = ResolveAnimator(target);
                    if (animator != null && !string.IsNullOrEmpty(step.InlineAnimTrigger))
                    {
                        ApplyAnimatorFloats(animator, request.StoredValues);
                        animator.SetTrigger(step.InlineAnimTrigger);
                    }
                    break;
                case StepSource.InlineBehaviorValue:
                    if (step.InlineBehaviorValueKey != BehaviorValueKey.None)
                    {
                        var inlineEntry = new FeedbackEntry
                        {
                            Type = FeedbackType.BehaviorValue,
                            BehaviorValueKey = step.InlineBehaviorValueKey,
                            ValueTarget = step.InlineBehaviorValueTarget,
                        };
                        DispatchBehaviorValue(inlineEntry, request);
                    }
                    break;
            }
            return handle;
        }

        private static float EstimateSequenceDuration(List<FeedbackSequenceStep> steps)
        {
            if (steps == null || steps.Count == 0) return 0f;
            float total = 0f;
            foreach (var s in steps)
            {
                if (!s.BlockSequence) continue;
                total += s.StartDelay + GuessDuration(s);
            }
            return Mathf.Max(total, 2f);
        }

        private static float GuessDuration(FeedbackSequenceStep step)
        {
            if (step == null) return 0f;
            if (step.DurationOverride > 0f) return step.DurationOverride;
            if (step.Source == StepSource.InlineWait) return step.WaitDuration;
            if (step.EndMode == StepEndMode.Immediate) return 0f;
            return UnknownStepDurationEstimate;
        }

        // ===================================================================
        // Watchdog
        // ===================================================================

        private IEnumerator FeedbackTimeoutCoroutine(int instanceId, float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
            if (_activeFeedbacks.ContainsKey(instanceId))
            {
                Debug.LogWarning($"[FeedbackManager] Feedback {instanceId} timed out. Force completing.");
                CompleteFeedback(instanceId);
            }
        }

        private void CompleteFeedback(int instanceId)
        {
            if (!_activeFeedbacks.TryGetValue(instanceId, out var active)) return;
            _activeFeedbacks.Remove(instanceId);
            active.CompletionCallback?.Invoke();
        }

        // ===================================================================
        // Helpers
        // ===================================================================

        private static Animator ResolveAnimator(Guid guid)
        {
            var t = FeedbackPositionResolver.ResolvePawnTransform(guid);
            if (t == null) return null;
            return t.GetComponent<Animator>() ?? t.GetComponentInChildren<Animator>();
        }

        private static void ApplyImpulse(Guid targetGuid, ImpulseBehaviorValue impulse)
        {
            var guid = impulse.TargetEntityGuid != Guid.Empty ? impulse.TargetEntityGuid : targetGuid;
            var t = FeedbackPositionResolver.ResolvePawnTransform(guid);
            if (t == null) return;
            var consumer = t.GetComponent<HitImpulseConsumer>()
                           ?? t.GetComponentInChildren<HitImpulseConsumer>();
            if (consumer != null) consumer.ApplyImpulse(impulse.Impulse);
        }

        private static FloatingNumberView GetFloatingNumberPrefab()
        {
            if (_floatingNumberPrefabCache != null) return _floatingNumberPrefabCache;
            var loaded = Resources.Load<GameObject>("FloatingNumber");
            if (loaded == null) return null;
            _floatingNumberPrefabCache = loaded.GetComponent<FloatingNumberView>();
            if (_floatingNumberPrefabCache == null)
                Debug.LogWarning("[FeedbackManager] Resources/FloatingNumber.prefab no tiene FloatingNumberView.");
            return _floatingNumberPrefabCache;
        }

        private static FloatingNumberView.NumberType KeyToNumberType(BehaviorValueKey key) => key switch
        {
            BehaviorValueKey.FloatingDamage => FloatingNumberView.NumberType.Damage,
            BehaviorValueKey.FloatingHeal => FloatingNumberView.NumberType.Heal,
            BehaviorValueKey.FloatingShield => FloatingNumberView.NumberType.Shield,
            _ => FloatingNumberView.NumberType.Generic,
        };

        // ===================================================================
        // Internal book-keeping types
        // ===================================================================

        private sealed class ActiveFeedback
        {
            public Action CompletionCallback;
        }

        private sealed class PlaybackHandle
        {
            public GameObject SpawnedVfx;
            public FeedbackCallbackListener Listener;
        }

        private sealed class StepHandle
        {
            public bool Done;
        }
    }
}
