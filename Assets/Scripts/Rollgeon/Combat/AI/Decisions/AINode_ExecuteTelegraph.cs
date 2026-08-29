using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Acción de "ataque telegráfico — turno N+1": consume el área que el Boss marcó el turno
    /// anterior (<see cref="AINode_TelegraphMark"/>). Si el jugador sigue en alguna casilla
    /// marcada → aplica el daño guardado vía <see cref="IDamagePipeline"/>; si se movió fuera →
    /// el ataque falla sin daño. En ambos casos limpia el resaltado y el estado. Sistemas
    /// prerequisito Bosses §1.
    /// </summary>
    /// <remarks>
    /// Pensado para ir como <b>primer</b> hijo del sequence del Boss (se resuelve al inicio del
    /// turno del Boss, antes de elegir su acción del pool). Siempre retorna
    /// <see cref="AIResult.Succeeded"/> — no es un gate, es una resolución de inicio de turno;
    /// "no había nada pendiente" o "el jugador esquivó" no deben cortar el sequence.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ExecuteTelegraph : AIActionNode, IAIIntentNode
    {
        [Title("Windup")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feedback que corre al cobrar la marca, antes de resolver el daño. " +
                 "Vacío = se resuelve sin animación.")]
        public string WindupFeedbackId;

        [ShowIf(nameof(HasWindup))]
        [Tooltip("Event key del Animation Event que marca el frame de impacto. Vacío = el " +
                 "daño cae cuando el feedback termina por duración, no en el golpe.")]
        public string ImpactEventKey = "hit";

        public override string NodeName => "Execute Telegraph (turn N+1)";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>). Resuelve como
        /// siempre: sin windup, porque no hay dónde esperar el Animation Event.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (!TryConsumePending(context, out var area)) return AIResult.Succeeded;
            Resolve(context, area);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode. Corre el windup y aterriza el daño en el frame del golpe,
        /// pero <b>retiene el turno hasta que la animación termina</b>: el hijo siguiente del
        /// sequence del boss es <see cref="AINode_KeepDistance"/>, y soltar en el impacto lo
        /// dejaba moviéndose con medio ataque todavía reproduciéndose.
        /// </summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!TryConsumePending(context, out var area))
            {
                onResult?.Invoke(AIResult.Succeeded);
                yield break;
            }

            FaceTarget(context);

            bool resolved = false;
            Action resolveOnce = () =>
            {
                if (resolved) return;
                resolved = true;
                Resolve(context, area);
            };

            var windup = PlayWindup(context, resolveOnce);
            while (windup.MoveNext()) yield return windup.Current;

            // Red de seguridad: si la key nunca se publicó (clip sin Animation Event, key mal
            // autorada, sin feedback service) el daño igual cae — tarde, pero cae.
            resolveOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        /// <summary>
        /// La marca ya congelada, leída sin consumir: casillas, daño y tipo son los del
        /// <see cref="IThreatenedAreaService"/>, no una re-deducción del árbol.
        /// </summary>
        public bool TryDescribeIntent(AIContext context, out AIIntent intent)
        {
            intent = default;
            if (context == null) return false;
            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
                return false;
            if (!threat.TryPeek(context.SelfGuid, out var area)) return false;

            intent = new AIIntent(AIIntentTextKeys.Telegraph, "Golpe marcado",
                                  area.Damage, area.Kind, area.Tiles);
            return true;
        }

        /// <summary>
        /// Como repertorio no afirma nada: este nodo cobra lo que otro marcó, y sin marca
        /// pendiente no hay forma, daño ni tipo que prometer.
        /// </summary>
        public bool TryDescribeOption(AIContext context, out AIIntent intent)
        {
            intent = default;
            return false;
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        private static bool TryConsumePending(AIContext context, out ThreatenedArea area)
        {
            area = default;
            if (context == null) return false;

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                ClearOverlay(context);
                return false;
            }

            if (threat.TryConsume(context.SelfGuid, out area)) return true;

            // Nada pendiente: el overlay igual se apaga. Defensivo — un overlay sin área
            // pendiente no deberia existir, pero si quedara no lo limpiaria nadie mas.
            ClearOverlay(context);
            return false;
        }

        private static void ClearOverlay(AIContext context)
        {
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(context.SelfGuid);
        }

        /// <remarks>
        /// Request armado a mano porque el nodo no nace de un effect pass y no tiene
        /// <c>EffectContext</c> que pasarle.
        /// </remarks>
        private IEnumerator PlayWindup(AIContext context, Action onImpact)
        {
            if (!HasWindup()) yield break;
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            // El step bloquea la duración COMPLETA de la entry, no hasta el evento de impacto.
            // El daño no sale de acá: lo dispara el latch del bus (abajo), así que se puede
            // aterrizar en el golpe y aun así retener el turno hasta que el clip termine.
            var step = new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = WindupFeedbackId,
                StartMode = StepStartMode.Immediate,
                EndMode = StepEndMode.OnDuration,
                BlockSequence = true,
            };

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = new List<FeedbackSequenceStep> { step },
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            // Sin TurnManager no hay gate que esperar — la anim igual corre, pero el daño
            // no queda sincronizado.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            bool impactFired = string.IsNullOrEmpty(ImpactEventKey);

            // El bus es latched: pollear HasFired por frame alcanza para enganchar el Animation
            // Event sin suscribirse a nada.
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
        /// Gira al boss hacia el jugador antes del windup. Sin esto queda mirando en la
        /// dirección en la que kiteó el turno anterior y ataca de espaldas.
        /// </summary>
        private static void FaceTarget(AIContext context)
        {
            if (context?.Grid == null || context.PlayerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<Entities.Visuals.IEntityVisualService>(out var visuals) || visuals == null) return;
            if (!visuals.TryGetPawn(context.SelfGuid, out var pawn) || pawn == null) return;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var from)) return;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var to)) return;
            pawn.FaceCoord(from, to);
        }

        /// <remarks>
        /// El overlay se apaga acá y no al consumir la marca: con el windup de por medio, apagarlo
        /// antes deja medio segundo de tiles muertos mientras el boss carga el golpe. Se apaga
        /// siempre que ejecutemos, haya o no impacto.
        /// </remarks>
        private static void Resolve(AIContext context, ThreatenedArea area)
        {
            ClearOverlay(context);

            bool hit = false;
            var grid = context.Grid;
            if (grid != null
                && grid.TryGetPosition(context.PlayerGuid, out var playerCoord)
                && area.Contains(playerCoord))
            {
                hit = true;
                if (context.DamagePipeline != null && area.Damage > 0)
                {
                    context.DamagePipeline.Resolve(new DamageContext
                    {
                        SourceId = context.SelfGuid,
                        TargetId = context.PlayerGuid,
                        BaseDamage = area.Damage,
                        Kind = area.Kind,
                    });
                }
            }

            // Daño incidental al cofre (GDD §22): un AoE que cubre su tile lo golpea con el mismo
            // daño y source no-jugador. Sólo el cofre: iterar al resto de los ocupantes sería
            // friendly fire entre enemigos.
            if (grid != null
                && context.DamagePipeline != null
                && area.Damage > 0
                && ServiceLocator.TryGetService<Rollgeon.Chests.IChestRegistry>(out var chests)
                && chests != null
                && chests.TryGetActiveChest(out var chestGuid)
                && grid.TryGetPosition(chestGuid, out var chestCoord)
                && area.Contains(chestCoord))
            {
                context.DamagePipeline.Resolve(new DamageContext
                {
                    SourceId = context.SelfGuid,
                    TargetId = chestGuid,
                    BaseDamage = area.Damage,
                    Kind = area.Kind,
                });
            }

            EventManager.Trigger(EventName.OnThreatenedAreaResolved, context.SelfGuid, hit);
        }

        // Odin puede instanciar el nodo sin correr field initializers, así que el guard
        // no puede asumir que ImpactEventKey traiga su default.
        private bool HasWindup() => !string.IsNullOrEmpty(WindupFeedbackId);

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
