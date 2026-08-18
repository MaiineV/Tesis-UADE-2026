using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// El brazo de La Bandida: 12 de daño melee directo a quien haya terminado su turno pegado a la
    /// máquina. Sin marca y sin área. Ficha de diseño "La Bandida" (piso 1).
    /// </summary>
    /// <remarks>
    /// "Termina el turno pegado" se lee del presente: el jefe actúa después del jugador (CNF-006). Se
    /// auto-gatea aunque el árbol ya lo envuelva en un <c>If(PcTargetInRange)</c>, y el
    /// <see cref="Metric"/> tiene que ser el mismo que el del gate o una de las dos mitades miente
    /// sobre las diagonales.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_BandidaArm : AIActionNode
    {
        [Tooltip("Daño del brazo. Directo, sin marca previa.")]
        [MinValue(0)]
        public int Damage = 12;

        [Tooltip("Alcance en casillas. 1 = pegado a la máquina.")]
        [MinValue(1)]
        public int Range = 1;

        [Tooltip("Métrica de distancia al jugador. Chebyshev incluye las diagonales — tiene que " +
                 "coincidir con la del PcTargetInRange que gatea el nodo.")]
        public DistanceMetric Metric = DistanceMetric.Chebyshev;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Animación de la palanca bajando. Vacío = el id canónico del brazo (ver remarks).")]
        public string ArmFeedbackId = BossFeedbackIds.BandidaArmAnim;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("VFX de impacto sobre el jugador, al terminar el golpe. Vacío = el id canónico.")]
        public string ImpactVfxId = BossFeedbackIds.BandidaImpactVfx;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feel (hitstop/shake) del impacto. Vacío = el id canónico.")]
        public string ImpactFeelId = BossFeedbackIds.BandidaImpactFeel;

        [Tooltip("Event key del Animation Event que marca el frame del golpe. Hoy ningún clip del rig " +
                 "Mecha publica eventos, así que vacío es lo correcto: el daño cae al terminar la " +
                 "secuencia. Cuando el clip tenga su evento, ponerlo acá adelanta el número al golpe.")]
        public string ImpactEventKey;

        public override string NodeName => $"Bandida — Arm ({Damage})";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): el daño y nada más —
        /// bloquear acá colgaría los tests.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (!CanStrike(context)) return AIResult.Failed;

            Strike(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode: palanca → impacto sobre el jugador, con el daño aterrizando en el
        /// golpe si el clip publica <see cref="ImpactEventKey"/> y al final de la secuencia si no.
        /// </summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!CanStrike(context))
            {
                onResult?.Invoke(AIResult.Failed);
                yield break;
            }

            bool resolved = false;
            Action strikeOnce = () =>
            {
                if (resolved) return;
                resolved = true;
                Strike(context);
            };

            var swing = PlaySwing(context, strikeOnce);
            while (swing.MoveNext()) yield return swing.Current;

            // Red de seguridad: sin feedback service o con la secuencia cortada, el golpe igual cobra.
            strikeOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        /// <summary>
        /// El gate completo del nodo. Separado del golpe porque el camino coroutine tiene que decidir
        /// <b>antes</b> de animar: si no, retendría el turno para bajar la palanca sobre nadie.
        /// </summary>
        private bool CanStrike(AIContext context)
        {
            if (context?.Grid == null) return false;
            if (context.DamagePipeline == null || Damage <= 0) return false;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return false;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return false;

            int distance = Metric == DistanceMetric.Manhattan
                ? selfCoord.Manhattan(playerCoord)
                : selfCoord.Chebyshev(playerCoord);
            return distance <= Mathf.Max(1, Range);
        }

        private void Strike(AIContext context)
        {
            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = Damage,
                Kind = Kind,
            });
        }

        /// <remarks>
        /// Request de secuencia a mano y no <c>EffPlaySequence</c>: el nodo no nace de un effect pass
        /// y no tiene <c>EffectContext</c> que pasarle (por eso <c>FeedbackRequest.Context</c> admite null).
        /// </remarks>
        private IEnumerator PlaySwing(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            // Impacto encadenado al final de la palanca (AfterStep 0) y no por evento: ningún clip del
            // rig Mecha publica keys, y un StartMode=OnEvent esperaría algo que nunca llega.
            var steps = new List<FeedbackSequenceStep>(3)
            {
                new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = Authored(ArmFeedbackId, BossFeedbackIds.BandidaArmAnim),
                    StartMode = StepStartMode.Immediate,
                    EndMode = StepEndMode.OnDuration,
                    BlockSequence = true,
                },
                Impact(Authored(ImpactVfxId, BossFeedbackIds.BandidaImpactVfx)),
                Impact(Authored(ImpactFeelId, BossFeedbackIds.BandidaImpactFeel)),
            };

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = steps,
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            // Sin TurnManager no hay gate que esperar: la anim corre igual, sin sincronizar el daño.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            bool impactFired = string.IsNullOrEmpty(ImpactEventKey);

            // El bus es latched: pollear HasFired por frame engancha el Animation Event.
            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext())
            {
                if (!impactFired)
                {
                    var bus = FeedbackSequenceRuntime.Current;
                    if (bus != null && bus.HasFired(ImpactEventKey))
                    {
                        impactFired = true;
                        onImpact?.Invoke();
                    }
                }
                yield return wait.Current;
            }
        }

        /// <summary>VFX/Feel del impacto: arrancan juntos cuando la palanca terminó de bajar.</summary>
        private static FeedbackSequenceStep Impact(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.AfterStep,
            StartDependsOnStepIndex = 0,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };

        /// <summary>
        /// Campo vacío ⇒ el id canónico: Odin no corre field initializers, así que un
        /// <c>ED_Boss_Bandida</c> ya autorado no trae estos campos. Para silenciar un canal se lo
        /// apunta a otra entry, no se lo vacía.
        /// </summary>
        private static string Authored(string authored, string canonical)
            => string.IsNullOrEmpty(authored) ? canonical : authored;

#if UNITY_EDITOR
        // Dropdown obligatorio (§0): los ids de feedback nunca se tipean a mano.
        private static IEnumerable<string> GetFeedbackIdsForDropdown()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FeedbackDBSO"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var db = UnityEditor.AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(path);
                if (db == null) continue;
                foreach (var id in db.GetAllFeedbackIds()) yield return id;
            }
        }
#endif
    }
}
