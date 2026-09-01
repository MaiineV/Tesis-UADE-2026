using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Visuals;
using Rollgeon.Feedback;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Disparo a distancia genérico: <see cref="Damage"/> directos al jugador a distancia
    /// <see cref="Range"/> o menos, sin área y sin telegráfico.
    /// </summary>
    /// <remarks>
    /// Se auto-gatea por rango en vez de depender de un <c>PcTargetInRange</c> en el árbol: devuelve
    /// Failed si el jugador está lejos y el <c>Selector[Shot, Wait]</c> lo absorbe, así que un rewire
    /// que se olvide la condición no puede convertirlo en un ataque de alcance infinito.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class AINode_RangedShot : AIActionNode, IAIIntentNode
    {
        [Tooltip("Daño directo del disparo.")]
        [MinValue(0)]
        public int Damage = 10;

        [Tooltip("Alcance en casillas.")]
        [MinValue(1)]
        public int Range = 4;

        [Tooltip("Métrica de distancia al jugador. Manhattan es la que usa AINode_KeepDistance: si " +
                 "difirieran, un jefe que kitea a esta distancia podría replegarse fuera de su propio " +
                 "rango.")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feedback ref id de la animación de disparo. Vacío = sin animación — degrada con un " +
                 "warning en el bus, no rompe el turno.")]
        public string AnimFeedbackId;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feedback ref id del VFX de impacto, en el frame del golpe.")]
        public string ImpactVfxFeedbackId;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feedback ref id del Feel (rumble/hitstop) de impacto, en el frame del golpe.")]
        public string ImpactFeelFeedbackId;

        /// <summary>
        /// Event key del Animation Event que marca el frame del disparo. Const y no campo autorable:
        /// un campo nuevo nace vacío en los <c>ED_Boss_*</c> ya serializados (Odin no corre field
        /// initializers al deserializar) y el disparo se quedaría mudo.
        /// </summary>
        private const string ImpactEventKey = "hit";

        public override string NodeName => $"Ranged Shot ({Damage} a ≤ {Range})";

        /// <summary>
        /// Hook de subclase para que un jefe resuelva un id propio cuando el campo autorado quedó
        /// vacío.
        /// </summary>
        protected virtual string ResolvedAnimFeedbackId => AnimFeedbackId;

        protected virtual string ResolvedImpactVfxFeedbackId => ImpactVfxFeedbackId;

        protected virtual string ResolvedImpactFeelFeedbackId => ImpactFeelFeedbackId;

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): cobra el disparo en el
        /// acto, sin presentación — bloquear acá colgaría el runner de tests.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (!CanFire(context)) return AIResult.Failed;

            FaceTarget(context);
            Fire(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode: disparo + impacto ranged sobre el jugador, con el daño aterrizado en
        /// el frame del golpe y el turno retenido hasta que el clip termina.
        /// </summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!CanFire(context))
            {
                onResult?.Invoke(AIResult.Failed);
                yield break;
            }

            FaceTarget(context);

            bool resolved = false;
            Action resolveOnce = () =>
            {
                if (resolved) return;
                resolved = true;
                Fire(context);
            };

            var shot = PlayShot(context, resolveOnce);
            while (shot.MoveNext()) yield return shot.Current;

            // Red de seguridad: sin presentación o con el bus perdido, el disparo igual cobra.
            resolveOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        /// <remarks>
        /// Incluye el chequeo del pipeline y del daño: separarlos dejaría al camino coroutine
        /// reproduciendo el disparo de un golpe que nunca iba a cobrar.
        /// </remarks>
        /// <summary>
        /// Describe el disparo sobre la casilla del jugador.
        /// </summary>
        /// <remarks>
        /// El disparo apunta al jugador y no a un lugar, así que la casilla no es un compromiso:
        /// se recalcula en cada hover. Va por el mismo <see cref="CanFire"/> que el tick para que
        /// un jefe de alcance corto quede honesto sin escribir nada más.
        /// </remarks>
        public bool TryDescribeIntent(AIContext context, out AIIntent intent)
        {
            intent = default;
            if (!CanFire(context)) return false;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return false;

            intent = new AIIntent(
                AIIntentTextKeys.RangedShot, AIIntentTextKeys.RangedShotFallback,
                Damage, Kind,
                tiles: new[] { playerCoord });
            return true;
        }

        /// <summary>
        /// Como repertorio el disparo se afirma siempre: <see cref="CanFire"/> es el estado de
        /// ESTE turno, y "qué sabe hacer" no depende de dónde esté parado el jugador.
        /// </summary>
        public bool TryDescribeOption(AIContext context, out AIIntent intent)
        {
            intent = new AIIntent(AIIntentTextKeys.RangedShot,
                                  AIIntentTextKeys.RangedShotFallback, Damage, Kind);
            return true;
        }

        private bool CanFire(AIContext context)
        {
            if (context?.Grid == null) return false;
            if (context.SelfGuid == Guid.Empty || context.PlayerGuid == Guid.Empty) return false;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return false;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return false;

            int distance = Metric == DistanceMetric.Manhattan
                ? selfCoord.Manhattan(playerCoord)
                : selfCoord.Chebyshev(playerCoord);
            if (distance > Mathf.Max(1, Range)) return false;

            return context.DamagePipeline != null && Damage > 0;
        }

        private void Fire(AIContext context)
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
        /// y no tiene <c>EffectContext</c> que pasarle. Los tres steps son opcionales.
        /// </remarks>
        private IEnumerator PlayShot(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            var steps = new List<FeedbackSequenceStep>(3);
            if (!string.IsNullOrEmpty(ResolvedAnimFeedbackId))
            {
                steps.Add(new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = ResolvedAnimFeedbackId,
                    StartMode = StepStartMode.Immediate,
                    EndMode = StepEndMode.OnDuration,
                    BlockSequence = true,
                });
            }

            // El rig puede compartir el clip entre disparo y otro gesto: el chispazo/feel en el
            // frame del golpe es lo único que distingue el impacto.
            if (!string.IsNullOrEmpty(ResolvedImpactVfxFeedbackId)) steps.Add(ImpactStep(ResolvedImpactVfxFeedbackId));
            if (!string.IsNullOrEmpty(ResolvedImpactFeelFeedbackId)) steps.Add(ImpactStep(ResolvedImpactFeelFeedbackId));

            if (steps.Count == 0) yield break;

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = steps,
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            // Sin TurnManager no hay gate que esperar: el disparo corre igual, sin sincronizar el daño.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            bool impactFired = false;

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

        /// <summary>
        /// VFX y Feel del impacto: arrancan en el frame del golpe y bloquean la secuencia, para que
        /// el chispazo quede atado a la ficha que sale y no al inicio del clip.
        /// </summary>
        private static FeedbackSequenceStep ImpactStep(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.OnEvent,
            StartOnEventKey = ImpactEventKey,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };

        /// <summary>
        /// Gira al jefe hacia el jugador antes de disparar: si no, apunta hacia donde kiteó el turno
        /// anterior, que en un enemigo que se aleja es el lado opuesto. No-op sin capa visual.
        /// </summary>
        private static void FaceTarget(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) || visuals == null) return;
            if (!visuals.TryGetPawn(context.SelfGuid, out var pawn) || pawn == null) return;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var from)) return;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var to)) return;
            pawn.FaceCoord(from, to);
        }

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
