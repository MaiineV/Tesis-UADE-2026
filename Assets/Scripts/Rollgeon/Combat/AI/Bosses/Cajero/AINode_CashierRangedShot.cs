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
    /// <para>
    /// <b>Por qué existe.</b> Es la mitad de la tenaza que le da presión al jefe. La columna sola
    /// se esquiva con un paso — el jugador salía del área, volvía a pegarle y el Cajero medía 0%
    /// de vida perdida en la mediana de 3000 peleas simuladas. Con el disparo, salir del área ya
    /// no alcanza: para golpearlo hay que estar a distancia 1, y distancia 1 está dentro del rango
    /// del disparo. Esquivar la columna es gratis; dejar de pagar el disparo es dejar de atacar.
    /// </para>
    /// <para>
    /// <b>Se auto-gatea por rango</b> en vez de depender de un <c>PcTargetInRange</c> en el árbol:
    /// el nodo devuelve Failed si el jugador está lejos y el <c>Selector[Shot, Wait]</c> del árbol
    /// lo absorbe. Un rewire que se olvide la condición no puede convertirlo en un ataque de
    /// alcance infinito. Mismo criterio que <c>AINode_TahurPoke</c>.
    /// </para>
    /// <para>
    /// <b>Resuelve por <see cref="IDamagePipeline"/> directo</b>, no por
    /// <c>AINode_Behavior → EnemyActionBehavior → EffDealDamage</c>: el daño base de
    /// <c>EffDealDamage</c> es un campo privado sin setter, así que un builder de editor no puede
    /// autorar los 12 de la ficha — quedaría clavado en el default de 10. El camino directo es el
    /// que ya usan <c>AINode_ExecuteTelegraph</c> y el poke del Tahúr, y pasa por el mismo pipeline
    /// (debilidades, escudo, número flotante).
    /// </para>
    /// <para>
    /// <b>La presentación es parte de la tenaza.</b> El disparo no marca área ni telegrafía, así que
    /// sin animación el jugador ve bajar la vida sin nada que la explique y lee la pelea como "la
    /// columna me pega igual aunque me salga". El camino de play mode
    /// (<see cref="TickCoroutine"/>) retiene el turno con el disparo y aterriza el daño en el frame
    /// del golpe, mismo contrato que <c>AINode_ExecuteTelegraph</c>.
    /// </para>
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
        /// Event key del Animation Event que marca el frame del disparo. No es campo autorable: el
        /// nodo es de un solo jefe y <c>Anim_GeneralDirector_Attack</c> publica esta key y ninguna
        /// otra. Un campo nuevo, además, nace vacío en los <c>ED_Boss_*</c> ya serializados —Odin no
        /// corre field initializers al deserializar— y el disparo se quedaría mudo hasta que alguien
        /// lo re-autorara a mano.
        /// </summary>
        private const string ImpactEventKey = "hit";

        public override string NodeName => $"Cajero — Disparo ({Damage} a ≤ {Range})";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): cobra el disparo en el
        /// acto, sin presentación. No hay dónde esperar el Animation Event y bloquear acá colgaría
        /// el runner de tests.
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

            // Red de seguridad: sin feedback service, sin Animation Event o con el bus perdido, el
            // disparo igual cobra. La presentación puede faltar; el daño de la ficha no.
            resolveOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        /// <remarks>
        /// Incluye el chequeo del pipeline y del daño porque los dos son <c>Failed</c> en el
        /// contrato original: separarlos dejaría al camino coroutine reproduciendo el disparo de un
        /// golpe que nunca iba a cobrar.
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
        /// Se arma el request de secuencia a mano en vez de reusar <c>EffPlaySequence</c>: el nodo no
        /// nace de un effect pass y no tiene <c>EffectContext</c> que pasarle — el mismo caso que la
        /// secuencia de muerte del <c>CombatDeathWatcher</c>, y por eso <c>FeedbackRequest.Context</c>
        /// admite null.
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
                // Impacto ranged y no el melee: el rig del Cajero comparte el clip entre los dos
                // ataques, así que lo único que distingue el disparo del peaje es el chispazo.
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

            // Sin TurnManager no hay gate que esperar — el disparo igual corre, pero el daño no
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
        /// chispazo a la ficha que sale y no al inicio del clip.
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
        /// Gira al jefe hacia el jugador antes de tirar la ficha. Sin esto dispara mirando hacia
        /// donde kiteó el turno anterior, que en un enemigo que se aleja es justo el lado opuesto.
        /// No-op sin capa visual (EditMode).
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
