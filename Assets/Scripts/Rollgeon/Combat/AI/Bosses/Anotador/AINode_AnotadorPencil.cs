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
    /// El lápiz del Anotador (piso 2): 12 de daño melee <b>directo</b> —sin marca y sin área— contra
    /// el jugador que esté pegado cuando le toca el turno. Ficha de diseño "El Anotador".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué directo y no telegrafiado.</b> El lápiz era un anillo 3×3 avisado un turno antes por
    /// canal auxiliar. Eso ponía un tercer overlay en un piso que ya pinta la franja de fila/columna y
    /// la estela de hielo, y el tercero era justo el que menos decisión cambiaba: 12 de daño que sólo
    /// cobran si el jugador sigue pegado. Cobrado en el acto, el peaje de acercarse se lee sin overlay
    /// y el piso queda para las dos amenazas que sí se esquivan moviéndose.
    /// </para>
    /// <para>
    /// <b>Va antes del repliegue.</b> "Estar a 1 cuando le toca" se mide al empezar su turno, sobre la
    /// posición que el jugador eligió. Después de <see cref="AINode_KeepDistance"/> el boss ya está a
    /// distancia 4 y el lápiz no cobraría nunca, salvo en el caso raro de que el repliegue falle. El
    /// anillo telegrafiado sí tenía que ir después —su área se ancla en la casilla final del boss—,
    /// pero un golpe sin área no arrastra esa restricción.
    /// </para>
    /// <para>
    /// <b>Manhattan y no Chebyshev.</b> El rango del jugador se mide en Manhattan
    /// (<c>SelectionSettings</c>), así que las casillas a Manhattan 1 son exactamente las que tiene
    /// que ocupar para pegarle de melee. El lápiz cobra el peaje de esa casilla, no el de una diagonal
    /// desde la que nadie ataca.
    /// </para>
    /// <para>
    /// <b>La paridad la decide el árbol.</b> El nodo no se auto-gatea por ronda: la alternancia
    /// fila/columna/lápiz es una propiedad del ciclo de turno del jefe y vive en un solo lugar (el
    /// <see cref="AINode_If"/> que lo cuelga). Un <c>Failed</c> por estar lejos es el caso mayoritario,
    /// así que en el árbol va dentro de un <c>Selector[…, Wait]</c> como el resto.
    /// </para>
    /// <para>
    /// <b>Sin telegraph no hay overlay que lo anuncie</b>, así que la única señal de que el lápiz
    /// entró es la presentación: sin ella el jugador ve un 12 flotante salir de la nada y no puede
    /// aprender que la casilla pegada se paga. Por eso el camino de play mode
    /// (<see cref="TickCoroutine"/>) retiene el turno con la estocada y aterriza el daño en el frame
    /// de impacto, mismo contrato que <see cref="AINode_ExecuteTelegraph"/>.
    /// </para>
    /// </remarks>
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

        /// <summary>
        /// Event key del Animation Event que marca el frame en que el lápiz entra. No es campo
        /// autorable: el nodo es de un solo jefe y <c>Anim_ChestMimic_Attack</c> publica esta key y
        /// ninguna otra. Un campo nuevo, además, nace vacío en los <c>ED_Boss_*</c> ya serializados
        /// —Odin no corre field initializers al deserializar— y el golpe se quedaría mudo hasta que
        /// alguien lo re-autorara a mano.
        /// </summary>
        private const string ImpactEventKey = "hit";

        public override string NodeName => $"Anotador — Lápiz ({Damage})";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): cobra el lápiz en el acto,
        /// sin presentación. No hay dónde esperar el Animation Event y bloquear acá colgaría el
        /// runner de tests.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (!CanStab(context)) return AIResult.Failed;

            Stab(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode: estocada + impacto sobre el jugador, con el daño aterrizado en el
        /// frame del golpe y el turno retenido hasta que el clip termina.
        /// </summary>
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

            // Red de seguridad: sin feedback service, sin Animation Event o con el bus perdido, el
            // lápiz igual cobra. La presentación puede faltar; el daño de la ficha no.
            resolveOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        /// <remarks>
        /// Incluye el chequeo del pipeline y del daño porque los dos son <c>Failed</c> en el
        /// contrato original: separarlos dejaría al camino coroutine reproduciendo la estocada de un
        /// golpe que nunca iba a cobrar.
        /// </remarks>
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

        /// <remarks>
        /// Se arma el request de secuencia a mano en vez de reusar <c>EffPlaySequence</c>: el nodo no
        /// nace de un effect pass y no tiene <c>EffectContext</c> que pasarle — el mismo caso que la
        /// secuencia de muerte del <c>CombatDeathWatcher</c>, y por eso <c>FeedbackRequest.Context</c>
        /// admite null.
        /// </remarks>
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

            // Sin TurnManager no hay gate que esperar — la estocada igual corre, pero el daño no
            // queda sincronizado. Mismo degradado que EffPlaySequence.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            bool impactFired = false;

            // El bus es latched, así que pollear HasFired por frame alcanza para enganchar el
            // Animation Event sin suscribirse a nada. El wait canónico trae su propio timeout y el
            // force-reset del depth.
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
        /// VFX y Feel del impacto arrancan en el frame del golpe y bloquean la secuencia, igual que
        /// los steps de impacto del ataque del Warrior (<c>CH_Warrior</c>). Es lo que ata el
        /// chispazo al lápiz en vez de al inicio del clip.
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
        /// Gira al jefe hacia el jugador antes de la estocada: el lápiz sale después del repliegue
        /// del turno anterior, así que sin esto apuñala mirando hacia donde huyó. No-op sin capa
        /// visual (EditMode).
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
