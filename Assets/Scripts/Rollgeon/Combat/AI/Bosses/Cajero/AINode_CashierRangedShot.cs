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
    /// El disparo del Cajero: <see cref="Damage"/> directos al jugador a distancia
    /// <see cref="Range"/> o menos, sin área y sin telegráfico. Es lo que hace en los turnos en
    /// que no marca columna. Ficha de diseño "El Cajero" (piso 2).
    /// </summary>
    /// <remarks>
    /// Se auto-gatea por rango en vez de depender de un <c>PcTargetInRange</c> en el árbol: devuelve
    /// Failed si el jugador está lejos y el <c>Selector[Shot, Wait]</c> lo absorbe, así que un rewire
    /// que se olvide la condición no puede convertirlo en un ataque de alcance infinito.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CashierRangedShot : AIActionNode
    {
        [Tooltip("Daño directo del disparo. Ficha: 12.")]
        [MinValue(0)]
        public int Damage = 12;

        [Tooltip("Alcance en casillas. Ficha: 4 — el mismo número que la distancia a la que kitea, " +
                 "para que replegarse no lo saque de su propio rango.")]
        [MinValue(1)]
        public int Range = 4;

        [Tooltip("Métrica de distancia al jugador. Manhattan, igual que AINode_KeepDistance: si " +
                 "difirieran, el jefe se replegaría fuera de su propio alcance.")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        /// <summary>
        /// Event key del Animation Event que marca el frame del disparo. Const y no campo autorable:
        /// un campo nuevo nace vacío en los <c>ED_Boss_*</c> ya serializados (Odin no corre field
        /// initializers al deserializar) y el disparo se quedaría mudo.
        /// </summary>
        private const string ImpactEventKey = "hit";

        public override string NodeName => $"Cajero — Disparo ({Damage} a ≤ {Range})";

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
        /// y no tiene <c>EffectContext</c> que pasarle (por eso <c>FeedbackRequest.Context</c> admite null).
        /// </remarks>
        private static IEnumerator PlayShot(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            var steps = new List<FeedbackSequenceStep>
            {
                new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = BossFeedbackIds.CajeroShotAnim,
                    StartMode = StepStartMode.Immediate,
                    EndMode = StepEndMode.OnDuration,
                    BlockSequence = true,
                },
                // El rig comparte el clip entre disparo y peaje: el chispazo es lo único que los distingue.
                ImpactStep(BossFeedbackIds.CajeroShotImpactVfx),
                ImpactStep(BossFeedbackIds.CajeroShotImpactFeel),
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
    }
}
