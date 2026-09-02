using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Visuals;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Va antes del repliegue en el Sequence: la distancia se mide al empezar el turno del jefe, y
    /// después de <see cref="AINode_KeepDistance"/> el lápiz no cobraría nunca.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_AnotadorPencil : AIActionNode
    {
        [Tooltip("Daño del lápiz. Va directo al pipeline: no pasa por telegraph ni por área amenazada.")]
        [MinValue(0)]
        public int Damage = 12;

        [Tooltip("Alcance en casillas. 1 = pegado.")]
        [MinValue(1)]
        public int Range = 1;

        [Tooltip("Métrica de distancia al jugador. Manhattan = las casillas desde las que el jugador " +
                 "puede pegarle a él.")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        /// <summary>Const y no campo autorable: un campo nuevo nace vacío en los <c>ED_Boss_*</c> ya serializados (Odin no corre field initializers).</summary>
        private const string ImpactEventKey = "hit";

        public override string NodeName => $"Anotador — Lápiz ({Damage})";

        /// <summary>Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): bloquear acá colgaría el runner de tests.</summary>
        public override AIResult Tick(AIContext context)
        {
            if (!CanStab(context)) return AIResult.Failed;

            Stab(context);
            return AIResult.Succeeded;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!CanStab(context))
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
                Stab(context);
            };

            var swing = PlayStab(context, resolveOnce);
            while (swing.MoveNext()) yield return swing.Current;

            // Red de seguridad: sin presentación o con el bus perdido, el lápiz igual cobra.
            resolveOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        private bool CanStab(AIContext context)
        {
            if (context?.Grid == null) return false;
            if (context.SelfGuid == Guid.Empty || context.PlayerGuid == Guid.Empty) return false;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return false;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return false;

            int distance = Metric == DistanceMetric.Manhattan
                ? selfCoord.Manhattan(playerCoord)
                : selfCoord.Chebyshev(playerCoord);
            if (distance > Mathf.Max(1, Range)) return false;

            // LOS de proyecto: detrás de algo no hay lápiz.
            if (!GridLineOfSight.HasClearLine(context.Grid, selfCoord, playerCoord,
                                              context.SelfGuid, context.PlayerGuid))
            {
                return false;
            }

            return context.DamagePipeline != null && Damage > 0;
        }

        private void Stab(AIContext context)
        {
            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = Damage,
                Kind = Kind,
            });
        }

        private static IEnumerator PlayStab(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            var steps = new List<FeedbackSequenceStep>
            {
                new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = BossFeedbackIds.AnotadorPencilAnim,
                    StartMode = StepStartMode.Immediate,
                    EndMode = StepEndMode.OnDuration,
                    BlockSequence = true,
                },
                ImpactStep(BossFeedbackIds.AnotadorImpactVfx),
                ImpactStep(BossFeedbackIds.AnotadorImpactFeel),
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

            // Sin TurnManager no hay gate que esperar: la estocada corre igual, sin sincronizar el daño.
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

        /// <summary>Arrancan en el frame del golpe, para atar el chispazo al lápiz en vez de al inicio del clip.</summary>
        private static FeedbackSequenceStep ImpactStep(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.OnEvent,
            StartOnEventKey = ImpactEventKey,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };

        /// <summary>El lápiz sale después del repliegue del turno anterior: sin esto apuñala mirando hacia donde huyó.</summary>
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
