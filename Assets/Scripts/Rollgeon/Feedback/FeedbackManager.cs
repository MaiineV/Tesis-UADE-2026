using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Audio;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Visuals;
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
                case FeedbackType.Feel:
                    DispatchFeel(entry, position);
                    break;
                case FeedbackType.PawnDeath:
                    DispatchPawnDeath(entry, request, handle);
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

        private void DispatchFeel(FeedbackEntry entry, Vector3 position)
        {
            if (entry.FeelPlayerPrefab == null) return;

            var player = Instantiate(entry.FeelPlayerPrefab, position, Quaternion.identity);

            // El default (InitializationMode.Start) inicializa recién en el Start del player —
            // un frame después de este Instantiate, o sea DESPUÉS del PlayFeedbacks de abajo.
            // CaptureRestPose lo pasa a modo Script e inicializa ya.
            MmfJuice.CaptureRestPose(player);
            MmfJuice.Replay(player, entry.FeelIntensity);

            // El player instanciado no tiene dueño: no lo colgamos del PlaybackHandle porque
            // el cleanup de ese path está atado a la semántica de ShouldDestroyOnParticleEnd
            // de los VFX. Se auto-destruye cuando el más largo de los dos relojes terminó.
            float life = Mathf.Max(entry.Duration, player.TotalDuration) + WatchdogSafetySeconds;
            Destroy(player.gameObject, life);
        }

        /// <summary>
        /// Tween de muerte sobre el transform del pawn target. Hecho desde código
        /// (no hay clip de death) → una sola entry sirve para cualquier pawn.
        /// </summary>
        private void DispatchPawnDeath(FeedbackEntry entry, FeedbackRequest request, PlaybackHandle handle)
        {
            var pawn = FeedbackPositionResolver.ResolvePawnTransform(request.TargetGuid);
            if (pawn == null) return;

            if (entry.DeathHideHealthBar)
            {
                // La barra es hija del pawn: sin esto se encoge junto con él y queda un
                // sprite de HP girando arriba del cadáver.
                var owner = pawn.GetComponent<EntityPawn>();
                if (owner != null && owner.HealthBar != null)
                    owner.HealthBar.gameObject.SetActive(false);
            }

            var listener = pawn.GetComponent<FeedbackCallbackListener>();
            if (listener == null) listener = pawn.gameObject.AddComponent<FeedbackCallbackListener>();
            handle.Listener = listener;

            // La coroutine corre en el manager, no en el pawn: el pawn se destruye ni bien
            // esto completa, y una coroutine no sobrevive al Destroy de su propio host.
            StartCoroutine(PawnDeathRoutine(pawn, entry, listener));
        }

        private IEnumerator PawnDeathRoutine(Transform pawn, FeedbackEntry entry, FeedbackCallbackListener listener)
        {
            float duration = Mathf.Max(0.05f, entry.Duration);
            var startScale = pawn.localScale;
            var endScale = startScale * Mathf.Clamp01(entry.DeathEndScale);
            var startPos = pawn.position;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // El pawn puede morir dos veces (despawn de sala, restart de run) — si lo
                // destruyeron abajo nuestro, cortamos y completamos igual para no colgar
                // al caller que espera el callback.
                if (pawn == null) break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ease-in cúbico: se sostiene un instante y después colapsa de golpe.
                // Un lerp lineal se lee como "se apagó", no como "lo reventaron".
                float collapse = t * t * t;
                pawn.localScale = Vector3.Lerp(startScale, endScale, collapse);
                pawn.Rotate(0f, entry.DeathSpinDegrees * (Time.deltaTime / duration), 0f, Space.Self);

                // Arco: sube y vuelve — sin(π·t) vale 0 en ambas puntas.
                var pos = startPos;
                pos.y += Mathf.Sin(t * Mathf.PI) * entry.DeathRiseHeight;
                pawn.position = pos;

                yield return null;
            }

            if (pawn != null) pawn.localScale = endScale;
            listener.MarkCompleted();
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

            var type = KeyToFloatingNumberType(key);

            // Dedup con la ruta A (DamagePipeline → TypedEvent<DamageResolvedPayload> →
            // FloatingDamageSpawner): cuando hay un IDamagePipeline registrado, el daño ya
            // se pintó por esa ruta y esta (ruta B, vía EffDealDamage + FeedbackDB) duplicaría
            // el número. Sin pipeline (EditMode/escenas sin bootstrap) esta ruta queda como
            // único fallback de display, así que no se suprime. Heal/Shield van SOLO por esta
            // ruta (no hay pipeline de heal/shield todavía) — nunca se suprimen.
            bool pipelinePresent = ServiceLocator.HasService<IDamagePipeline>();
            if (ShouldSuppressFloatingNumber(type, pipelinePresent)) yield break;

            // Delegamos al spawner moderno (FloatingDamageSpawner) via el evento legacy.
            // Why: el path antiguo (Resources/FloatingNumber world-space + IPawnRegistry)
            // perdía el ancla cuando el target no estaba en el PawnRegistry, y aún cuando
            // anclaba bien renderizaba con coords del RT del pixel-art pipeline. El spawner
            // moderno ya tiene el fix RT→Screen y resuelve por IEntityPositionResolver +
            // IPawnRegistry como fallback. Misma estética en damage / heal / shield.
            var targetGuid = fn.TargetEntityGuid != Guid.Empty ? fn.TargetEntityGuid : request.TargetGuid;
            EventManager.Trigger(EventName.OnFloatingNumberRequested, targetGuid, type, fn.Value, fn.Offset);
        }

        private static FloatingNumberType KeyToFloatingNumberType(BehaviorValueKey key) => key switch
        {
            BehaviorValueKey.FloatingDamage => FloatingNumberType.Damage,
            BehaviorValueKey.FloatingHeal   => FloatingNumberType.Heal,
            BehaviorValueKey.FloatingShield => FloatingNumberType.Shield,
            _                               => FloatingNumberType.Damage,
        };

        /// <summary>
        /// Decide si la ruta B (BehaviorValue → evento legacy) debe saltear el spawn porque
        /// la ruta A (TypedEvent&lt;DamageResolvedPayload&gt;) ya lo va a pintar. Extraído a
        /// método puro (no <c>internal</c>: el assembly "Rollgeon" no tiene
        /// InternalsVisibleTo("Rollgeon.Feedback.Tests")) para poder testearlo sin depender
        /// del plumbing de corrutinas de <see cref="SpawnFloatingNumberDelayed"/>.
        /// </summary>
        public static bool ShouldSuppressFloatingNumber(FloatingNumberType type, bool pipelinePresent)
        {
            return type == FloatingNumberType.Damage && pipelinePresent;
        }

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

            var steps = request.SequenceSteps;

            // Un request sin steps inline referencia una secuencia autorada en el DB por
            // FeedbackId. Es el caso de las secuencias disparadas desde código (muerte de
            // un enemigo), que no nacen de un EffPlaySequence y no tienen dónde autorar.
            if ((steps == null || steps.Count == 0)
                && !string.IsNullOrEmpty(request.FeedbackId)
                && _db != null
                && !_db.TryGetSequence(request.FeedbackId, out steps))
            {
                Debug.LogWarning($"[FeedbackManager] Sequence id '{request.FeedbackId}' not found in DB.");
            }

            steps ??= new List<FeedbackSequenceStep>();
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

        private float EstimateSequenceDuration(List<FeedbackSequenceStep> steps)
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

        private float GuessDuration(FeedbackSequenceStep step)
        {
            if (step == null) return 0f;
            if (step.DurationOverride > 0f) return step.DurationOverride;
            if (step.Source == StepSource.InlineWait) return step.WaitDuration;
            if (step.EndMode == StepEndMode.Immediate) return 0f;

            // Un step que referencia el DB ya trae su duración autorada en la entry. Sin esto
            // caía siempre al estimate genérico de 5s, y como el TurnManager espera a la
            // secuencia, cada golpe congelaba el turno 5 segundos.
            if (step.Source == StepSource.FeedbackRef
                && _db != null
                && _db.TryGetFeedback(step.FeedbackRefId, out var entry)
                && entry != null
                && entry.Duration > 0f)
            {
                return entry.Duration;
            }

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

            // Ojo: NO usar `??` acá. GetComponent devuelve un "fake null" (referencia C# viva
            // que el operator== de Unity reporta como null), así que `??` se lo queda en vez de
            // caer al fallback — y DispatchAnimation abortaba en silencio con los pawns que
            // tienen el Animator en el hijo del modelo, que son todos.
            var own = t.GetComponent<Animator>();
            if (own != null) return own;
            return t.GetComponentInChildren<Animator>(includeInactive: true);
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
