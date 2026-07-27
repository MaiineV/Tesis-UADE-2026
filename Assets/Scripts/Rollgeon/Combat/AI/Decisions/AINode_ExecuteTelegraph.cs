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
    public sealed class AINode_ExecuteTelegraph : AIActionNode
    {
        [Title("Windup")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feedback que corre al cobrar la marca, antes de resolver el daño. " +
                 "Vacío = se resuelve sin animación (comportamiento previo a Feature#0038).")]
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
        /// Camino de play mode. Corre el windup <b>antes</b> de resolver, así el daño del
        /// telegraph aterriza en el frame del golpe igual que el de la acción autorada, en
        /// vez de aparecer con el boss quieto en Idle.
        /// </summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!TryConsumePending(context, out var area))
            {
                onResult?.Invoke(AIResult.Succeeded);
                yield break;
            }

            var windup = PlayWindup(context);
            while (windup.MoveNext()) yield return windup.Current;

            Resolve(context, area);
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        private static bool TryConsumePending(AIContext context, out ThreatenedArea area)
        {
            area = default;
            if (context == null) return false;

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
                return false;

            // Apagar el overlay de advertencia siempre que ejecutemos (haya o no
            // impacto). Antes esto era TileHighlightService.ClearAll(), que además
            // se llevaba puesto cualquier highlight ajeno al telegraph.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(context.SelfGuid);

            return threat.TryConsume(context.SelfGuid, out area);
        }

        /// <remarks>
        /// Se arma un request de secuencia de un solo step en vez de reusar
        /// <c>EffPlaySequence</c>: este nodo no nace de un effect pass, así que no tiene
        /// <c>EffectContext</c> que pasarle (mismo caso que la secuencia de muerte del
        /// <c>CombatDeathWatcher</c>, y por eso <c>FeedbackRequest.Context</c> admite null).
        /// </remarks>
        private IEnumerator PlayWindup(AIContext context)
        {
            if (!HasWindup()) yield break;
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            var step = new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = WindupFeedbackId,
                StartMode = StepStartMode.Immediate,
                BlockSequence = true,
            };
            if (!string.IsNullOrEmpty(ImpactEventKey))
            {
                step.EndMode = StepEndMode.OnEvent;
                step.EndOnEventKey = ImpactEventKey;
            }

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
            // no queda sincronizado. Mismo degradado que EffPlaySequence.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

        private static void Resolve(AIContext context, ThreatenedArea area)
        {
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
